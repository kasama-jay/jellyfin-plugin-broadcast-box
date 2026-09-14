# Jellyfin Broadcast Box plugin

A private Jellyfin 12 plugin for publishing selected local Jellyfin media to [Broadcast Box](https://github.com/Glimesh/broadcast-box) over FFmpeg WHIP.

The implementation plan is in [PLAN.md](PLAN.md). The initial scaffold establishes the Jellyfin 12/.NET 10 plugin shape, a safe singleton session owner, shutdown handling, and a non-secret status endpoint. It deliberately does **not** start FFmpeg yet.

## Requirements

- Jellyfin 12 with the matching `Jellyfin.Controller` and `Jellyfin.Model` package version.
- .NET SDK 10 to build.
- Jellyfin's configured FFmpeg must provide `whip`, an H.264 encoder, and `libopus`.
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

Do not commit Jellyfin configuration, Broadcast Box tokens, or generated plugin artifacts.

## Status

Scaffold / milestone 1. See [PLAN.md](PLAN.md) for the remaining milestones.
