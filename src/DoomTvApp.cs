using MelonLoader;
using S1API.TVApp;
using UnityEngine;

namespace ScheduleIDoomTV;

public sealed class DoomTvApp : TVApp
{
    protected override string AppName => "ScheduleIDoomTV.Doom";
    protected override string AppTitle => "DOOM";

    // The first integration build intentionally leaves the icon unset. S1API still
    // creates the native TV button and label; a proper DOOM icon comes next.
    protected override Sprite Icon => null!;

    protected override void OnCreatedUI(GameObject container)
    {
        MelonLogger.Msg("DOOM TV app UI container created.");
    }

    protected override void OnOpened()
    {
        MelonLogger.Msg("DOOM TV app opened.");
    }

    protected override void OnClosed()
    {
        MelonLogger.Msg("DOOM TV app closed; returning to TV home screen.");
    }
}
