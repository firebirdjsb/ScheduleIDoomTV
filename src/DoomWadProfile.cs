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
        DoomEdition.AppId,
        DoomEdition.GameTitle,
        DoomEdition.WadFileName,
        "Doom3.compat.WAD",
        alwaysRegister: true);

    internal static readonly DoomWadProfile Tnt = new(
        DoomWadFlavor.Tnt,
        DoomEdition.AppId + ".Tnt",
        "DOOM 3: TNT",
        "Tnt.wad",
        "tnt.wad",
        alwaysRegister: false);

    internal static readonly DoomWadProfile Plutonia = new(
        DoomWadFlavor.Plutonia,
        DoomEdition.AppId + ".Plutonia",
        "DOOM 3: PLUTONIA",
        "Plutonia.wad",
        "plutonia.wad",
        alwaysRegister: false);

    internal static readonly IReadOnlyList<DoomWadProfile> All =
        new[] { Doom3, Tnt, Plutonia };

    private DoomWadProfile(
        DoomWadFlavor flavor,
        string appId,
        string title,
        string fileName,
        string runtimeFileName,
        bool alwaysRegister)
    {
        Flavor = flavor;
        AppId = appId;
        Title = title;
        FileName = fileName;
        RuntimeFileName = runtimeFileName;
        AlwaysRegister = alwaysRegister;
    }

    internal DoomWadFlavor Flavor { get; }
    internal string AppId { get; }
    internal string Title { get; }
    internal string FileName { get; }
    internal string RuntimeFileName { get; }
    internal bool AlwaysRegister { get; }
    internal string WadPath => DoomPaths.GetWadPath(this);
    internal string RuntimeWadPath => DoomPaths.GetRuntimeWadPath(this);
    internal bool IsInstalled => File.Exists(WadPath);
    internal bool ShouldRegister => AlwaysRegister || IsInstalled;
}
