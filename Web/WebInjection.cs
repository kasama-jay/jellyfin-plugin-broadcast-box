using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.BroadcastBox.Web;

/// <summary>Injects the Broadcast Box Web integration into Jellyfin Web's entry document.</summary>
public static class WebInjection
{
    /// <summary>Appends the embedded Web integration script to the served index document.</summary>
    public static string IndexHtml(JObject payload)
    {
        var contents = payload.Value<string>("contents") ?? string.Empty;
        if (contents.Contains("data-broadcast-box-web", StringComparison.Ordinal))
        {
            return contents;
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Jellyfin.Plugin.BroadcastBox.Inject.broadcastBoxWeb.js")
            ?? throw new InvalidOperationException("Broadcast Box Web resource is missing.");
        using var reader = new StreamReader(stream);
        return contents.Replace("</body>", $"<script data-broadcast-box-web defer>{reader.ReadToEnd()}</script></body>", StringComparison.OrdinalIgnoreCase);
    }
}
