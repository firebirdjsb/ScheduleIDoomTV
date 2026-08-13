using System.Collections.Generic;
using System.IO;

namespace ScheduleIDoom3TV;

internal enum DoomWadFlavor
{
    Doom3,
    Tnt,
    Plutonia
}

internal sealed class DoomWadProfile
{
    internal static readonly DoomWadProfile Doom3 = new(
        DoomWadFlavor.Doom3,
        DoomEdition.GameTitle,
        DoomEdition.WadFileName,
        "Doom3.compat.WAD");

    internal static readonly DoomWadProfile Tnt = new(
        DoomWadFlavor.Tnt,
        "DOOM 3: TNT",
        "Tnt.wad",
        "tnt.wad");

    internal static readonly DoomWadProfile Plutonia = new(
        DoomWadFlavor.Plutonia,
        "DOOM 3: PLUTONIA",
        "Plutonia.wad",
        "plutonia.wad");

    internal static readonly IReadOnlyList<DoomWadProfile> All =
        new[] { Doom3, Tnt, Plutonia };

    private DoomWadProfile(
        DoomWadFlavor flavor,
        string title,
        string fileName,
        string runtimeFileName)
    {
        Flavor = flavor;
        Title = title;
        FileName = fileName;
        RuntimeFileName = runtimeFileName;
    }

    internal DoomWadFlavor Flavor { get; }
    internal string Title { get; }
    internal string FileName { get; }
    internal string RuntimeFileName { get; }
    internal string WadPath => DoomPaths.GetWadPath(this);
    internal string RuntimeWadPath => DoomPaths.GetRuntimeWadPath(this);
    internal bool IsInstalled => File.Exists(WadPath);
}
