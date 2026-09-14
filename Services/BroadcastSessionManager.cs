using System.Collections.Concurrent;
using System.Diagnostics;
using Jellyfin.Plugin.BroadcastBox.Configuration;
using Jellyfin.Plugin.BroadcastBox.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BroadcastBox.Services;

/// <summary>Owns the sole FFmpeg process that publishes media to Broadcast Box.</summary>
public sealed class BroadcastSessionManager : IDisposable
{
    private const int DiagnosticLimit = 100;
    private static readonly Action<ILogger, int, Exception?> LogStoppingPublisher = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(1, "StoppingPublisher"),
        "Stopping Broadcast Box FFmpeg publisher {ProcessId}");
    private static readonly Action<ILogger, Guid, BroadcastQualityPreset, Exception?> LogPublisherStarting = LoggerMessage.Define<Guid, BroadcastQualityPreset>(
        LogLevel.Information,
        new EventId(2, "PublisherStarting"),
        "Starting Broadcast Box publisher for item {ItemId} with preset {Preset}");
    private static readonly Action<ILogger, int, int, Exception?> LogPublisherExited = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        new EventId(3, "PublisherExited"),
        "Broadcast Box FFmpeg publisher {ProcessId} exited with code {ExitCode}");

    private readonly FfmpegCapabilityProbe _capabilityProbe;
    private readonly ILogger<BroadcastSessionManager> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ConcurrentQueue<string> _diagnostics = new();
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private Process? _process;
    private BroadcastSessionStatus _status = BroadcastSessionStatus.Stopped;
    private DateTimeOffset? _startedAt;
    private Guid? _itemId;
    private string? _itemName;
    private string? _failure;
    private bool _stopRequested;

    /// <summary>Initializes a new instance of the <see cref="BroadcastSessionManager"/> class.</summary>
    public BroadcastSessionManager(
        FfmpegCapabilityProbe capabilityProbe,
        ILogger<BroadcastSessionManager> logger,
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder)
    {
        _capabilityProbe = capabilityProbe;
        _logger = logger;
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
    }

    /// <summary>Gets a safe snapshot of the current session.</summary>
    public BroadcastSessionSnapshot GetSnapshot()
    {
        var configuration = Plugin.Instance?.Configuration;
        return new BroadcastSessionSnapshot(
            _status,
            _process is { HasExited: false } ? _process.Id : null,
            _startedAt,
            _itemId,
            _itemName,
            ViewerUrl(configuration),
            _failure,
            _diagnostics.ToArray());
    }

    /// <summary>Starts publishing a local Jellyfin video.</summary>
    public async Task<BroadcastSessionSnapshot> StartAsync(
        StartBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false })
            {
                throw new InvalidOperationException("A Broadcast Box session is already active.");
            }

            var configuration = GetConfiguration();
            ValidateConfiguration(configuration);
            var capabilities = await _capabilityProbe.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            if (!capabilities.IsReady)
            {
                throw new InvalidOperationException(CapabilityFailure(capabilities));
            }

            var video = _libraryManager.GetItemById<Video>(request.ItemId)
                ?? throw new ArgumentException("The selected video was not found.", nameof(request));
            if (string.IsNullOrWhiteSpace(video.Path) || !File.Exists(video.Path))
            {
                throw new ArgumentException("Only readable, filesystem-backed videos are supported.", nameof(request));
            }

            ResetSession(video);
            _status = BroadcastSessionStatus.Starting;
            LogPublisherStarting(_logger, video.Id, request.Preset, null);
            var process = new Process
            {
                StartInfo = CreateStartInfo(_mediaEncoder.EncoderPath, video.Path, request.Preset, configuration),
                EnableRaisingEvents = true,
            };
            process.Exited += (_, _) => _ = ObserveExitAsync(process);

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("FFmpeg failed to start.");
                }
            }
            catch
            {
                process.Dispose();
                _status = BroadcastSessionStatus.Failed;
                _failure = "FFmpeg failed to start.";
                throw;
            }

            _process = process;
            _startedAt = DateTimeOffset.UtcNow;
            _status = BroadcastSessionStatus.Running;
            _ = CollectDiagnosticsAsync(process, configuration.BearerToken);
            return GetSnapshot();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>Stops the active publisher, if any.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is null || _process.HasExited)
            {
                _status = BroadcastSessionStatus.Stopped;
                return;
            }

            _stopRequested = true;
            _status = BroadcastSessionStatus.Stopping;
            LogStoppingPublisher(_logger, _process.Id, null);
            await _process.StandardInput.WriteLineAsync("q").ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);

            var exitTask = _process.WaitForExitAsync(cancellationToken);
            if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)).ConfigureAwait(false) != exitTask)
            {
                _process.Kill(entireProcessTree: true);
            }

            await exitTask.ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // FFmpeg may have exited between the state check and the stop request.
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _process?.Dispose();
        _sessionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ObserveExitAsync(Process process)
    {
        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(process, _process))
            {
                return;
            }

            var exitCode = process.ExitCode;
            LogPublisherExited(_logger, process.Id, exitCode, null);
            if (_stopRequested)
            {
                _status = BroadcastSessionStatus.Stopped;
                _failure = null;
            }
            else
            {
                _status = BroadcastSessionStatus.Failed;
                _failure = $"FFmpeg exited with code {exitCode}.";
            }

            _process = null;
            process.Dispose();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task CollectDiagnosticsAsync(Process process, string token)
    {
        while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            AddDiagnostic(Redact(line, token));
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        string inputPath,
        BroadcastQualityPreset preset,
        PluginConfiguration configuration)
    {
        var (width, height, videoBitrate) = preset switch
        {
            BroadcastQualityPreset.Hd720 => (1280, 720, "2500k"),
            _ => (1920, 1080, "5000k"),
        };
        var startInfo = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        var arguments = new[]
        {
            "-hide_banner", "-re", "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-c:v", "libx264", "-profile:v", "baseline", "-pix_fmt", "yuv420p",
            "-vf", $"scale=w={width}:h={height}:force_original_aspect_ratio=decrease",
            "-preset", "veryfast", "-tune", "zerolatency", "-bf", "0", "-g", "48", "-b:v", videoBitrate,
            "-c:a", "libopus", "-ar", "48000", "-ac", "2", "-b:a", "128k",
            "-f", "whip", "-authorization", configuration.BearerToken, configuration.WhipUrl,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string CapabilityFailure(FfmpegCapabilities capabilities)
    {
        if (capabilities.Error is not null)
        {
            return $"Unable to inspect Jellyfin FFmpeg: {capabilities.Error}";
        }

        return "Jellyfin FFmpeg requires the whip muxer and libx264/libopus encoders.";
    }

    private static PluginConfiguration GetConfiguration()
        => Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");

    private static void ValidateConfiguration(PluginConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BearerToken))
        {
            throw new InvalidOperationException("Configure a Broadcast Box bearer token first.");
        }

        if (!Uri.TryCreate(configuration.WhipUrl, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || (endpoint.Scheme == "http" && !configuration.AllowInsecureHttp))
        {
            throw new InvalidOperationException("Configure a valid HTTPS WHIP URL. HTTP requires explicit opt-in.");
        }

        if (string.IsNullOrWhiteSpace(configuration.StreamKey))
        {
            throw new InvalidOperationException("Configure the Broadcast Box stream key first.");
        }
    }

    private static string? ViewerUrl(PluginConfiguration? configuration)
    {
        if (configuration is null || !Uri.TryCreate(configuration.WhipUrl, UriKind.Absolute, out var whipUri)
            || string.IsNullOrWhiteSpace(configuration.StreamKey))
        {
            return null;
        }

        var baseUri = new Uri(whipUri.GetLeftPart(UriPartial.Authority));
        return new Uri(baseUri, Uri.EscapeDataString(configuration.StreamKey)).ToString();
    }

    private static string Redact(string value, string token)
        => string.IsNullOrEmpty(token) ? value : value.Replace(token, "[REDACTED]", StringComparison.Ordinal);

    private void AddDiagnostic(string line)
    {
        _diagnostics.Enqueue(line);
        while (_diagnostics.Count > DiagnosticLimit && _diagnostics.TryDequeue(out _))
        {
        }
    }

    private void ResetSession(Video video)
    {
        _diagnostics.Clear();
        _failure = null;
        _itemId = video.Id;
        _itemName = video.Name;
        _startedAt = null;
        _stopRequested = false;
    }
}
