using System.Collections;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace ScheduleIDoomTV;

/// <summary>
/// S1API's TVApp auto-discovery is not finding external TVApp subclasses on the
/// current IL2CPP build. Instead of cloning Schedule I's UI ourselves, hook the
/// real TVHomeScreen.Open method and ask S1API's TVApp base class to create the
/// UI and native app button directly.
/// </summary>
internal static class DoomTvRegistrationPatch
{
    private static DoomTvApp? _app;
    private static object? _homeScreen;
    private static MethodInfo? _spawnUi;
    private static MethodInfo? _spawnButton;
    private static MethodInfo? _registryRegister;
    private static FieldInfo? _registeredApps;

    internal static bool Install(Harmony harmony)
    {
        Type? homeType = AccessTools.TypeByName("Il2CppScheduleOne.TV.TVHomeScreen")
                         ?? AccessTools.TypeByName("ScheduleOne.TV.TVHomeScreen");

        if (homeType == null)
        {
            MelonLogger.Error("Doom TV: could not find Schedule One TVHomeScreen type.");
            return false;
        }

        MethodInfo? open = AccessTools.Method(homeType, "Open");
        if (open == null)
        {
            MelonLogger.Error($"Doom TV: found {homeType.FullName}, but its Open method was not found.");
            return false;
        }

        MethodInfo postfix = AccessTools.Method(typeof(DoomTvRegistrationPatch), nameof(OnHomeScreenOpened));
        harmony.Patch(open, postfix: new HarmonyMethod(postfix));

        CacheS1ApiMethods();
        MelonLogger.Msg($"Doom TV: patched {homeType.FullName}.Open for forced TV-app registration.");
        return true;
    }

    private static void CacheS1ApiMethods()
    {
        Type appBase = typeof(S1API.TVApp.TVApp);
        _spawnUi = appBase.GetMethod("SpawnUI", BindingFlags.Instance | BindingFlags.NonPublic);
        _spawnButton = appBase.GetMethod("SpawnButton", BindingFlags.Instance | BindingFlags.NonPublic);

        Assembly s1api = appBase.Assembly;
        Type? registry = s1api.GetType("S1API.Internal.Patches.TVAppRegistry", throwOnError: false);
        if (registry != null)
        {
            _registryRegister = registry.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _registeredApps = registry.GetField("RegisteredApps", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    private static void OnHomeScreenOpened(object __instance)
    {
        try
        {
            if (__instance == null)
                return;

            // Scene reload or a different TVHomeScreen instance: create a fresh app instance.
            if (!ReferenceEquals(_homeScreen, __instance))
            {
                _homeScreen = __instance;
                _app = null;
            }

            if (RegistryAlreadyContainsDoomApp())
            {
                MelonLogger.Msg("Doom TV: S1API registry already contains the DOOM app.");
                return;
            }

            if (_app != null)
                return;

            CacheS1ApiMethods();
            if (_spawnUi == null || _spawnButton == null)
            {
                MelonLogger.Error("Doom TV: current S1API TVApp no longer exposes SpawnUI/SpawnButton; cannot inject tile.");
                return;
            }

            _app = new DoomTvApp();

            // Register first so S1API's home-screen cleanup knows about the app.
            _registryRegister?.Invoke(null, new object?[] { _app });

            _spawnUi.Invoke(_app, new[] { __instance });
            _spawnButton.Invoke(_app, new[] { __instance });

            MelonLogger.Msg("Doom TV: forced DOOM tile registration completed.");
        }
        catch (TargetInvocationException ex)
        {
            MelonLogger.Error($"Doom TV: TV-app injection failed: {ex.InnerException ?? ex}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Doom TV: TV-app injection failed: {ex}");
        }
    }

    private static bool RegistryAlreadyContainsDoomApp()
    {
        if (_registeredApps?.GetValue(null) is not IEnumerable apps)
            return false;

        foreach (object? app in apps)
        {
            if (app is DoomTvApp)
            {
                _app = (DoomTvApp)app;
                return true;
            }
        }

        return false;
    }
}
