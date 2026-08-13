namespace UnityEngine;

public class CanvasRenderer : Component { }

public enum RenderMode
{
    ScreenSpaceOverlay = 0,
    ScreenSpaceCamera = 1,
    WorldSpace = 2
}

public class Canvas : Component
{
    public bool enabled { get; set; }
    public RenderMode renderMode { get; set; }
    public bool overrideSorting { get; set; }
    public int sortingOrder { get; set; }
}
