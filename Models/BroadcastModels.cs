namespace Jellyfin.Plugin.BroadcastBox.Models;

/// <summary>Configurable, bounded encoding presets.</summary>
public enum BroadcastQualityPreset
{
    /// <summary>1280x720 at 2.5 Mbps.</summary>
    Hd720,

    /// <summary>1920x1080 at 5 Mbps.</summary>
    Hd1080,
}

/// <summary>Request to start publishing a library item.</summary>
public sealed record StartBroadcastRequest(Guid ItemId, BroadcastQualityPreset Preset = BroadcastQualityPreset.Hd1080);

/// <summary>FFmpeg features required to publish to Broadcast Box.</summary>
public sealed record FfmpegCapabilities(
    string EncoderPath,
    string? Version,
    bool HasWhipMuxer,
    bool HasLibx264Encoder,
    bool HasLibopusEncoder,
    string? Error)
{
    /// <summary>Gets whether this FFmpeg binary can be used by the plugin.</summary>
    public bool IsReady => Error is null && HasWhipMuxer && HasLibx264Encoder && HasLibopusEncoder;
}

/// <summary>Safe-to-return state of a Broadcast Box publishing session.</summary>
public sealed record BroadcastSessionSnapshot(
    BroadcastSessionStatus Status,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    Guid? ItemId,
    string? ItemName,
    string? ViewerUrl,
    string? Failure,
    IReadOnlyList<string> RecentDiagnostics);

/// <summary>Publisher lifecycle state.</summary>
public enum BroadcastSessionStatus
{
    /// <summary>No publisher is running.</summary>
    Stopped,

    /// <summary>FFmpeg is starting the WHIP session.</summary>
    Starting,

    /// <summary>FFmpeg is publishing.</summary>
    Running,

    /// <summary>FFmpeg input is paused.</summary>
    Paused,

    /// <summary>FFmpeg is being stopped.</summary>
    Stopping,

    /// <summary>The publisher exited unexpectedly.</summary>
    Failed,
}
