using System.IO;
using MelonLoader.Utils;

namespace ScheduleIDoom2TV;

internal static class DoomPaths
{
    internal static string ModRoot => Path.Combine(MelonEnvironment.ModsDirectory, DoomEdition.ModDirectoryName);
    internal static string WadDirectory => Path.Combine(ModRoot, "WAD");
    internal static string WadPath => Path.Combine(WadDirectory, DoomEdition.WadFileName);
    internal static string RuntimeDirectory => Path.Combine(ModRoot, "Runtime");
    internal static string RuntimePath => Path.Combine(RuntimeDirectory, "doomgeneric_s1.dll");

    internal static void EnsureDirectories()
    {
        Directory.CreateDirectory(ModRoot);
        Directory.CreateDirectory(WadDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }
}
