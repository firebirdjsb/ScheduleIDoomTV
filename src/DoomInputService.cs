using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScheduleIDoom3TV;

internal static class DoomInputService
{
    internal enum SelectionAction
    {
        None,
        Previous,
        Next,
        Confirm
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private sealed class Binding
    {
        internal readonly byte DoomKey;
        internal readonly int[] VirtualKeys;
        internal bool Down;

        internal Binding(byte doomKey, params int[] virtualKeys)
        {
            DoomKey = doomKey;
            VirtualKeys = virtualKeys;
        }
    }

    private const byte KeyLeft = 0xac;
    private const byte KeyUp = 0xad;
    private const byte KeyRight = 0xae;
    private const byte KeyDown = 0xaf;
    private const byte KeyUse = 0xa2;
    private const byte KeyFire = 0xa3;
    private const byte KeyRun = 0xb6;
    private const byte KeyEscape = 27;
    private const byte KeyEnter = 13;
    private const byte KeyTab = 9;

    private static readonly List<Binding> Bindings = new()
    {
        new(KeyUp, 0x26, 0x57),             // Up / W
        new(KeyDown, 0x28, 0x53),           // Down / S
        new(KeyLeft, 0x25, 0x41),           // Left / A
        new(KeyRight, 0x27, 0x44),          // Right / D
        new(KeyFire, 0x11, 0x01),           // Ctrl / left mouse
        new(KeyUse, 0x20, 0x45),            // Space / E
        new(KeyRun, 0x10),                  // Shift
        new(KeyEscape, 0x51),               // Q opens/closes Doom's own menu; Esc exits TV app
        new(KeyEnter, 0x0D),
        new(KeyTab, 0x09),
        new((byte)'1', 0x31),
        new((byte)'2', 0x32),
        new((byte)'3', 0x33),
        new((byte)'4', 0x34),
        new((byte)'5', 0x35),
        new((byte)'6', 0x36),
        new((byte)'7', 0x37)
    };

    private static bool _selectionPreviousDown;
    private static bool _selectionNextDown;
    private static bool _selectionConfirmDown;
    private static readonly int[] SelectionPreviousKeys = { 0x26, 0x57 };
    private static readonly int[] SelectionNextKeys = { 0x28, 0x53 };
    private static readonly int[] SelectionConfirmKeys = { 0x0D };

    internal static void BeginSelection()
    {
        _selectionPreviousDown = IsAnyKeyDown(SelectionPreviousKeys);
        _selectionNextDown = IsAnyKeyDown(SelectionNextKeys);
        _selectionConfirmDown = IsAnyKeyDown(SelectionConfirmKeys);
    }

    internal static SelectionAction PollSelection()
    {
        bool previous = IsAnyKeyDown(SelectionPreviousKeys);
        bool next = IsAnyKeyDown(SelectionNextKeys);
        bool confirm = IsAnyKeyDown(SelectionConfirmKeys);

        SelectionAction action = SelectionAction.None;
        if (confirm && !_selectionConfirmDown)
            action = SelectionAction.Confirm;
        else if (previous && !_selectionPreviousDown)
            action = SelectionAction.Previous;
        else if (next && !_selectionNextDown)
            action = SelectionAction.Next;

        _selectionPreviousDown = previous;
        _selectionNextDown = next;
        _selectionConfirmDown = confirm;
        return action;
    }

    internal static void SynchronizeGameBindings()
    {
        foreach (Binding binding in Bindings)
            binding.Down = IsAnyKeyDown(binding.VirtualKeys);
    }

    internal static void Update(DoomNativeRuntime runtime)
    {
        foreach (Binding binding in Bindings)
        {
            bool down = false;
            foreach (int key in binding.VirtualKeys)
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                {
                    down = true;
                    break;
                }
            }

            if (down == binding.Down)
                continue;

            binding.Down = down;
            runtime.SendKey(down, binding.DoomKey);
        }
    }

    internal static void ReleaseAll(DoomNativeRuntime runtime)
    {
        foreach (Binding binding in Bindings)
        {
            if (!binding.Down)
                continue;
            binding.Down = false;
            runtime.SendKey(false, binding.DoomKey);
        }
    }

    private static bool IsAnyKeyDown(params int[] virtualKeys)
    {
        foreach (int key in virtualKeys)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
                return true;
        }

        return false;
    }
}
