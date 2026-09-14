# Jellyfin Broadcast Box plugin

A private Jellyfin 12 plugin for publishing selected local Jellyfin media to [Broadcast Box](https://github.com/Glimesh/broadcast-box) over FFmpeg WHIP.

The implementation plan is in [PLAN.md](PLAN.md). Version 0.0.1 provides an administrator-only dashboard for configuring Broadcast Box, validating the configured Jellyfin FFmpeg binary, and starting/stopping one publisher process.

## Requirements

- Jellyfin 12 with the matching `Jellyfin.Controller` and `Jellyfin.Model` package version.
- .NET SDK 10 to build.
- Jellyfin's configured FFmpeg must provide `whip`, `libx264`, and `libopus`.
- A configured Broadcast Box WHIP endpoint and publisher token.

## Verify FFmpeg

Run the configured binary on the Jellyfin host/container:

```sh
ffmpeg -hide_banner -formats | grep -i whip
ffmpeg -hide_banner -encoders | grep -E 'libx264|libopus'
```

## Development

```sh
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Install through Jellyfin

In Dashboard → Plugins → Repositories, add this repository URL:

```text
https://raw.githubusercontent.com/kasama-jay/jellyfin-plugin-broadcast-box/main/manifest.json
```

Refresh the catalog, then install **Broadcast Box** from the plugin list. The catalog pins each release ZIP with an MD5 checksum.

## Manual installation

Build and publish the plugin:

```sh
dotnet publish Jellyfin.Plugin.BroadcastBox.csproj --configuration Release --output ./dist
```

Copy the **contents** of `dist/` (including `Jellyfin.Plugin.BroadcastBox.dll` and `meta.json`) to a new subdirectory of Jellyfin's plugins directory, then restart Jellyfin. For example:

```sh
install -d /var/lib/jellyfin/plugins/BroadcastBox
install -m 0644 ./dist/Jellyfin.Plugin.BroadcastBox.dll ./dist/meta.json /var/lib/jellyfin/plugins/BroadcastBox/
```

The local manifest is [`meta.json`](meta.json): it declares the plugin GUID, version, Jellyfin 12 ABI (`12.0.0.0`), and assembly. [`build.yaml`](build.yaml) is the source metadata for eventual Jellyfin plugin-repository/catalog packaging; it is not needed for manual installation.

After restart, open Dashboard → Plugins → Broadcast Box. Configure the WHIP URL, publisher token, and stream key. Confirm the capability check is ready, then enter the Jellyfin item UUID (available in the item URL) and start the broadcast.

## Security and limitations

- Controls require Jellyfin's `RequiresElevation` policy.
- The token is never returned by status APIs or included in captured FFmpeg diagnostics; it remains part of Jellyfin's administrator-controlled plugin configuration.
- v0.0.1 supports one local filesystem video at a time, starts at time zero, transcodes to H.264/Opus, and offers fixed 720p or 1080p presets.
- It does not yet support queues, subtitles, HDR/tone mapping, remote media, live TV, or hardware encoder profiles.

Do not commit Jellyfin configuration, Broadcast Box tokens, or generated plugin artifacts.
