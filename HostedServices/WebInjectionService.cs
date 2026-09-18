using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.BroadcastBox.Web;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.BroadcastBox.HostedServices;

/// <summary>Registers the Jellyfin Web injection through File Transformation.</summary>
public sealed class WebInjectionService : IHostedService
{
    private static readonly Action<ILogger, Exception?> LogDisabled = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, "WebIntegrationDisabled"),
        "Broadcast Box Web integration is disabled: Jellyfin File Transformation 3.x is not installed.");
    private static readonly Action<ILogger, Exception?> LogRegistered = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(2, "WebIntegrationRegistered"),
        "Broadcast Box Web integration registered with File Transformation.");

    private readonly ILogger<WebInjectionService> _logger;

    /// <summary>Initializes a new instance of the <see cref="WebInjectionService"/> class.</summary>
    public WebInjectionService(ILogger<WebInjectionService> logger) => _logger = logger;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var assembly = AssemblyLoadContext.All.SelectMany(context => context.Assemblies)
            .FirstOrDefault(candidate => candidate.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) == true);
        var registrationType = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
        var method = registrationType?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
        if (method is null)
        {
            LogDisabled(_logger, null);
            return Task.CompletedTask;
        }

        var payload = new JObject
        {
            ["id"] = "0d6e3a86-1830-48a0-92b3-5e11e51dcb39",
            ["fileNamePattern"] = "index.html",
            ["callbackAssembly"] = typeof(WebInjection).Assembly.FullName,
            ["callbackClass"] = typeof(WebInjection).FullName,
            ["callbackMethod"] = nameof(WebInjection.IndexHtml),
        };
        method.Invoke(null, [payload]);
        LogRegistered(_logger, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
