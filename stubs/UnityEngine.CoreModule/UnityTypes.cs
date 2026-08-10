using System;

namespace UnityEngine;

public class Object { }

public class Component : Object
{
    public GameObject gameObject { get; } = null!;
    public Transform transform { get; } = null!;
}

public class Transform : Component
{
    public void SetParent(Transform parent, bool worldPositionStays) { }
}

public class RectTransform : Transform
{
    public Vector2 anchorMin;
    public Vector2 anchorMax;
    public Vector2 offsetMin;
    public Vector2 offsetMax;
}

public class CanvasRenderer : Component { }

public class GameObject : Object
{
    public GameObject(string name = "") { }
    public GameObject(string name, params Type[] components) { }
    public string name = string.Empty;
    public Transform transform { get; } = null!;
    public T AddComponent<T>() where T : Component => null!;
    public T GetComponent<T>() where T : Component => null!;
    public void SetActive(bool value) { }
}

public struct Vector2
{
    public float x;
    public float y;
    public Vector2(float x, float y) { this.x = x; this.y = y; }
    public static Vector2 zero => new(0f, 0f);
    public static Vector2 one => new(1f, 1f);
}

public struct Rect
{
    public float x;
    public float y;
    public float width;
    public float height;
    public Rect(float x, float y, float width, float height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}

public enum TextureFormat
{
    RGBA32 = 4,
    BGRA32 = 14
}

public enum FilterMode
{
    Point = 0,
    Bilinear = 1,
    Trilinear = 2
}

public enum TextureWrapMode
{
    Repeat = 0,
    Clamp = 1
}

public class Texture : Object { }

public class Texture2D : Texture
{
    public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
    public FilterMode filterMode { get; set; }
    public TextureWrapMode wrapMode { get; set; }
    public void LoadRawTextureData(IntPtr data, int size) { }
    public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) { }
}

public class Sprite : Object { }
