using System;
using System.Collections.Generic;
using System.Threading;
using MelonLoader;
using S1API.TVApp;
using UnityEngine;
using UnityEngine.UI;

namespace ScheduleIDoomTV;

public sealed class DoomTvApp : TVApp
{
    private const int DiagnosticHoldFrames = 90;

    private static DoomTvApp? _active;
    private static int _nextInstanceId;
    private static readonly object InstancesLock = new();
    private static readonly List<DoomTvApp> Instances = new();

    private readonly int _instanceId = Interlocked.Increment(ref _nextInstanceId);
    private GameObject? _uiContainer;
    private GameObject? _displayRoot;
    private DoomNativeRuntime? _runtime;
    private Texture2D? _frameTexture;
    private RawImage? _frameImage;
    private byte[]? _diagnosticFrame;
    private int _diagnosticFramesRemaining;
    private bool _loggedFirstPump;
    private bool _loggedFirstFrame;
    private bool _loggedFrameStats;

    protected override string AppName => "ScheduleIDoomTV.Doom";
    protected override string AppTitle => "DOOM";
    protected override Sprite Icon => DoomIconFactory.GetOrCreate()!;

    public DoomTvApp()
    {
        lock (InstancesLock)
            Instances.Add(this);
    }

    internal static int AttachAllToGameCanvas(object homeScreen)
    {
        DoomTvApp[] snapshot;
        lock (InstancesLock)
            snapshot = Instances.ToArray();

        int attached = 0;
        foreach (DoomTvApp app in snapshot)
        {
            if (app.AttachToGameCanvas(homeScreen))
                attached++;
        }

        return attached;
    }

    internal static void PumpActiveFromMelon()
    {
        _active?.PumpFrame();
    }

    protected override void OnCreatedUI(GameObject container)
    {
        try
        {
            _uiContainer = container;
            int canvasLayer = SynchronizeContainerLayer();

            _frameTexture = new Texture2D(
                DoomNativeRuntime.Width,
                DoomNativeRuntime.Height,
                TextureFormat.RGBA32,
                false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            UploadDiagnosticPattern();
            BindToOwnAppCanvas();
            MelonLogger.Msg($"Doom TV[{_instanceId}]: 640x400 frame texture prepared on the S1API TV app canvas; layer={canvasLayer}.");
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
        SynchronizeContainerLayer();
        BindToOwnAppCanvas();
        _displayRoot?.SetActive(true);
        if (_frameImage != null && _frameTexture != null)
        {
            _frameImage.color = Color.white;
            _frameImage.texture = _frameTexture;
            _frameImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            StretchDisplayToParent();
            UploadDiagnosticPattern();
            _diagnosticFramesRemaining = DiagnosticHoldFrames;
            MelonLogger.Msg($"Doom TV[{_instanceId}]: holding diagnostic checkerboard for {DiagnosticHoldFrames} Melon frames before showing native DOOM.");
        }
        DoomInputOwnershipService.Acquire();

        if (_frameTexture == null)
            MelonLogger.Warning($"Doom TV[{_instanceId}]: native DOOM started, but the TV framebuffer is unavailable.");

        MelonLogger.Msg($"Doom TV[{_instanceId}]: DOOM app opened. Melon main-loop frame pump is active.");
        MelonLogger.Msg($"Doom TV[{_instanceId}]: controls: WASD/arrows move/turn, Ctrl or left mouse fires, E/Space uses, Shift runs, 1-7 switch Doom weapons, Q opens Doom menu, Esc returns to TV menu.");
        MelonLogger.Msg($"Doom TV[{_instanceId}]: Schedule I hotbar input and HUD are disabled until the DOOM app closes.");
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

        if (_diagnosticFramesRemaining > 0)
        {
            _diagnosticFramesRemaining--;
            if (_diagnosticFramesRemaining == 0)
                MelonLogger.Msg($"Doom TV[{_instanceId}]: diagnostic hold complete; switching TV to native DOOM frames.");
            return;
        }

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

    internal bool AttachToGameCanvas(object homeScreen)
    {
        _ = homeScreen;
        try
        {
            return BindToOwnAppCanvas();
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV[{_instanceId}]: failed to bind the S1API TV app canvas: {ex.Message}");
            return false;
        }
    }

    private bool BindToOwnAppCanvas()
    {
        if (_uiContainer == null || _frameTexture == null)
            return false;

        Transform appCanvasRoot = _uiContainer.transform.parent;
        if (appCanvasRoot == null)
            return false;

        CreateOrReparentDisplay(appCanvasRoot, appCanvasRoot.gameObject.layer);
        return true;
    }

    private void CreateOrReparentDisplay(Transform parent, int layer)
    {
        if (_frameTexture == null)
            return;

        if (_displayRoot == null)
        {
            _displayRoot = new GameObject($"DoomFramebuffer_{_instanceId}");
            _displayRoot.AddComponent<RectTransform>();
            _displayRoot.AddComponent<CanvasRenderer>();
            _frameImage = _displayRoot.AddComponent<RawImage>();
            _frameImage.color = Color.white;
            _frameImage.texture = _frameTexture;
            // The native buffer is already vertically corrected and the S1API
            // app canvas presents the horizontal axis in its normal direction.
            _frameImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        _displayRoot.layer = layer;
        _displayRoot.transform.SetParent(parent, false);
        _displayRoot.transform.SetAsLastSibling();

        StretchDisplayToParent();
        _displayRoot.SetActive(false);
    }

    private void StretchDisplayToParent()
    {
        if (_displayRoot == null)
            return;

        RectTransform frameRect = _frameImage?.rectTransform
                                  ?? _displayRoot.GetComponent<RectTransform>();
        if (frameRect == null)
            return;

        // S1API TV apps use a WorldSpace canvas. The canvas's full RectTransform
        // includes off-panel space, so use Doom's 640x400 display dimensions as
        // explicit world-space UI units. This fills the visible TV panel without
        // spilling outside it.
        Vector2 targetSize = new Vector2(
            DoomNativeRuntime.Width,
            DoomNativeRuntime.Height);

        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = targetSize;
        frameRect.localScale = Vector3.one;
        MelonLogger.Msg($"Doom TV[{_instanceId}]: framebuffer sized to TV app canvas {targetSize.x:0}x{targetSize.y:0}.");
    }

    private int SynchronizeContainerLayer()
    {
        if (_uiContainer == null)
            return -1;

        Transform parent = _uiContainer.transform.parent;
        int canvasLayer = parent != null ? parent.gameObject.layer : _uiContainer.layer;
        _uiContainer.layer = canvasLayer;
        return canvasLayer;
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

        _displayRoot?.SetActive(false);
        DoomInputOwnershipService.Release();
        _runtime?.Dispose();
        _runtime = null;
        MelonLogger.Msg($"Doom TV[{_instanceId}]: DOOM and all native audio stopped; session exited; returning to TV home screen.");
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
