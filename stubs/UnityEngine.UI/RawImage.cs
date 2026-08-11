using UnityEngine;

namespace UnityEngine.UI;

public class Graphic : Component
{
    public Color color { get; set; }
}

public class RawImage : Graphic
{
    public Texture? texture { get; set; }
    public RectTransform rectTransform { get; } = null!;
}
