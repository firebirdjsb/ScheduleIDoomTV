using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace ScheduleIDoomTV;

/// <summary>
/// Gives the TV DOOM app exclusive ownership of gameplay input and presentation
/// while it is open. Uses reflection so the mod does not need to ship or compile
/// directly against Assembly-CSharp game types.
/// </summary>
internal static class DoomInputOwnershipService
{
    private static Type? _movementType;
    private static PropertyInfo? _movementInstanceProperty;
    private static FieldInfo? _movementInstanceField;
    private static PropertyInfo? _canMoveProperty;
    private static PropertyInfo? _canJumpProperty;

    private static Type? _inventoryType;
    private static PropertyInfo? _inventoryInstanceProperty;
    private static FieldInfo? _inventoryInstanceField;
    private static PropertyInfo? _hotbarEnabledProperty;
    private static FieldInfo? _hotbarEnabledField;

    private static Type? _hudType;
    private static PropertyInfo? _hudInstanceProperty;
    private static FieldInfo? _hudInstanceField;
    private static FieldInfo? _hudCanvasField;

    private static object? _movement;
    private static object? _inventory;
    private static object? _hud;
    private static Canvas? _hudCanvas;

    private static bool _locked;
    private static bool _movementCaptured;
    private static bool _inventoryCaptured;
    private static bool _hudCaptured;
    private static bool _previousCanMove = true;
    private static bool _previousCanJump = true;
    private static bool _previousHotbarEnabled = true;
    private static bool _previousHudCanvasEnabled = true;
    private static bool _loggedResolutionFailure;
    private static bool _loggedApplyFailure;

    internal static void Acquire()
    {
        _locked = true;
        Resolve();
        CaptureState();
        ApplyLock();
    }

    internal static void Maintain()
    {
        if (!_locked)
            return;

        Resolve();
        CaptureState();
        ApplyLock();
    }

    internal static void Release()
    {
        if (!_locked && !_movementCaptured && !_inventoryCaptured && !_hudCaptured)
            return;

        _locked = false;

        Resolve();

        try
        {
            object? movement = GetMovementInstance();
            if (movement != null && _movementCaptured)
            {
                _canMoveProperty?.SetValue(movement, _previousCanMove);
                _canJumpProperty?.SetValue(movement, _previousCanJump);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not restore Schedule I movement state: {ex.Message}");
        }

        try
        {
            object? inventory = GetInventoryInstance();
            if (inventory != null && _inventoryCaptured)
                SetHotbarEnabled(inventory, _previousHotbarEnabled);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not restore Schedule I hotbar state: {ex.Message}");
        }

        try
        {
            Canvas? hudCanvas = GetHudCanvas();
            if (hudCanvas != null && _hudCaptured)
                hudCanvas.enabled = _previousHudCanvasEnabled;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not restore Schedule I HUD state: {ex.Message}");
        }

        _movementCaptured = false;
        _inventoryCaptured = false;
        _hudCaptured = false;
        _movement = null;
        _inventory = null;
        _hud = null;
        _hudCanvas = null;
        _loggedApplyFailure = false;
    }

    private static void Resolve()
    {
        Assembly? gameAssembly = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
            {
                gameAssembly = assembly;
                break;
            }
        }

        if (gameAssembly == null)
        {
            LogResolutionFailure("Assembly-CSharp is not loaded yet");
            return;
        }

        const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        if (_movementType == null)
        {
            _movementType = gameAssembly.GetType("Il2CppScheduleOne.PlayerScripts.PlayerMovement", false)
                            ?? gameAssembly.GetType("ScheduleOne.PlayerScripts.PlayerMovement", false);
            if (_movementType != null)
            {
                _movementInstanceProperty = _movementType.GetProperty("Instance", staticFlags);
                _movementInstanceField = _movementType.GetField("Instance", staticFlags);
                _canMoveProperty = _movementType.GetProperty("CanMove", instanceFlags);
                _canJumpProperty = _movementType.GetProperty("CanJump", instanceFlags);
            }
        }

        if (_inventoryType == null)
        {
            _inventoryType = gameAssembly.GetType("Il2CppScheduleOne.PlayerScripts.PlayerInventory", false)
                             ?? gameAssembly.GetType("ScheduleOne.PlayerScripts.PlayerInventory", false);
            if (_inventoryType != null)
            {
                _inventoryInstanceProperty = _inventoryType.GetProperty("Instance", staticFlags);
                _inventoryInstanceField = _inventoryType.GetField("Instance", staticFlags);
                _hotbarEnabledProperty = _inventoryType.GetProperty("HotbarEnabled", instanceFlags);
                _hotbarEnabledField = _inventoryType.GetField("<HotbarEnabled>k__BackingField", instanceFlags);
            }
        }

        if (_hudType == null)
        {
            _hudType = gameAssembly.GetType("Il2CppScheduleOne.UI.HUD", false)
                       ?? gameAssembly.GetType("ScheduleOne.UI.HUD", false);
            if (_hudType != null)
            {
                _hudInstanceProperty = _hudType.GetProperty("Instance", staticFlags);
                _hudInstanceField = _hudType.GetField("Instance", staticFlags);
                _hudCanvasField = _hudType.GetField("canvas", instanceFlags);
            }
        }

        if (_movementType == null || _inventoryType == null || _hudType == null)
            LogResolutionFailure("one or more player input/HUD types were not found");
    }

