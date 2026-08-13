using System.Collections.Generic;

namespace ScheduleIDoom3TV;

internal static class DoomWadSelectorRenderer
{
    private const int Width = DoomNativeRuntime.Width;
    private const int Height = DoomNativeRuntime.Height;

    private static readonly IReadOnlyDictionary<char, byte[]> Font = new Dictionary<char, byte[]>
    {
        ['A'] = Glyph("01110", "10001", "10001", "11111", "10001", "10001", "10001"),
        ['B'] = Glyph("11110", "10001", "10001", "11110", "10001", "10001", "11110"),
        ['C'] = Glyph("01111", "10000", "10000", "10000", "10000", "10000", "01111"),
        ['D'] = Glyph("11110", "10001", "10001", "10001", "10001", "10001", "11110"),
        ['E'] = Glyph("11111", "10000", "10000", "11110", "10000", "10000", "11111"),
        ['F'] = Glyph("11111", "10000", "10000", "11110", "10000", "10000", "10000"),
        ['G'] = Glyph("01111", "10000", "10000", "10111", "10001", "10001", "01111"),
        ['H'] = Glyph("10001", "10001", "10001", "11111", "10001", "10001", "10001"),
        ['I'] = Glyph("11111", "00100", "00100", "00100", "00100", "00100", "11111"),
        ['J'] = Glyph("00111", "00010", "00010", "00010", "10010", "10010", "01100"),
        ['K'] = Glyph("10001", "10010", "10100", "11000", "10100", "10010", "10001"),
        ['L'] = Glyph("10000", "10000", "10000", "10000", "10000", "10000", "11111"),
        ['M'] = Glyph("10001", "11011", "10101", "10101", "10001", "10001", "10001"),
        ['N'] = Glyph("10001", "11001", "10101", "10011", "10001", "10001", "10001"),
        ['O'] = Glyph("01110", "10001", "10001", "10001", "10001", "10001", "01110"),
        ['P'] = Glyph("11110", "10001", "10001", "11110", "10000", "10000", "10000"),
        ['Q'] = Glyph("01110", "10001", "10001", "10001", "10101", "10010", "01101"),
        ['R'] = Glyph("11110", "10001", "10001", "11110", "10100", "10010", "10001"),
        ['S'] = Glyph("01111", "10000", "10000", "01110", "00001", "00001", "11110"),
        ['T'] = Glyph("11111", "00100", "00100", "00100", "00100", "00100", "00100"),
        ['U'] = Glyph("10001", "10001", "10001", "10001", "10001", "10001", "01110"),
        ['V'] = Glyph("10001", "10001", "10001", "10001", "10001", "01010", "00100"),
        ['W'] = Glyph("10001", "10001", "10001", "10101", "10101", "10101", "01010"),
        ['X'] = Glyph("10001", "10001", "01010", "00100", "01010", "10001", "10001"),
        ['Y'] = Glyph("10001", "10001", "01010", "00100", "00100", "00100", "00100"),
        ['Z'] = Glyph("11111", "00001", "00010", "00100", "01000", "10000", "11111"),
        ['0'] = Glyph("01110", "10001", "10011", "10101", "11001", "10001", "01110"),
        ['1'] = Glyph("00100", "01100", "00100", "00100", "00100", "00100", "01110"),
        ['2'] = Glyph("01110", "10001", "00001", "00010", "00100", "01000", "11111"),
        ['3'] = Glyph("11110", "00001", "00001", "01110", "00001", "00001", "11110"),
        ['4'] = Glyph("00010", "00110", "01010", "10010", "11111", "00010", "00010"),
        ['5'] = Glyph("11111", "10000", "10000", "11110", "00001", "00001", "11110"),
        ['6'] = Glyph("01110", "10000", "10000", "11110", "10001", "10001", "01110"),
        ['7'] = Glyph("11111", "00001", "00010", "00100", "01000", "01000", "01000"),
        ['8'] = Glyph("01110", "10001", "10001", "01110", "10001", "10001", "01110"),
        ['9'] = Glyph("01110", "10001", "10001", "01111", "00001", "00001", "01110"),
        [':'] = Glyph("00000", "00100", "00100", "00000", "00100", "00100", "00000"),
        ['-'] = Glyph("00000", "00000", "00000", "11111", "00000", "00000", "00000"),
        ['.'] = Glyph("00000", "00000", "00000", "00000", "00000", "00100", "00100"),
        ['>'] = Glyph("10000", "01000", "00100", "00010", "00100", "01000", "10000"),
        ['?'] = Glyph("01110", "10001", "00001", "00010", "00100", "00000", "00100")
    };

