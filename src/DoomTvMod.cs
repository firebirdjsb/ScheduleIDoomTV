using MelonLoader;

namespace ScheduleIDoomTV;

public sealed class DoomTvMod : MelonMod
{
    private global::HarmonyLib.Harmony? _harmony;

    public override void OnInitializeMelon()
    {
        DoomPaths.EnsureDirectories();
        MelonLogger.Msg("Schedule I - Doom TV 0.3.4-alpha loaded successfully.");
        MelonLogger.Msg($"Doom TV: looking for Doom1.WAD at {DoomPaths.WadPath}");
        MelonLogger.Msg($"Doom TV: looking for native runtime at {DoomPaths.RuntimePath}");

        _harmony = new global::HarmonyLib.Harmony("com.firebirdjsb.scheduleidoomtv");
        if (DoomTvRegistrationPatch.Install(_harmony))
            MelonLogger.Msg("Doom TV: forced TV-home registration patch installed.");
        else
            MelonLogger.Error("Doom TV: TV-home registration patch failed to install.");
    }

    public override void OnDeinitializeMelon()
    {
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch
        {
        }
    }
}
