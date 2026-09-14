using System.ComponentModel;
using System.Diagnostics;
using Jellyfin.Plugin.BroadcastBox.Models;
using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.BroadcastBox.Services;

/// <summary>Probes Jellyfin's configured FFmpeg binary without invoking a shell.</summary>
public sealed class FfmpegCapabilityProbe : IDisposable
{
    private readonly IMediaEncoder _mediaEncoder;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private FfmpegCapabilities? _cached;

    /// <summary>Initializes a new instance of the <see cref="FfmpegCapabilityProbe"/> class.</summary>
    public FfmpegCapabilityProbe(IMediaEncoder mediaEncoder)
    {
        _mediaEncoder = mediaEncoder;
    }

    /// <summary>Gets the capabilities required for a Broadcast Box WHIP publisher.</summary>
    public async Task<FfmpegCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var encoderPath = _mediaEncoder.EncoderPath;
        if (string.IsNullOrWhiteSpace(encoderPath) || !File.Exists(encoderPath))
        {
            return Unavailable(encoderPath, "Jellyfin does not have a usable configured FFmpeg binary.");
        }

        await _probeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null && string.Equals(_cached.EncoderPath, encoderPath, StringComparison.Ordinal))
            {
                return _cached;
            }

            try
            {
                var version = await RunAsync(encoderPath, cancellationToken, "-hide_banner", "-version").ConfigureAwait(false);
                var formats = await RunAsync(encoderPath, cancellationToken, "-hide_banner", "-formats").ConfigureAwait(false);
                var encoders = await RunAsync(encoderPath, cancellationToken, "-hide_banner", "-encoders").ConfigureAwait(false);

                _cached = new FfmpegCapabilities(
                    encoderPath,
                    FirstLine(version),
                    ContainsFormat(formats, "whip"),
                    ContainsEncoder(encoders, "libx264"),
                    ContainsEncoder(encoders, "libopus"),
                    null);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception)
            {
                _cached = Unavailable(encoderPath, exception.Message);
            }

            return _cached;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _probeLock.Dispose();

    private static FfmpegCapabilities Unavailable(string encoderPath, string error)
        => new(encoderPath, null, false, false, false, error);

    private static async Task<string> RunAsync(string executable, CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(executable, arguments),
        };
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await standardOutput.ConfigureAwait(false) + await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {FirstLine(output)}");
        }

        return output;
    }

    private static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static bool ContainsFormat(string output, string format)
        => output.Split('\n').Any(line => line.Contains($" {format}", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsEncoder(string output, string encoder)
        => output.Split('\n').Any(line => line.Contains($" {encoder}", StringComparison.OrdinalIgnoreCase));

    private static string? FirstLine(string value)
        => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