    internal static void Render(
        byte[] pixels,
        IReadOnlyList<DoomWadProfile> profiles,
        int selectedIndex,
        string? status)
    {
        Fill(pixels, 5, 7, 12);
        DrawRect(pixels, 0, 0, Width, 8, 155, 28, 16);
        DrawRect(pixels, 0, Height - 8, Width, 8, 155, 28, 16);

        DrawCenteredText(pixels, "SELECT DOOM 3 WAD", 30, 4, 245, 212, 170);
        DrawCenteredText(pixels, "W S OR ARROWS - ENTER TO LOAD", 82, 2, 175, 180, 190);

        if (profiles.Count == 0)
        {
            DrawCenteredText(pixels, "NO SUPPORTED WADS FOUND", 175, 3, 255, 115, 75);
            DrawCenteredText(pixels, "ADD A WAD TO THE MOD WAD FOLDER", 225, 2, 220, 220, 225);
        }
        else
        {
            const int cardX = 70;
            const int cardWidth = 500;
            const int cardHeight = 52;
            const int firstY = 116;
            const int spacing = 65;

            for (int i = 0; i < profiles.Count; i++)
            {
                int y = firstY + i * spacing;
                bool selected = i == selectedIndex;
                if (selected)
                {
                    DrawRect(pixels, cardX, y, cardWidth, cardHeight, 125, 31, 18);
                    DrawOutline(pixels, cardX, y, cardWidth, cardHeight, 3, 255, 145, 35);
                    DrawText(pixels, ">", cardX + 20, y + 13, 3, 255, 215, 155);
                }
                else
                {
                    DrawRect(pixels, cardX, y, cardWidth, cardHeight, 20, 24, 32);
                    DrawOutline(pixels, cardX, y, cardWidth, cardHeight, 2, 62, 68, 80);
                }

                DrawCenteredText(
                    pixels,
                    profiles[i].Title,
                    y + 15,
                    3,
                    selected ? (byte)255 : (byte)215,
                    selected ? (byte)238 : (byte)220,
                    selected ? (byte)215 : (byte)230);
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
            DrawCenteredText(pixels, status.ToUpperInvariant(), 326, 2, 255, 100, 75);

        DrawCenteredText(pixels, "THE ORIGINAL WAD FILES ARE NEVER CHANGED", 364, 2, 125, 135, 150);
    }

    private static byte[] Glyph(params string[] rows)
    {
        byte[] result = new byte[7];
        for (int y = 0; y < result.Length; y++)
        {
            byte bits = 0;
            for (int x = 0; x < 5; x++)
            {
                if (rows[y][x] == '1')
                    bits |= (byte)(1 << (4 - x));
            }

            result[y] = bits;
        }

        return result;
    }

    private static void Fill(byte[] pixels, byte r, byte g, byte b)
    {
        for (int i = 0; i + 3 < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }
    }

    private static void DrawCenteredText(
        byte[] pixels,
        string text,
        int y,
        int scale,
        byte r,
        byte g,
        byte b)
    {
        int x = (Width - MeasureText(text, scale)) / 2;
        DrawText(pixels, text, x, y, scale, r, g, b);
    }

    private static int MeasureText(string text, int scale) =>
        text.Length == 0 ? 0 : (text.Length * 6 - 1) * scale;

    private static void DrawText(
        byte[] pixels,
        string text,
        int x,
        int y,
        int scale,
        byte r,
        byte g,
        byte b)
    {
        int cursor = x;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (c != ' ')
            {
                if (!Font.TryGetValue(c, out byte[]? rows))
                    rows = Font['?'];

                for (int row = 0; row < 7; row++)
                {
                    for (int column = 0; column < 5; column++)
                    {
                        if ((rows[row] & (1 << (4 - column))) != 0)
                        {
                            DrawRect(
                                pixels,
                                cursor + column * scale,
                                y + row * scale,
                                scale,
                                scale,
                                r,
                                g,
                                b);
                        }
                    }
                }
            }

            cursor += 6 * scale;
        }
    }

    private static void DrawOutline(
        byte[] pixels,
        int x,
        int y,
        int width,
        int height,
        int thickness,
        byte r,
        byte g,
        byte b)
    {
        DrawRect(pixels, x, y, width, thickness, r, g, b);
        DrawRect(pixels, x, y + height - thickness, width, thickness, r, g, b);
        DrawRect(pixels, x, y, thickness, height, r, g, b);
        DrawRect(pixels, x + width - thickness, y, thickness, height, r, g, b);
    }

    private static void DrawRect(
        byte[] pixels,
        int x,
        int y,
        int width,
        int height,
        byte r,
        byte g,
        byte b)
    {
        int minX = Math.Max(0, x);
        int minY = Math.Max(0, y);
        int maxX = Math.Min(Width, x + width);
        int maxY = Math.Min(Height, y + height);
        for (int py = minY; py < maxY; py++)
        {
            for (int px = minX; px < maxX; px++)
            {
                // Texture2D raw rows start at the bottom. The selector's layout
                // coordinates start at the top, so mirror the row while writing.
                int bufferY = Height - 1 - py;
                int index = (bufferY * Width + px) * 4;
                pixels[index] = r;
                pixels[index + 1] = g;
                pixels[index + 2] = b;
                pixels[index + 3] = 255;
            }
        }
    }
}
