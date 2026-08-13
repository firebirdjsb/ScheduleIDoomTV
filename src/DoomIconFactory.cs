using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace ScheduleIDoom2TV;

internal static class DoomIconFactory
{
    private static Sprite? _icon;

    internal static Sprite? GetOrCreate()
    {
        if (_icon != null)
            return _icon;

        try
        {
            const int width = 96;
            const int height = 64;
            byte[] pixels = new byte[width * height * 4];

            // A deeper red frame and gold Roman numeral distinguish DOOM II.
            Fill(pixels, width, height, 9, 7, 8, 255);
            DrawRect(pixels, width, height, 3, 3, width - 6, height - 6, 176, 31, 22, 255);
            DrawRect(pixels, width, height, 6, 6, width - 12, height - 12, 43, 14, 10, 255);
            DrawRect(pixels, width, height, 55, 9, 34, 46, 82, 20, 12, 255);
            DrawWordDoom(pixels, width, height, 7, 18, 2, 4);
            DrawRomanTwo(pixels, width, height, 59, 14);

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                texture.LoadRawTextureData(handle.AddrOfPinnedObject(), pixels.Length);
                texture.Apply(false, false);
            }
            finally
            {
                handle.Free();
            }

            Rect rect = new(0, 0, width, height);
            Vector2 pivot = new(0.5f, 0.5f);

            MethodInfo? create = typeof(Sprite)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Create")
                .Select(m => new { Method = m, Params = m.GetParameters() })
                .Where(x => x.Params.Length >= 3 &&
                            x.Params[0].ParameterType == typeof(Texture2D) &&
                            x.Params[1].ParameterType == typeof(Rect) &&
                            x.Params[2].ParameterType == typeof(Vector2))
                .OrderBy(x => x.Params.Length)
                .Select(x => x.Method)
                .FirstOrDefault();

            if (create == null)
                return null;

            ParameterInfo[] parameters = create.GetParameters();
            object?[] args = new object?[parameters.Length];
            args[0] = texture;
            args[1] = rect;
            args[2] = pivot;
            for (int i = 3; i < parameters.Length; i++)
            {
                if (parameters[i].HasDefaultValue)
                    args[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType.IsValueType)
                    args[i] = Activator.CreateInstance(parameters[i].ParameterType);
                else
                    args[i] = null;
            }

            _icon = create.Invoke(null, args) as Sprite;
            return _icon;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Doom TV: could not generate DOOM icon: {ex.Message}");
            return null;
        }
    }

    private static void DrawWordDoom(byte[] p, int w, int h, int x, int y, int scaleX, int scaleY)
    {
        // Unity presents our raw texture rows vertically flipped. D/O are
        // vertically symmetric, but M is not, so the source M is pre-flipped.
        string[] glyphs =
        {
            "11110|10001|10001|10001|10001|10001|11110",
            "01110|10001|10001|10001|10001|10001|01110",
            "01110|10001|10001|10001|10001|10001|01110",
            "10001|10001|10001|10101|10101|11011|10001"
        };

        int cursor = x;
        foreach (string glyph in glyphs)
        {
            string[] rows = glyph.Split('|');
            for (int gy = 0; gy < rows.Length; gy++)
            {
                for (int gx = 0; gx < rows[gy].Length; gx++)
                {
                    if (rows[gy][gx] != '1')
                        continue;
                    DrawRect(p, w, h, cursor + gx * scaleX, y + gy * scaleY, Math.Max(1, scaleX - 1), Math.Max(1, scaleY - 1), 232, 112, 38, 255);
                }
            }
            cursor += 6 * scaleX;
        }
    }

    private static void DrawRomanTwo(byte[] p, int w, int h, int x, int y)
    {
        DrawRomanOne(p, w, h, x, y);
        DrawRomanOne(p, w, h, x + 16, y);
    }

    private static void DrawRomanOne(byte[] p, int w, int h, int x, int y)
    {
        const byte r = 249;
        const byte g = 184;
        const byte b = 45;
        DrawRect(p, w, h, x, y, 11, 4, r, g, b, 255);
        DrawRect(p, w, h, x + 4, y + 4, 3, 28, r, g, b, 255);
        DrawRect(p, w, h, x, y + 32, 11, 4, r, g, b, 255);
    }
    private static void Fill(byte[] p, int w, int h, byte r, byte g, byte b, byte a)
    {
        for (int i = 0; i < w * h; i++)
        {
            p[i * 4] = r;
            p[i * 4 + 1] = g;
            p[i * 4 + 2] = b;
            p[i * 4 + 3] = a;
        }
    }

    private static void DrawRect(byte[] p, int w, int h, int x, int y, int rw, int rh, byte r, byte g, byte b, byte a)
    {
        for (int yy = Math.Max(0, y); yy < Math.Min(h, y + rh); yy++)
        for (int xx = Math.Max(0, x); xx < Math.Min(w, x + rw); xx++)
        {
            int i = (yy * w + xx) * 4;
            p[i] = r;
            p[i + 1] = g;
            p[i + 2] = b;
            p[i + 3] = a;
        }
    }
}
