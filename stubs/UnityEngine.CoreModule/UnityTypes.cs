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
    public Transform parent { get; } = null!;
    public Vector3 localScale { get; set; }
    public void SetParent(Transform parent, bool worldPositionStays) { }
    public void SetAsLastSibling() { }
}

public class RectTransform : Transform
{
    public Vector2 anchorMin { get; set; }
    public Vector2 anchorMax { get; set; }
    public Vector2 offsetMin { get; set; }
    public Vector2 offsetMax { get; set; }
    public Vector2 pivot { get; set; }
    public Vector2 anchoredPosition { get; set; }
    public Vector2 sizeDelta { get; set; }
}

public class GameObject : Object
{
    public GameObject(string name = "") { }
    public GameObject(string name, params Type[] components) { }
    public string name { get; set; } = string.Empty;
    public int layer { get; set; }
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

public struct Vector3
{
    public float x;
    public float y;
    public float z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3 one => new(1f, 1f, 1f);
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

public struct Color
{
    public float r;
    public float g;
    public float b;
    public float a;
    public Color(float r, float g, float b, float a = 1f)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }
    public static Color white => new(1f, 1f, 1f, 1f);
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
