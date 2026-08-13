using System.IO;
using MelonLoader.Utils;

namespace ScheduleIDoomTV;

internal static class DoomPaths
{
    internal static string ModRoot => Path.Combine(MelonEnvironment.ModsDirectory, "SchedualDoomTv");
    internal static string WadDirectory => Path.Combine(ModRoot, "WAD");
    internal static string WadPath => Path.Combine(WadDirectory, "Doom1.WAD");
    internal static string RuntimeDirectory => Path.Combine(ModRoot, "Runtime");
    internal static string RuntimePath => Path.Combine(RuntimeDirectory, "doomgeneric_s1.dll");

    internal static void EnsureDirectories()
    {
        Directory.CreateDirectory(ModRoot);
        Directory.CreateDirectory(WadDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }
}
