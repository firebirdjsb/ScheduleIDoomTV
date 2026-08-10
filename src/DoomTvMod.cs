using MelonLoader;

namespace ScheduleIDoomTV;

public sealed class DoomTvMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Schedule I - Doom TV 0.2.2-alpha loaded successfully.");
        MelonLogger.Msg("TV app milestone enabled: S1API should register a DOOM tile on the television home screen.");
    }
}
