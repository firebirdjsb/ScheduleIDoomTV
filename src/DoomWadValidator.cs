using System;
using System.IO;
using System.Text;

namespace ScheduleIDoom3TV;

internal static class DoomWadValidator
{
    private const int DirectoryEntrySize = 16;

    internal static bool TryValidate(string path, out string description, out string error)
    {
        description = string.Empty;
        error = string.Empty;

        try
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

            if (stream.Length < 12)
            {
                error = "the file is too small to contain a WAD header";
                return false;
            }

            byte[] identification = reader.ReadBytes(4);
            if (!NameEquals(identification, "IWAD"))
            {
                error = "the file is not a standalone IWAD";
                return false;
            }

            int lumpCount = reader.ReadInt32();
            int directoryOffset = reader.ReadInt32();
            long directoryEnd = (long)directoryOffset + (long)lumpCount * DirectoryEntrySize;
            if (lumpCount <= 0 || directoryOffset < 12 || directoryEnd > stream.Length)
            {
                error = "the WAD directory is outside the file";
                return false;
            }

            bool hasPalette = false;
            bool hasColorMap = false;
            bool hasFirstMap = false;
            bool hasLastMap = false;
            bool hasFlatStart = false;
            bool hasFlatEnd = false;
            bool hasSpriteStart = false;
            bool hasSpriteEnd = false;
            int episodeMapCount = 0;

            stream.Position = directoryOffset;
            for (int i = 0; i < lumpCount; i++)
            {
                int lumpOffset = reader.ReadInt32();
                int lumpSize = reader.ReadInt32();
                byte[] name = reader.ReadBytes(8);

                if (lumpOffset < 0 || lumpSize < 0 || (long)lumpOffset + lumpSize > stream.Length)
                {
                    error = $"lump directory entry {i} points outside the file";
                    return false;
                }

                hasPalette |= NameEquals(name, "PLAYPAL");
                hasColorMap |= NameEquals(name, "COLORMAP");
                hasFirstMap |= NameEquals(name, "E1M1");
                hasLastMap |= NameEquals(name, "E4M9");
                hasFlatStart |= NameEquals(name, "F_START");
                hasFlatEnd |= NameEquals(name, "F_END");
                hasSpriteStart |= NameEquals(name, "S_START");
                hasSpriteEnd |= NameEquals(name, "S_END");
                if (IsEpisodeMap(name))
                    episodeMapCount++;
            }

            if (!hasPalette || !hasColorMap)
            {
                error = "the IWAD is missing its palette or color map";
                return false;
            }

            if (!hasFirstMap || !hasLastMap || episodeMapCount != 36)
            {
                error = $"expected the 36-map E1M1-through-E4M9 layout, found {episodeMapCount} episode maps";
                return false;
            }

            if (!hasFlatStart || !hasFlatEnd || !hasSpriteStart || !hasSpriteEnd)
            {
                error = "the IWAD is missing required flat or sprite namespace markers";
                return false;
            }

            description = $"{stream.Length:N0} bytes, {lumpCount:N0} lumps, {episodeMapCount} episode maps";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool NameEquals(byte[] bytes, string expected)
    {
        if (bytes.Length < expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (bytes[i] != expected[i])
                return false;
        }

        for (int i = expected.Length; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
                return false;
        }

        return true;
    }

    private static bool IsEpisodeMap(byte[] name)
    {
        return name.Length == 8
            && name[0] == 'E'
            && name[1] >= '1' && name[1] <= '4'
            && name[2] == 'M'
            && name[3] >= '1' && name[3] <= '9'
            && name[4] == 0;
    }
}
