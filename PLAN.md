# Jellyfin → Broadcast Box plugin plan

## Goal

Publish a selected **local Jellyfin video** from the Jellyfin host to a configured Broadcast Box stream via FFmpeg 8+ WHIP, with start/stop/status controls in Jellyfin.

## v1 scope

- One active broadcast at a time.
- Administrator-only control surface.
- A local library movie or episode; start at 0 and play at native speed.
- Video transcode to H.264; audio transcode to Opus, 48 kHz stereo.
- Broadcast Box URL and bearer token configured by an administrator.
- Explicit Start, Stop, and status/log-tail operations.
- No playlist/queue, subtitles, HDR tonemapping, live TV, remote media, or item-page action initially.

## Key decisions

1. **Target Jellyfin 12 / FFmpeg 8+**. At startup, inspect Jellyfin's configured `IMediaEncoder.EncoderPath`; require the `whip` muxer and usable H.264 and Opus encoders. Show an actionable configuration error if absent.
2. **Own the FFmpeg process.** Do not use `ITranscodeManager.StartFfMpeg`: it expects file/HLS output and owns a different lifecycle. Reuse its configured executable path only.
3. **Use typed process arguments.** Start `ProcessStartInfo` with `UseShellExecute=false` and `ArgumentList`; never construct a shell command or interpolate untrusted values.
4. **Use a dedicated dashboard page and REST API.** Plugin configuration pages and custom ASP.NET controllers are supported. An action in Jellyfin Web's media detail/playback menus is deferred because it needs web-client work or brittle injection.

## Plugin structure

```text
Jellyfin.Plugin.BroadcastBox/
  Plugin.cs                         # BasePlugin config + dashboard page
  Configuration/PluginConfiguration.cs
  Configuration/configPage.html     # target URL, token, quality defaults
  Api/BroadcastController.cs         # start/stop/status/library-search
  Services/PluginServiceRegistrator.cs
  Services/BroadcastSessionManager.cs
  Services/FfmpegCapabilityProbe.cs
  Services/LibraryItemResolver.cs
  Models/*.cs
  HostedServices/ShutdownService.cs  # stop process on Jellyfin shutdown
```

Register `BroadcastSessionManager` as a singleton and the shutdown service as an `IHostedService` through `IPluginServiceRegistrator`.

## API and UI

All endpoints require Jellyfin authentication and elevation/admin authorization.

- `GET /BroadcastBox/capabilities` — configured FFmpeg path/version, WHIP/H264/Opus availability.
- `GET /BroadcastBox/items?search=...` — searchable, permitted local video items.
- `POST /BroadcastBox/session` — body: item ID plus optional stream and encoding overrides; starts the session.
- `GET /BroadcastBox/session` — item metadata, state, PID, start time, sanitized FFmpeg diagnostics, viewer URL.
- `DELETE /BroadcastBox/session` — gracefully stop publishing, then kill after a timeout.

The dashboard page:

1. Displays capability/configuration status.
2. Lets the admin select/search a movie or episode.
3. Offers bounded presets (e.g. 720p/2.5 Mbps, 1080p/5 Mbps), not arbitrary FFmpeg flags.
4. Has Start/Stop and a link to Broadcast Box playback: `<broadcast-box-base>/<stream-key>`.
5. Shows recent sanitized stderr lines and clear failure states.

## Start flow

1. Acquire a single-session async lock; reject a second start with HTTP 409.
2. Verify server config and probe FFmpeg capabilities, cache by executable path/version.
3. Resolve the item with `ILibraryManager.GetItemById<Video>(itemId, userId)`.
4. Reject non-filesystem/unsupported paths in v1; verify the Jellyfin service account can read the path.
5. Validate URL: HTTPS by default; allow HTTP only for explicitly configured private/LAN Broadcast Box endpoints. Restrict to `http`/`https`, no user-provided endpoint per request.
6. Launch FFmpeg with a controlled argument set, capture stderr asynchronously, and wait for either successful WHIP connection evidence or process exit.
7. Publish state transitions: `Starting → Running → Stopping → Stopped/Failed`.

Baseline argument shape:

```text
-re -i <item-path>
-map 0:v:0 -map 0:a:0?
-c:v <approved-h264-encoder> -pix_fmt yuv420p -bf 0 -g <bounded-gop>
-preset <bounded-preset> -tune zerolatency -b:v <bounded-bitrate>
-c:a libopus -ar 48000 -ac 2 -b:a <bounded-bitrate>
-f whip -authorization <secret-token> <configured-whip-url>
```

Hardware encoding is a later controlled preset: validate the configured encoder and its required pixel-format/filter path before offering it.

## Stop and failure handling

- `Stop`: request graceful FFmpeg termination first; wait a short bounded time; kill its process tree if necessary.
- On process exit: atomically record exit code/recent diagnostics, clear the active session, and notify dashboard clients on their next poll.
- On Jellyfin shutdown/plugin disposal: apply the same stop path.
- Never attempt to reconstruct or delete a Broadcast Box WHIP session directly; FFmpeg owns its WHIP lifecycle.

## Security

- Store the Broadcast Box token only in plugin configuration; do not include it in API responses, status objects, UI reloads, or logs.
- Redact `-authorization` and token-bearing URLs before storing FFmpeg command/diagnostics.
- Do not expose arbitrary process arguments, local paths, item IDs outside authorization checks, or arbitrary output URLs.
- Apply admin authorization to all controls; enforce library access again when resolving the item.
- Token rotation is manual in v1: update plugin configuration and restart the next broadcast.

## Testing

### Unit tests

- capability-parser fixtures (`-formats`, `-encoders`, version output);
- argument construction, including paths with spaces/quotes and token redaction;
- item eligibility and authorization decisions;
- session-state concurrency and stop/exit races;
- URL validation and config validation.

### Integration/manual test matrix

1. FFmpeg 8+ detection against the actual Jellyfin 12 binary.
2. A short H.264/AAC and HEVC/5.1 source transcodes and plays through Broadcast Box.
3. Stop/restart; Jellyfin restart while active; Broadcast Box unavailable; invalid token; unreachable WHIP endpoint.
4. A non-admin and a user without library access cannot start or inspect broadcasts.
5. Verify no token appears in Jellyfin logs, API responses, browser developer tools, or persisted status.

## Milestones

1. **Scaffold**: plugin template targeting the exact Jellyfin 12 package API; config page; service registration; controller discovery test.
2. **Capability probe**: use Jellyfin's configured FFmpeg and report WHIP/H264/Opus readiness.
3. **Publisher service**: hard-coded local test item/config, lifecycle management, stderr capture, safe stop.
4. **Controlled API**: authenticated start/stop/status with library-item resolution and one-session lock.
5. **Dashboard UX**: item picker, presets, status/logs, playback link.
6. **Hardening/release**: security review, tests, manual deployment instructions, version compatibility matrix.
7. **Post-v1**: playlist/queue, resume/start offset, subtitles, hardware profiles, HDR handling, per-user profiles, and a proper Jellyfin Web broadcast action.

## Prerequisites before coding

- Exact Jellyfin 12 server version and installation form (Docker/package).
- Actual configured FFmpeg path and output of `-version`, `-formats | grep whip`, and `-encoders` checks.
- Broadcast Box internal URL, HTTPS/TLS arrangement, and whether it uses a reserved stream profile/token.
- A test movie that the Jellyfin service account can read and a private test stream key.
