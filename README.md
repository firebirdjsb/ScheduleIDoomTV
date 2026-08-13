# Schedule I Doom TV

A MelonLoader IL2CPP mod project for adding DOOM as a native-style TV app in Schedule I.

## Current milestone

The first CI build is intentionally a minimal, genuine MelonLoader DLL. Its only job is to prove that `ScheduleIDoomTV.dll` loads cleanly with normal compiler-generated `MelonInfo` metadata. Once that is confirmed, the S1API TV app and Doom runtime layers are enabled incrementally.

Target environment:

- Schedule I IL2CPP
- MelonLoader 0.7.3+
- .NET 6
- Current ifBars/S1API for the TV app implementation

## DOOM II edition

`ScheduleIDoom2TV.csproj` builds a TV-only `ScheduleIDoom2TV.dll` for DOOM II.
It uses an independent Melon identity, TV app ID, Harmony ID, install directory,
`Doom2.WAD` path, and red/gold pixel-art icon. No arcade code or assets are
compiled into the DOOM II DLL.

Build it with:

```powershell
dotnet build ScheduleIDoom2TV.csproj -c Release
```

Place a legally owned IWAD at:

```text
Schedule I\Mods\SchedualDoom2Tv\WAD\Doom2.WAD
```
