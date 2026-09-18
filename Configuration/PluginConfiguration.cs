using Jellyfin.Plugin.BroadcastBox.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.BroadcastBox.Configuration;

/// <summary>Persistent administrator-controlled plugin settings.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Broadcast Box WHIP endpoint.</summary>
    public string WhipUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the default bounded encoding preset.</summary>
    public BroadcastQualityPreset DefaultPreset { get; set; } = BroadcastQualityPreset.Hd1080;

    /// <summary>Gets or sets a value indicating whether an HTTP LAN endpoint is permitted.</summary>
    public bool AllowInsecureHttp { get; set; }
}
