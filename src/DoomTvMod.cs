using MelonLoader;

namespace ScheduleIDoom3TV;

public sealed class DoomTvMod : MelonMod
{
    private global::HarmonyLib.Harmony? _harmony;

    public override void OnInitializeMelon()
    {
        DoomPaths.EnsureDirectories();
        MelonLogger.Msg($"{DoomEdition.MelonName} {DoomEdition.ModVersion} loaded successfully.");
        foreach (DoomWadProfile profile in DoomWadProfile.All)
            MelonLogger.Msg($"{DoomEdition.GameLogName} TV: {profile.Title} WAD path: {profile.WadPath}");
        MelonLogger.Msg($"Doom TV: looking for native runtime at {DoomPaths.RuntimePath}");

        _harmony = new global::HarmonyLib.Harmony(DoomEdition.HarmonyId);
        if (DoomTvRegistrationPatch.Install(_harmony))
            MelonLogger.Msg("Doom TV: forced TV-home registration patch installed.");
        else
            MelonLogger.Error("Doom TV: TV-home registration patch failed to install.");
    }

    public override void OnUpdate()
    {
        DoomTvApp.PumpActiveFromMelon();
    }

    public override void OnDeinitializeMelon()
    {
        DoomInputOwnershipService.Release();
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch
        {
        }
    }
}