    private static object? GetMovementInstance()
    {
        if (_movement != null)
            return _movement;

        try
        {
            _movement = _movementInstanceProperty?.GetValue(null) ?? _movementInstanceField?.GetValue(null);
        }
        catch
        {
            _movement = null;
        }

        return _movement;
    }

    private static object? GetInventoryInstance()
    {
        if (_inventory != null)
            return _inventory;

        try
        {
            _inventory = _inventoryInstanceProperty?.GetValue(null) ?? _inventoryInstanceField?.GetValue(null);
        }
        catch
        {
            _inventory = null;
        }

        return _inventory;
    }

    private static object? GetHudInstance()
    {
        if (_hud != null)
            return _hud;

        try
        {
            _hud = _hudInstanceProperty?.GetValue(null) ?? _hudInstanceField?.GetValue(null);
        }
        catch
        {
            _hud = null;
        }

        return _hud;
    }

    private static Canvas? GetHudCanvas()
    {
        if (_hudCanvas != null)
            return _hudCanvas;

        try
        {
            object? hud = GetHudInstance();
            if (hud != null)
                _hudCanvas = _hudCanvasField?.GetValue(hud) as Canvas;
        }
        catch
        {
            _hudCanvas = null;
        }

        return _hudCanvas;
    }

    private static void CaptureState()
    {
        if (!_movementCaptured)
        {
            try
            {
                object? movement = GetMovementInstance();
                if (movement != null)
                {
                    if (_canMoveProperty?.GetValue(movement) is bool canMove)
                        _previousCanMove = canMove;
                    if (_canJumpProperty?.GetValue(movement) is bool canJump)
                        _previousCanJump = canJump;
                    _movementCaptured = true;
                }
            }
            catch (Exception ex)
            {
                LogApplyFailure($"could not capture movement state: {ex.Message}");
            }
        }

        if (!_inventoryCaptured)
        {
            try
            {
                object? inventory = GetInventoryInstance();
                if (inventory != null && TryGetHotbarEnabled(inventory, out bool hotbarEnabled))
                {
                    _previousHotbarEnabled = hotbarEnabled;
                    _inventoryCaptured = true;
                }
            }
            catch (Exception ex)
            {
                LogApplyFailure($"could not capture hotbar state: {ex.Message}");
            }
        }

        if (!_hudCaptured)
        {
            try
            {
                Canvas? hudCanvas = GetHudCanvas();
                if (hudCanvas != null)
                {
                    _previousHudCanvasEnabled = hudCanvas.enabled;
                    _hudCaptured = true;
                }
            }
            catch (Exception ex)
            {
                LogApplyFailure($"could not capture HUD state: {ex.Message}");
            }
        }
    }

    private static void ApplyLock()
    {
        try
        {
            object? movement = GetMovementInstance();
            if (movement != null)
            {
                _canMoveProperty?.SetValue(movement, false);
                _canJumpProperty?.SetValue(movement, false);
            }

            object? inventory = GetInventoryInstance();
            if (inventory != null)
                SetHotbarEnabled(inventory, false);

            Canvas? hudCanvas = GetHudCanvas();
            if (hudCanvas != null)
                hudCanvas.enabled = false;
        }
        catch (Exception ex)
        {
            LogApplyFailure($"failed to lock Schedule I input/HUD: {ex.Message}");
        }
    }

    private static bool TryGetHotbarEnabled(object inventory, out bool enabled)
    {
        object? value = _hotbarEnabledProperty?.GetValue(inventory) ?? _hotbarEnabledField?.GetValue(inventory);
        if (value is bool hotbarEnabled)
        {
            enabled = hotbarEnabled;
            return true;
        }

        enabled = true;
        return false;
    }

    private static void SetHotbarEnabled(object inventory, bool enabled)
    {
        MethodInfo? setter = _hotbarEnabledProperty?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(inventory, new object[] { enabled });
            return;
        }

        _hotbarEnabledField?.SetValue(inventory, enabled);
    }

    private static void LogResolutionFailure(string detail)
    {
        if (_loggedResolutionFailure)
            return;

        _loggedResolutionFailure = true;
        MelonLogger.Warning($"Doom TV: {detail}; some Schedule I input/HUD controls may remain active.");
    }

    private static void LogApplyFailure(string detail)
    {
        if (_loggedApplyFailure)
            return;

        _loggedApplyFailure = true;
        MelonLogger.Warning($"Doom TV: {detail}");
    }
}
