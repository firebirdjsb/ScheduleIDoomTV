# Schedule I DOOM 3 TV

A standalone MelonLoader IL2CPP mod that adds the DOOM 3 IWAD as a native-style TV
application in Schedule I.

This branch contains only the DOOM 3 edition:

- managed output: `ScheduleIDoom3TV.dll`
- TV title: `DOOM 3`
- mod data folder: `Schedule I\Mods\SchedualDoom3Tv`
- required IWAD: `Schedule I\Mods\SchedualDoom3Tv\WAD\Doom3.WAD`
- version: `1.0.1`

The supplied `Doom3.WAD` is a standalone IWAD with the Ultimate Doom map
layout (`E1M1` through `E4M9`). The native runtime detects that layout
directly and does not require a separate base IWAD.

The specific PrBoom-targeted download with SHA-256
`0C91D97E5D7ABAE57A23628DA38C11E69B4ED1046400E814CD66A8B02B183807`
has a damaged final WAD-directory block. On first launch, the mod verifies that
exact file, reconstructs the missing flat entries and namespace markers, and
writes `Runtime\Doom3.compat.WAD`. The original file is never changed. Later
launches reuse the verified compatibility copy.

The branch builds one standalone TV application and one install package.

## Build

```powershell
dotnet build ScheduleIDoom3TV.csproj -c Release
```

The repository package does not include the IWAD. Copy `Doom3.WAD` to the path
above before opening the TV app.
