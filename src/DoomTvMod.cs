using MelonLoader;

namespace ScheduleIDoomTV;

public sealed class DoomTvMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Schedule I - Doom TV 0.2.1-alpha loaded successfully.");
        MelonLogger.Msg("Load-test milestone: compiler-generated MelonInfo metadata is valid.");
    }
}
