namespace ScheduleIDoomTV;

internal static class DoomEdition
{
#if DOOM2_BUILD
    internal const string GameTitle = "DOOM II";
    internal const string GameLogName = "Doom II";
    internal const string MelonName = "Schedule I - Doom II TV";
    internal const string ModVersion = "1.0.0";
    internal const string AppId = "ScheduleIDoom2TV.Doom2";
    internal const string ModDirectoryName = "SchedualDoom2Tv";
    internal const string WadFileName = "Doom2.WAD";
    internal const string HarmonyId = "com.firebirdjsb.scheduleidoom2tv";
    internal const string FramebufferName = "Doom2Framebuffer";
#else
    internal const string GameTitle = "DOOM";
    internal const string GameLogName = "Doom";
    internal const string MelonName = "Schedule I - Doom TV";
    internal const string ModVersion = "0.3.6-alpha";
    internal const string AppId = "ScheduleIDoomTV.Doom";
    internal const string ModDirectoryName = "SchedualDoomTv";
    internal const string WadFileName = "Doom1.WAD";
    internal const string HarmonyId = "com.firebirdjsb.scheduleidoomtv";
    internal const string FramebufferName = "DoomFramebuffer";
#endif
}
