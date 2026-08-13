# Schedule I DOOM 3 TV

A standalone MelonLoader IL2CPP mod that adds the DOOM 3 IWAD as a native-style TV
application in Schedule I.

This branch contains only the DOOM 3 edition:

- managed output: `ScheduleIDoom3TV.dll`
- TV title: `DOOM 3`
- mod data folder: `Schedule I\Mods\SchedualDoom3Tv`
- supported IWADs: `Doom3.WAD`, `Tnt.wad`, and `Plutonia.wad`
- version: `1.1.0`

The supplied `Doom3.WAD` is a standalone IWAD with the Ultimate Doom map
layout (`E1M1` through `E4M9`). The native runtime detects that layout
directly and does not require a separate base IWAD.

The specific PrBoom-targeted download with SHA-256
`0C91D97E5D7ABAE57A23628DA38C11E69B4ED1046400E814CD66A8B02B183807`
has a damaged final WAD-directory block. On first launch, the mod verifies that
exact file, reconstructs the missing flat entries and namespace markers, and
writes `Runtime\Doom3.compat.WAD`. The original file is never changed. Later
launches reuse the verified compatibility copy.

`Tnt.wad` and `Plutonia.wad` are validated as complete standalone 32-map
IWADs. When present, the TV home screen adds separate `DOOM 3: TNT` and
`DOOM 3: PLUTONIA` entries, so all three WADs can remain installed together.
The supplied TNT and Plutonia files are identified by SHA-256 before launch;
the mod does not modify or redistribute them.

The branch builds one standalone TV application and one install package.
The native runtime includes a Windows `waveOut` mixer for Doom sound effects
and a MUS-to-MIDI/MCI music backend. Audio pauses with the TV app and both
devices are shut down when the app closes.

## Build

```powershell
dotnet build ScheduleIDoom3TV.csproj -c Release
```

The repository package does not include any IWAD. Copy the WADs you own into
`Schedule I\Mods\SchedualDoom3Tv\WAD` using the supported filenames above.
