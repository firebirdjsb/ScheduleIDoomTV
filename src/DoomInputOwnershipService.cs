using System;
using System.Reflection;
using MelonLoader;

namespace ScheduleIDoom3TV;

/// <summary>
/// Keeps Schedule I's local player stationary while the TV DOOM app owns the
/// keyboard. Uses reflection so the mod does not need to ship or compile
/// directly against Assembly-CSharp game types.
/// </summary>
internal static class DoomInputOwnershipService
{
    private static Type? _movementType;
    private static PropertyInfo? _instanceProperty;
    private static FieldInfo? _instanceField;
    private static PropertyInfo? _canMoveProperty;
    private static PropertyInfo? _canJumpProperty;

    private static object? _movement;
    private static bool _locked;
    private static bool _captured;
    private static bool _previousCanMove = true;
    private static bool _previousCanJump = true;
    private static bool _loggedResolutionFailure;

    internal static void Acquire()
    {
        _locked = true;
        Resolve();
        CaptureState();
        ApplyLock();
    }

    internal static void Maintain()
    {
        if (_locked)
            ApplyLock();
    }

    internal static void Release()
    {
        if (!_locked && !_captured)
            return;

        _locked = false;

        try
        {
            Resolve();
            object? movement = GetMovementInstance();
            if (movement != null && _captured)
            {
                _canMoveProperty?.SetValue(movement, _previousCanMove);
                _canJumpProperty?.SetValue(movement, _previousCanJump);
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not restore Schedule I movement state: {ex.Message}");
        }
        finally
        {
            _captured = false;
            _movement = null;
        }
    }

    private static void Resolve()
    {
        if (_movementType != null)
            return;

        Assembly? gameAssembly = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
            {
                gameAssembly = assembly;
                break;
            }
        }

        _movementType = gameAssembly?.GetType("Il2CppScheduleOne.PlayerScripts.PlayerMovement", false)
                        ?? gameAssembly?.GetType("ScheduleOne.PlayerScripts.PlayerMovement", false);

        if (_movementType == null)
        {
            if (!_loggedResolutionFailure)
            {
                _loggedResolutionFailure = true;
                MelonLogger.Warning("Doom TV: PlayerMovement type not found; gameplay movement cannot be locked yet.");
            }
            return;
        }

        const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        _instanceProperty = _movementType.GetProperty("Instance", staticFlags);
        _instanceField = _movementType.GetField("Instance", staticFlags);
        _canMoveProperty = _movementType.GetProperty("CanMove", instanceFlags);
        _canJumpProperty = _movementType.GetProperty("CanJump", instanceFlags);
    }

    private static object? GetMovementInstance()
    {
        if (_movement != null)
            return _movement;

        try
        {
            _movement = _instanceProperty?.GetValue(null) ?? _instanceField?.GetValue(null);
        }
        catch
        {
            _movement = null;
        }

        return _movement;
    }

    private static void CaptureState()
    {
        if (_captured)
            return;

        try
        {
            object? movement = GetMovementInstance();
            if (movement == null)
                return;

            if (_canMoveProperty?.GetValue(movement) is bool canMove)
                _previousCanMove = canMove;
            if (_canJumpProperty?.GetValue(movement) is bool canJump)
                _previousCanJump = canJump;

            _captured = true;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not capture Schedule I movement state: {ex.Message}");
        }
    }

    private static void ApplyLock()
    {
        try
        {
            object? movement = GetMovementInstance();
            if (movement == null)
                return;

            _canMoveProperty?.SetValue(movement, false);
            _canJumpProperty?.SetValue(movement, false);
        }
        catch (Exception ex)
        {
            if (!_loggedResolutionFailure)
            {
                _loggedResolutionFailure = true;
                MelonLogger.Warning($"Doom TV: failed to lock Schedule I movement: {ex.Message}");
            }
        }
    }
}
