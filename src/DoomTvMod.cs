using HarmonyLib;
using MelonLoader;

namespace ScheduleIDoomTV;

public sealed class DoomTvMod : MelonMod
{
    private Harmony? _harmony;

    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Schedule I - Doom TV 0.2.3-alpha loaded successfully.");

        _harmony = new Harmony("com.firebirdjsb.scheduleidoomtv");
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
            // MelonLoader is already shutting down; nothing useful remains to do.
        }
    }
}
