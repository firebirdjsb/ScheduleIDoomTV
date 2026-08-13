using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ScheduleIDoom3TV;

internal static class DoomWadCompatibility
{
    private const string SuppliedDamagedSha256 = "0C91D97E5D7ABAE57A23628DA38C11E69B4ED1046400E814CD66A8B02B183807";
    private const string RepairedSha256 = "6FDF361847B46228CFEBD9F3AF09CD844282AC75F3EDBB61CA4CB27103CE2E7F";
    private const string SuppliedTntSha256 = "C0A9C29D023AF2737953663D0E03177D9B7B7B64146C158DCC2A07F9EC18F353";
    private const string SuppliedPlutoniaSha256 = "A83B00C636FA3308286E76B1B3153FC14507CAF994B0450770421260B08EFED8";
    private const int ExpectedLumpCount = 2306;
    private const int DirectoryOffset = 12371396;
    private const int FirstFlatDataOffset = 11933124;
    private const int FirstFlatDirectoryIndex = 2193;
    private const int FlatSize = 4096;

    private static readonly string[] CommonFlatNames =
        ("FLOOR0_1 FLOOR0_3 FLOOR0_6 FLOOR1_1 FLOOR1_7 FLOOR3_3 FLOOR4_1 FLOOR4_5 " +
         "FLOOR4_6 FLOOR4_8 FLOOR5_1 FLOOR5_2 FLOOR5_3 FLOOR5_4 STEP1 STEP2 FLOOR6_1 " +
         "FLOOR6_2 TLITE6_1 TLITE6_4 TLITE6_5 TLITE6_6 FLOOR7_1 FLOOR7_2 MFLR8_1 " +
         "DEM1_1 DEM1_2 DEM1_3 DEM1_4 CEIL3_1 CEIL3_2 CEIL3_5 CEIL4_2 CEIL4_3 " +
         "CEIL5_1 CEIL5_2 FLAT1 FLAT2 FLAT5 FLAT10 FLAT14 FLAT18 FLAT20 FLAT22 " +
         "FLAT23 FLAT5_4 FLAT5_5 CONS1_1 CONS1_5 CONS1_7 NUKAGE1 NUKAGE2 NUKAGE3 F_SKY1")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static readonly string[] ExtendedFlatNames =
        ("SFLR6_1 SFLR6_4 SFLR7_1 SFLR7_4 FLOOR0_2 FLOOR0_5 FLOOR0_7 FLOOR1_6 GATE1 " +
         "GATE2 GATE3 GATE4 FWATER1 FWATER2 FWATER3 FWATER4 LAVA1 LAVA2 LAVA3 LAVA4 " +
         "DEM1_5 DEM1_6 MFLR8_2 MFLR8_3 MFLR8_4 CEIL1_1 CEIL1_2 CEIL1_3 CEIL3_3 " +
         "CEIL3_4 CEIL3_6 CEIL4_1 BLOOD1 BLOOD2 BLOOD3 FLAT1_1 FLAT1_2 FLAT1_3 " +
         "FLAT5_1 FLAT5_2 FLAT5_3 FLAT5_6 FLAT5_7 FLAT5_8 CRATOP1 CRATOP2 FLAT3 " +
         "FLAT4 FLAT8 FLAT9 FLAT17 FLAT19 COMP01")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    internal static bool TryPrepare(
        DoomWadProfile profile,
        out string runtimePath,
        out string description,
        out string error)
    {
        runtimePath = string.Empty;
        description = string.Empty;
        error = string.Empty;
        string sourcePath = profile.WadPath;
        string compatiblePath = profile.RuntimeWadPath;

        if (DoomWadValidator.TryValidate(
                sourcePath,
                out DoomWadLayout sourceLayout,
                out description,
                out string sourceValidationError))
        {
            return TryResolveValidatedWad(
                profile,
                sourcePath,
                sourceLayout,
                ref description,
                out runtimePath,
                out error);
        }

        if (profile.Flavor != DoomWadFlavor.Doom3)
        {
            error = sourceValidationError;
            return false;
        }

        try
        {
            string sourceHash = ComputeSha256(sourcePath);
            if (!sourceHash.Equals(SuppliedDamagedSha256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"{sourceValidationError}; unsupported file SHA-256 {sourceHash}";
                return false;
            }

            if (File.Exists(compatiblePath)
                && ComputeSha256(compatiblePath).Equals(RepairedSha256, StringComparison.OrdinalIgnoreCase)
                && DoomWadValidator.TryValidate(
                    compatiblePath,
                    out DoomWadLayout cachedLayout,
                    out description,
                    out _)
                && cachedLayout == DoomWadLayout.Episode36)
            {
                runtimePath = compatiblePath;
                description += ", cached directory repair";
                return true;
            }

            string? directory = Path.GetDirectoryName(compatiblePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = compatiblePath + ".tmp";
            try
            {
                File.Copy(sourcePath, temporaryPath, overwrite: true);
                RepairDirectory(temporaryPath);

                string repairedHash = ComputeSha256(temporaryPath);
                if (!repairedHash.Equals(RepairedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"directory repair produced unexpected SHA-256 {repairedHash}");

                if (!DoomWadValidator.TryValidate(
                        temporaryPath,
                        out DoomWadLayout repairedLayout,
                        out description,
                        out string validationError)
                    || repairedLayout != DoomWadLayout.Episode36)
                {
                    string reason = string.IsNullOrWhiteSpace(validationError)
                        ? $"directory repair produced unexpected map layout {repairedLayout}"
                        : validationError;
                    throw new InvalidDataException(reason);
                }

                File.Move(temporaryPath, compatiblePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            runtimePath = compatiblePath;
            description += ", repaired terminal directory";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryResolveValidatedWad(
        DoomWadProfile profile,
        string sourcePath,
        DoomWadLayout layout,
        ref string description,
        out string runtimePath,
        out string error)
    {
        runtimePath = string.Empty;
        error = string.Empty;

        if (profile.Flavor == DoomWadFlavor.Doom3 && layout == DoomWadLayout.Episode36)
        {
            runtimePath = sourcePath;
            return true;
        }

        if (layout != DoomWadLayout.Commercial32)
        {
            error = $"{profile.FileName} has the wrong map layout for {profile.Title}";
            return false;
        }

        string sourceHash = ComputeSha256(sourcePath);
        string expectedHash;
        DoomWadProfile canonicalProfile;
        switch (profile.Flavor)
        {
            case DoomWadFlavor.Tnt:
                expectedHash = SuppliedTntSha256;
                canonicalProfile = DoomWadProfile.Tnt;
                break;
            case DoomWadFlavor.Plutonia:
                expectedHash = SuppliedPlutoniaSha256;
                canonicalProfile = DoomWadProfile.Plutonia;
                break;
            default:
                if (sourceHash.Equals(SuppliedTntSha256, StringComparison.OrdinalIgnoreCase))
                {
                    expectedHash = SuppliedTntSha256;
                    canonicalProfile = DoomWadProfile.Tnt;
                }
                else if (sourceHash.Equals(SuppliedPlutoniaSha256, StringComparison.OrdinalIgnoreCase))
                {
                    expectedHash = SuppliedPlutoniaSha256;
                    canonicalProfile = DoomWadProfile.Plutonia;
                }
                else
                {
                    error = "a MAP01-through-MAP32 WAD named Doom3.WAD must match the supplied " +
                            "Tnt.wad or Plutonia.wad; otherwise keep its canonical filename";
                    return false;
                }
                break;
        }

        if (!sourceHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            error = $"{profile.FileName} has unsupported SHA-256 {sourceHash}";
            return false;
        }

        if (profile.Flavor == canonicalProfile.Flavor)
        {
            runtimePath = sourcePath;
        }
        else
        {
            runtimePath = canonicalProfile.RuntimeWadPath;
            CopyCanonicalWad(sourcePath, runtimePath, sourceHash);
        }

        description += canonicalProfile.Flavor == DoomWadFlavor.Tnt
            ? ", TNT identity verified"
            : ", Plutonia identity verified";
        return true;
    }

    private static void CopyCanonicalWad(string sourcePath, string destinationPath, string sourceHash)
    {
        if (File.Exists(destinationPath)
            && ComputeSha256(destinationPath).Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
            return;

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = destinationPath + ".tmp";
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void RepairDirectory(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);

        if (stream.Length != 12408292
            || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "IWAD"
            || reader.ReadInt32() != ExpectedLumpCount
            || reader.ReadInt32() != DirectoryOffset)
            throw new InvalidDataException("the supplied WAD does not have the expected damaged layout");

        int directoryIndex = FirstFlatDirectoryIndex;
        int flatOffset = FirstFlatDataOffset;

        WriteEntry(writer, directoryIndex++, 0, 0, "F_START");
        WriteEntry(writer, directoryIndex++, 0, 0, "F1_START");
        foreach (string name in CommonFlatNames)
        {
            WriteEntry(writer, directoryIndex++, flatOffset, FlatSize, name);
            flatOffset += FlatSize;
        }

        WriteEntry(writer, directoryIndex++, 0, 0, "F1_END");
        WriteEntry(writer, directoryIndex++, 0, 0, "F2_START");
        foreach (string name in ExtendedFlatNames)
        {
            WriteEntry(writer, directoryIndex++, flatOffset, FlatSize, name);
            flatOffset += FlatSize;
        }

        WriteEntry(writer, directoryIndex++, 0, 0, "F2_END");
        WriteEntry(writer, directoryIndex++, 0, 0, "F_END");

        if (directoryIndex != ExpectedLumpCount || flatOffset != DirectoryOffset)
            throw new InvalidDataException("the repaired directory layout is internally inconsistent");

        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void WriteEntry(BinaryWriter writer, int index, int offset, int size, string name)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        if (nameBytes.Length > 8)
            throw new InvalidDataException($"WAD lump name is too long: {name}");

        writer.BaseStream.Position = DirectoryOffset + (long)index * 16;
        writer.Write(offset);
        writer.Write(size);
        writer.Write(nameBytes);
        writer.Write(new byte[8 - nameBytes.Length]);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA256 sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream));
    }
}
