using Jellyfin.Plugin.BroadcastBox.HostedServices;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.BroadcastBox.Services;

/// <summary>Registers services owned by this plugin.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<BroadcastSessionManager>();
        serviceCollection.AddHostedService<BroadcastShutdownService>();
    }
}
