using System;
using MelonLoader;
using S1API.TVApp;
using UnityEngine;
using UnityEngine.UI;

namespace ScheduleIDoomTV;

public sealed class DoomTvApp : TVApp
{
    private DoomNativeRuntime? _runtime;
    private Texture2D? _frameTexture;
    private RawImage? _frameImage;

    protected override string AppName => "ScheduleIDoomTV.Doom";
    protected override string AppTitle => "DOOM";
    protected override Sprite Icon => DoomIconFactory.GetOrCreate()!;

    protected override void OnCreatedUI(GameObject container)
    {
        GameObject framebuffer = new(
            "DoomFramebuffer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        framebuffer.transform.SetParent(container.transform, false);

        RectTransform rect = framebuffer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _frameImage = framebuffer.GetComponent<RawImage>();
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

        MelonLogger.Msg("Doom TV: 640x400 framebuffer UI created.");
    }

    protected override void OnOpened()
    {
        _runtime ??= new DoomNativeRuntime();
        if (!_runtime.Start())
        {
            MelonLogger.Error($"Doom TV: cannot launch DOOM. Expected WAD: {DoomPaths.WadPath}");
            MelonLogger.Error($"Doom TV: expected runtime: {DoomPaths.RuntimePath}");
            return;
        }

        MelonLogger.Msg("Doom TV: DOOM app opened. Controls: WASD/arrows move/turn, Ctrl or left mouse fires, E/Space uses, Shift runs, Q opens Doom menu, Esc returns to TV menu.");
    }

    protected override unsafe void OnUpdate()
    {
        if (_runtime == null || !_runtime.IsRunning || _frameTexture == null)
            return;

        DoomInputService.Update(_runtime);
        if (!_runtime.TickAndCapture())
            return;

        byte[] frame = _runtime.Frame;
        fixed (byte* ptr = frame)
        {
            _frameTexture.LoadRawTextureData((IntPtr)ptr, frame.Length);
        }
        _frameTexture.Apply(false, false);
    }

    protected override void OnClosed()
    {
        _runtime?.Pause();
        MelonLogger.Msg("Doom TV: DOOM paused; returning to TV home screen.");
    }
}
