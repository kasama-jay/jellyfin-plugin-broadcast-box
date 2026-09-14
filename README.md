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

Copy the published plugin DLL(s) into a dedicated Jellyfin plugin directory and restart Jellyfin. Do not commit Jellyfin configuration, Broadcast Box tokens, or generated plugin artifacts.

## Status

Scaffold / milestone 1. See [PLAN.md](PLAN.md) for the remaining milestones.
