using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.BroadcastBox.Configuration;

/// <summary>Persistent administrator-controlled plugin settings.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Broadcast Box WHIP endpoint.</summary>
    public string WhipUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Broadcast Box publisher bearer token.</summary>
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the public stream key used for the viewer link.</summary>
    public string StreamKey { get; set; } = string.Empty;
}
