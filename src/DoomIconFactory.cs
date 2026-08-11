using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace ScheduleIDoomTV;

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

            Fill(pixels, width, height, 12, 10, 10, 255);
            DrawRect(pixels, width, height, 3, 3, width - 6, height - 6, 190, 58, 24, 255);
            DrawRect(pixels, width, height, 6, 6, width - 12, height - 12, 34, 18, 14, 255);

            // Four 5-column glyphs with one-column spacing = 23 columns total.
            // At 3 px per column the word is 69 px wide, centered inside 96 px.
            DrawWordDoom(pixels, width, height, 13, 18, 3, 4);

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
        string[] glyphs =
        {
            "11110|10001|10001|10001|10001|10001|11110",
            "01110|10001|10001|10001|10001|10001|01110",
            "01110|10001|10001|10001|10001|10001|01110",
            "10001|11011|10101|10101|10001|10001|10001"
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
