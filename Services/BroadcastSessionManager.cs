using System.Diagnostics;
using Jellyfin.Plugin.BroadcastBox.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BroadcastBox.Services;

/// <summary>
/// Owns the sole FFmpeg publishing process. Process launch is intentionally not
/// implemented until the capability probe and authorization boundary are tested.
/// </summary>
public sealed class BroadcastSessionManager
{
    private readonly ILogger<BroadcastSessionManager> _logger;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private BroadcastSessionStatus _status = BroadcastSessionStatus.Stopped;
    private Process? _process;

    /// <summary>Initializes a new instance of the <see cref="BroadcastSessionManager"/> class.</summary>
    public BroadcastSessionManager(ILogger<BroadcastSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>Gets a snapshot of the current session without exposing secrets.</summary>
    public BroadcastSessionSnapshot GetSnapshot()
        => new(_status, _process?.Id, null, Array.Empty<string>());

    /// <summary>Stops an active publisher, if any.</summary>
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

            _status = BroadcastSessionStatus.Stopping;
            _logger.LogInformation("Stopping Broadcast Box FFmpeg publisher {ProcessId}", _process.Id);
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _process.Dispose();
            _process = null;
            _status = BroadcastSessionStatus.Stopped;
        }
        finally
        {
            _sessionLock.Release();
        }
    }
}
