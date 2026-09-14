using Jellyfin.Plugin.BroadcastBox.Services;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.BroadcastBox.HostedServices;

/// <summary>Ensures publishing is stopped during Jellyfin shutdown.</summary>
public sealed class BroadcastShutdownService : IHostedService
{
    private readonly BroadcastSessionManager _sessionManager;

    /// <summary>Initializes a new instance of the <see cref="BroadcastShutdownService"/> class.</summary>
    public BroadcastShutdownService(BroadcastSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _sessionManager.StopAsync(cancellationToken);
}
