using System;
using MelonLoader;
using S1API.TVApp;
using UnityEngine;
using UnityEngine.UI;

namespace ScheduleIDoomTV;

public sealed class DoomTvApp : TVApp
{
    private static DoomTvApp? _active;

    private DoomNativeRuntime? _runtime;
    private Texture2D? _frameTexture;
    private RawImage? _frameImage;
    private bool _loggedFirstPump;
    private bool _loggedFirstFrame;

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

            MelonLogger.Msg("Doom TV: 640x400 framebuffer UI created on TV layer.");
        }
        catch (Exception ex)
        {
            _frameImage = null;
            _frameTexture = null;
            MelonLogger.Error($"Doom TV: framebuffer UI creation failed, but TV app registration will continue: {ex}");
        }
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

        _active = this;
        _loggedFirstPump = false;
        _loggedFirstFrame = false;
        DoomInputOwnershipService.Acquire();

        if (_frameTexture == null)
            MelonLogger.Warning("Doom TV: native DOOM started, but the TV framebuffer is unavailable. Check the preceding framebuffer compatibility error.");

        MelonLogger.Msg("Doom TV: DOOM app opened. Melon main-loop frame pump is active.");
        MelonLogger.Msg("Doom TV: controls: WASD/arrows move/turn, Ctrl or left mouse fires, E/Space uses, Shift runs, Q opens Doom menu, Esc returns to TV menu.");
    }

    // S1API still invokes this, but frame pumping is intentionally owned by the
    // MelonMod OnUpdate callback to guarantee one stable main-thread update path.
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
            MelonLogger.Msg("Doom TV: first Melon-driven DOOM update reached.");
        }

        if (_frameTexture == null)
            return;

        if (!_runtime.TickAndCapture())
            return;

        byte[] frame = _runtime.Frame;
        fixed (byte* ptr = frame)
        {
            _frameTexture.LoadRawTextureData((IntPtr)ptr, frame.Length);
        }
        _frameTexture.Apply(false, false);

        if (!_loggedFirstFrame)
        {
            _loggedFirstFrame = true;
            MelonLogger.Msg($"Doom TV: first native framebuffer uploaded to TV (frame {_runtime.LastCapturedFrameNumber}).");
        }
    }

    protected override void OnClosed()
    {
        if (ReferenceEquals(_active, this))
            _active = null;

        DoomInputOwnershipService.Release();
        _runtime?.Pause();
        MelonLogger.Msg("Doom TV: DOOM paused; Schedule I movement restored; returning to TV home screen.");
    }
}
