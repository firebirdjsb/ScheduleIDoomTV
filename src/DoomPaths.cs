using System.IO;
using MelonLoader.Utils;

namespace ScheduleIDoom3TV;

internal static class DoomPaths
{
    internal static string ModRoot => Path.Combine(MelonEnvironment.ModsDirectory, DoomEdition.ModDirectoryName);
    internal static string WadDirectory => Path.Combine(ModRoot, "WAD");
    internal static string WadPath => Path.Combine(WadDirectory, DoomEdition.WadFileName);
    internal static string RuntimeDirectory => Path.Combine(ModRoot, "Runtime");
    internal static string RuntimePath => Path.Combine(RuntimeDirectory, "doomgeneric_s1.dll");
    internal static string CompatibleWadPath => Path.Combine(RuntimeDirectory, "Doom3.compat.WAD");

    internal static void EnsureDirectories()
    {
        Directory.CreateDirectory(ModRoot);
        Directory.CreateDirectory(WadDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }
}
