# Schedule I DOOM II TV

A standalone MelonLoader IL2CPP mod that adds DOOM II as a native-style TV
application in Schedule I.

This branch contains only the DOOM II edition:

- managed output: `ScheduleIDoom2TV.dll`
- TV title: `DOOM II`
- mod data folder: `Schedule I\Mods\SchedualDoom2Tv`
- required IWAD: `Schedule I\Mods\SchedualDoom2Tv\WAD\Doom2.WAD`
- version: `1.0.0`

The branch builds one standalone TV application and one install package.

## Build

```powershell
dotnet build ScheduleIDoom2TV.csproj -c Release
```

The redistributable package does not include the commercial IWAD. Supply a
legally owned copy of `Doom2.WAD` at the path above.
