using System;
using System.Threading;
using MelonLoader;
using S1API.TVApp;
using UnityEngine;
using UnityEngine.UI;

namespace ScheduleIDoomTV;

public sealed class DoomTvApp : TVApp
{
    private static DoomTvApp? _active;
    private static int _nextInstanceId;

    private readonly int _instanceId = Interlocked.Increment(ref _nextInstanceId);
    private DoomNativeRuntime? _runtime;
    private Texture2D? _frameTexture;
    private RawImage? _frameImage;
    private byte[]? _diagnosticFrame;
    private bool _loggedFirstPump;
    private bool _loggedFirstFrame;
    private bool _loggedFrameStats;

    protected override string AppName => "ScheduleIDoomTV.Doom";
    protected override string AppTitle => "DOOM";
    protected override Sprite Icon => DoomIconFactory.GetOrCreate()!;

    internal static void PumpActiveFromMelon()
    {
        _active?.PumpFrame();
    }

    protected override void OnCreatedUI(GameObject container)
    {
        try
        {
            GameObject framebuffer = new("DoomFramebuffer");
            framebuffer.layer = container.layer;
            framebuffer.transform.SetParent(container.transform, false);

            RectTransform rect = framebuffer.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _frameImage = framebuffer.AddComponent<RawImage>();
            _frameImage.color = Color.white;

            _frameTexture = new Texture2D(
                DoomNativeRuntime.Width,
                DoomNativeRuntime.Height,
                TextureFormat.RGBA32,
                false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _frameImage.texture = _frameTexture;

            UploadDiagnosticPattern();
            MelonLogger.Msg($"Doom TV[{_instanceId}]: 640x400 framebuffer UI created on TV layer with diagnostic pattern.");
        }
        catch (Exception ex)
        {
            _frameImage = null;
            _frameTexture = null;
            MelonLogger.Error($"Doom TV[{_instanceId}]: framebuffer UI creation failed, but TV app registration will continue: {ex}");
        }
    }

    protected override void OnOpened()
    {
        _runtime ??= new DoomNativeRuntime();
        if (!_runtime.Start())
        {
            MelonLogger.Error($"Doom TV[{_instanceId}]: cannot launch DOOM. Expected WAD: {DoomPaths.WadPath}");
            MelonLogger.Error($"Doom TV[{_instanceId}]: expected runtime: {DoomPaths.RuntimePath}");
            return;
        }

        _active = this;
        _loggedFirstPump = false;
        _loggedFirstFrame = false;
        _loggedFrameStats = false;
        DoomInputOwnershipService.Acquire();

        if (_frameTexture == null)
            MelonLogger.Warning($"Doom TV[{_instanceId}]: native DOOM started, but the TV framebuffer is unavailable.");

        MelonLogger.Msg($"Doom TV[{_instanceId}]: DOOM app opened. Melon main-loop frame pump is active.");
        MelonLogger.Msg($"Doom TV[{_instanceId}]: controls: WASD/arrows move/turn, Ctrl or left mouse fires, E/Space uses, Shift runs, Q opens Doom menu, Esc returns to TV menu.");
    }

    protected override void OnUpdate() { }

    private unsafe void PumpFrame()
    {
        if (_runtime == null || !_runtime.IsRunning)
            return;

        DoomInputOwnershipService.Maintain();
        DoomInputService.Update(_runtime);

        if (!_loggedFirstPump)
        {
            _loggedFirstPump = true;
            MelonLogger.Msg($"Doom TV[{_instanceId}]: first Melon-driven DOOM update reached.");
        }

        if (_frameTexture == null)
            return;

        if (!_runtime.TickAndCapture())
            return;

        byte[] frame = _runtime.Frame;
        FrameStats stats = AnalyzeFrame(frame);

        if (!_loggedFrameStats)
        {
            _loggedFrameStats = true;
            MelonLogger.Msg($"Doom TV[{_instanceId}]: frame {_runtime.LastCapturedFrameNumber} stats: nonBlack={stats.NonBlackPixels}/{DoomNativeRuntime.Width * DoomNativeRuntime.Height}, minRGB={stats.MinRgb}, maxRGB={stats.MaxRgb}.");
        }

        // Keep the visible checkerboard on screen if the native framebuffer is
        // entirely black. This distinguishes a native-render problem from a Unity
        // RawImage/canvas problem in one test run.
        if (stats.NonBlackPixels == 0)
            return;

        fixed (byte* ptr = frame)
        {
            _frameTexture.LoadRawTextureData((IntPtr)ptr, frame.Length);
        }
        _frameTexture.Apply(false, false);

        if (!_loggedFirstFrame)
        {
            _loggedFirstFrame = true;
            MelonLogger.Msg($"Doom TV[{_instanceId}]: first non-black native framebuffer uploaded to TV (frame {_runtime.LastCapturedFrameNumber}).");
        }
    }

    private unsafe void UploadDiagnosticPattern()
    {
        if (_frameTexture == null)
            return;

        _diagnosticFrame ??= new byte[DoomNativeRuntime.FrameBytes];
        byte[] pixels = _diagnosticFrame;

        const int block = 40;
        for (int y = 0; y < DoomNativeRuntime.Height; y++)
        {
            for (int x = 0; x < DoomNativeRuntime.Width; x++)
            {
                bool alternate = ((x / block) + (y / block)) % 2 == 0;
                int i = (y * DoomNativeRuntime.Width + x) * 4;
                pixels[i + 0] = alternate ? (byte)220 : (byte)25;
                pixels[i + 1] = alternate ? (byte)45 : (byte)190;
                pixels[i + 2] = alternate ? (byte)45 : (byte)220;
                pixels[i + 3] = 255;
            }
        }

        fixed (byte* ptr = pixels)
        {
            _frameTexture.LoadRawTextureData((IntPtr)ptr, pixels.Length);
        }
        _frameTexture.Apply(false, false);
    }

    private static FrameStats AnalyzeFrame(byte[] frame)
    {
        int nonBlack = 0;
        int minRgb = 255;
        int maxRgb = 0;

        for (int i = 0; i + 3 < frame.Length; i += 4)
        {
            int r = frame[i + 0];
            int g = frame[i + 1];
            int b = frame[i + 2];
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));

            if ((r | g | b) != 0)
                nonBlack++;
            if (min < minRgb)
                minRgb = min;
            if (max > maxRgb)
                maxRgb = max;
        }

        return new FrameStats(nonBlack, minRgb, maxRgb);
    }

    protected override void OnClosed()
    {
        if (ReferenceEquals(_active, this))
            _active = null;

        DoomInputOwnershipService.Release();
        _runtime?.Pause();
        MelonLogger.Msg($"Doom TV[{_instanceId}]: DOOM paused; Schedule I movement restored; returning to TV home screen.");
    }

    private readonly struct FrameStats
    {
        internal FrameStats(int nonBlackPixels, int minRgb, int maxRgb)
        {
            NonBlackPixels = nonBlackPixels;
            MinRgb = minRgb;
            MaxRgb = maxRgb;
        }

        internal int NonBlackPixels { get; }
        internal int MinRgb { get; }
        internal int MaxRgb { get; }
    }
}
