# Schedule I Doom TV

A MelonLoader IL2CPP mod project for adding DOOM as a native-style TV app in Schedule I.

## Current milestone

The first CI build is intentionally a minimal, genuine MelonLoader DLL. Its only job is to prove that `ScheduleIDoomTV.dll` loads cleanly with normal compiler-generated `MelonInfo` metadata. Once that is confirmed, the S1API TV app and Doom runtime layers are enabled incrementally.

Target environment:

- Schedule I IL2CPP
- MelonLoader 0.7.3+
- .NET 6
- Current ifBars/S1API for the TV app implementation
