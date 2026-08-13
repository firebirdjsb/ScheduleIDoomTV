using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;

namespace ScheduleIDoom3TV;

internal sealed class DoomNativeRuntime : IDisposable
{
    internal const int Width = 640;
    internal const int Height = 400;
    internal const int FrameBytes = Width * Height * 4;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateDelegate([MarshalAs(UnmanagedType.LPStr)] string wadPath);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TickDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyDelegate(int pressed, byte key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopyFrameDelegate(IntPtr rgba, int capacity, out int width, out int height, out int frameNumber);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PauseDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ResumeDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IsInitializedDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint LastExceptionDelegate();

    private IntPtr _library;
    private CreateDelegate? _create;
    private TickDelegate? _tick;
    private KeyDelegate? _key;
    private CopyFrameDelegate? _copyFrame;
    private PauseDelegate? _pause;
    private ResumeDelegate? _resume;
    private IsInitializedDelegate? _isInitialized;
    private LastExceptionDelegate? _lastException;
    private readonly byte[] _frame = new byte[FrameBytes];
    private GCHandle _frameHandle;
    private int _lastFrameNumber = -1;

    internal bool IsLoaded => _library != IntPtr.Zero;
    internal bool IsRunning { get; private set; }
    internal byte[] Frame => _frame;
    internal int LastCapturedFrameNumber => _lastFrameNumber;
    internal string? LastError { get; private set; }

    internal bool Start()
    {
        LastError = null;
        DoomPaths.EnsureDirectories();

        if (!File.Exists(DoomPaths.WadPath))
            return Fail($"Doom WAD not found: {DoomPaths.WadPath}");

        if (!DoomWadCompatibility.TryPrepare(
                DoomPaths.WadPath,
                DoomPaths.CompatibleWadPath,
                out string runtimeWadPath,
                out string wadDescription,
                out string wadError))
            return Fail($"Doom 3 IWAD preparation failed: {wadError}");

        MelonLogger.Msg($"Doom TV: validated {DoomEdition.WadFileName} ({wadDescription}).");

        if (!File.Exists(DoomPaths.RuntimePath))
            return Fail($"Doom runtime not found: {DoomPaths.RuntimePath}");

        try
        {
            EnsureLoaded();

            if (_isInitialized!() != 0)
            {
                _resume!();
                IsRunning = true;
                MelonLogger.Msg("Doom TV: resumed existing DOOM session.");
                return true;
            }

            int result = _create!(runtimeWadPath);
            if (result <= 0)
                return Fail(DescribeNativeFailure("create", result));

            IsRunning = true;
            MelonLogger.Msg($"Doom TV: DOOM started from {runtimeWadPath}");
            return true;
        }
        catch (Exception ex)
        {
            return Fail($"Failed to start native DOOM runtime: {ex}");
        }
    }

    internal bool TickAndCapture()
    {
        if (!IsRunning || _tick == null || _copyFrame == null)
            return false;

        int tickResult;
        try
        {
            tickResult = _tick();
        }
        catch (Exception ex)
        {
            Fail($"Native DOOM tick invocation failed: {ex}");
            IsRunning = false;
            return false;
        }

        if (tickResult <= 0)
        {
            if (tickResult < 0)
                Fail(DescribeNativeFailure("tick", tickResult));
            IsRunning = false;
            return false;
        }

        try
        {
            int width;
            int height;
            int frameNumber;
            int copied = _copyFrame(_frameHandle.AddrOfPinnedObject(), _frame.Length, out width, out height, out frameNumber);
            if (copied < 0)
            {
                Fail(DescribeNativeFailure("frame copy", copied));
                IsRunning = false;
                return false;
            }

            if (copied != FrameBytes || width != Width || height != Height)
                return false;

            if (frameNumber == _lastFrameNumber)
                return false;

            _lastFrameNumber = frameNumber;
            return true;
        }
        catch (Exception ex)
        {
            Fail($"Native DOOM frame capture failed: {ex}");
            IsRunning = false;
            return false;
        }
    }

    internal void SendKey(bool pressed, byte doomKey)
    {
        if (IsRunning)
            _key?.Invoke(pressed ? 1 : 0, doomKey);
    }

    internal void Pause()
    {
        if (!IsLoaded)
            return;

        DoomInputService.ReleaseAll(this);
        _pause?.Invoke();
        IsRunning = false;
    }

    private void EnsureLoaded()
    {
        if (_library != IntPtr.Zero)
            return;

        _library = System.Runtime.InteropServices.NativeLibrary.Load(DoomPaths.RuntimePath);
        _create = Load<CreateDelegate>("s1doom_create");
        _tick = Load<TickDelegate>("s1doom_tick");
        _key = Load<KeyDelegate>("s1doom_key");
        _copyFrame = Load<CopyFrameDelegate>("s1doom_copy_frame");
        _pause = Load<PauseDelegate>("s1doom_pause");
        _resume = Load<ResumeDelegate>("s1doom_resume");
        _isInitialized = Load<IsInitializedDelegate>("s1doom_is_initialized");
        _lastException = Load<LastExceptionDelegate>("s1doom_last_exception");
        _frameHandle = GCHandle.Alloc(_frame, GCHandleType.Pinned);
    }

    private T Load<T>(string export) where T : Delegate
    {
        IntPtr address = System.Runtime.InteropServices.NativeLibrary.GetExport(_library, export);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private string DescribeNativeFailure(string operation, int result)
    {
        uint code = 0;
        try { code = _lastException?.Invoke() ?? 0; } catch { }

        return code != 0
            ? $"Native DOOM {operation} failed (result {result}, SEH 0x{code:X8})."
            : $"Native DOOM {operation} failed (result {result}).";
    }

    private bool Fail(string message)
    {
        LastError = message;
        MelonLogger.Error($"Doom TV: {message}");
        return false;
    }

    public void Dispose()
    {
        Pause();
        if (_frameHandle.IsAllocated)
            _frameHandle.Free();
        if (_library != IntPtr.Zero)
        {
            System.Runtime.InteropServices.NativeLibrary.Free(_library);
            _library = IntPtr.Zero;
        }
    }
}
