using System;

namespace UnityEngine;

public class Object
{
    public static T Instantiate<T>(T original) where T : Object => null!;
    public static void Destroy(Object obj) { }
}

public class Component : Object
{
    public GameObject gameObject { get; } = null!;
    public Transform transform { get; } = null!;
}

public class Transform : Component
{
    public Transform parent { get; } = null!;
    public Vector3 position { get; set; }
    public Vector3 localPosition { get; set; }
    public Quaternion rotation { get; set; }
    public Quaternion localRotation { get; set; }
    public Vector3 eulerAngles { get; set; }
    public Vector3 localEulerAngles { get; set; }
    public Vector3 forward { get; } = default;
    public Vector3 localScale { get; set; }
    public int childCount { get; }
    public Transform Find(string name) => null!;
    public Transform GetChild(int index) => null!;
    public Vector3 TransformPoint(Vector3 position) => default;
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
    public bool activeSelf { get; }
    public Transform transform { get; } = null!;
    public static GameObject Find(string name) => null!;
    public static GameObject CreatePrimitive(PrimitiveType type) => null!;
    public T AddComponent<T>() where T : Component => null!;
    public Component AddComponent(Type componentType) => null!;
    public T GetComponent<T>() where T : Component => null!;
    public Component GetComponent(Type componentType) => null!;
    public Component[] GetComponentsInChildren(Type componentType, bool includeInactive = false) => Array.Empty<Component>();
    public void SetActive(bool value) { }
}

public enum PrimitiveType
{
    Sphere,
    Capsule,
    Cylinder,
    Cube,
    Plane,
    Quad
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
    public static Vector3 zero => new(0f, 0f, 0f);
    public static Vector3 one => new(1f, 1f, 1f);
    public static Vector3 up => new(0f, 1f, 0f);
    public static float Distance(Vector3 a, Vector3 b) => 0f;
    public static Vector3 operator +(Vector3 a, Vector3 b) => default;
    public static Vector3 operator -(Vector3 a, Vector3 b) => default;
    public static Vector3 operator *(Vector3 a, float d) => default;
}

public struct Quaternion
{
    public float x;
    public float y;
    public float z;
    public float w;
    public Quaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    public static Quaternion identity => new(0f, 0f, 0f, 1f);
    public static Quaternion Euler(float x, float y, float z) => default;
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

public class Material : Object
{
    public Color color { get; set; }
    public Texture? mainTexture { get; set; }
    public Vector2 mainTextureScale { get; set; }
    public void EnableKeyword(string keyword) { }
    public void SetColor(string name, Color value) { }
}

public class Renderer : Component
{
    public Material material { get; set; } = null!;
}

public class MeshRenderer : Renderer { }

public enum LightType
{
    Spot,
    Directional,
    Point,
    Area,
    Rectangle,
    Disc
}

public class Light : Component
{
    public LightType type { get; set; }
    public Color color { get; set; }
    public float intensity { get; set; }
    public float range { get; set; }
}
