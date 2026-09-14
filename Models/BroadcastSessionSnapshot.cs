namespace Jellyfin.Plugin.BroadcastBox.Models;

/// <summary>Safe-to-return state of a Broadcast Box publishing session.</summary>
public sealed record BroadcastSessionSnapshot(
    BroadcastSessionStatus Status,
    int? ProcessId,
    DateTimeOffset? StartedAt,
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

    /// <summary>FFmpeg is being stopped.</summary>
    Stopping,

    /// <summary>The publisher exited unexpectedly.</summary>
    Failed,
}
