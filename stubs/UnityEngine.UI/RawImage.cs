using UnityEngine;

namespace UnityEngine.UI;

public class Graphic : Component
{
    public Color color { get; set; }
}

public class RawImage : Graphic
{
    public Texture? texture { get; set; }
    public Rect uvRect { get; set; }
    public RectTransform rectTransform { get; } = null!;
}

public class Image : Graphic
{
    public Sprite? sprite { get; set; }
    public bool preserveAspect { get; set; }
}
