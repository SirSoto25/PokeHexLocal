using PKHeX.Core;

namespace PokeHex.Services;

public sealed class SaveLoadResult
{
    public bool Success { get; init; }
    public SaveFile? Save { get; init; }
    public string? ErrorMessage { get; init; }
    public bool SuggestForceLoad { get; init; }
    public string? FileName { get; init; }

    public static SaveLoadResult Ok(SaveFile save, string? fileName) => new()
    {
        Success = true,
        Save = save,
        FileName = fileName
    };

    public static SaveLoadResult Fail(string message, bool suggestForce = false) => new()
    {
        Success = false,
        ErrorMessage = message,
        SuggestForceLoad = suggestForce
    };
}

public interface INativeFilePicker
{
    Task<string?> PickOpenAsync(string filterDescription, string[] extensions);
    Task<string?> PickSaveAsync(string filterDescription, string[] extensions, string defaultFileName);
    bool SupportsNativeDialogs { get; }
}

public sealed class BrowserFilePicker : INativeFilePicker
{
    public bool SupportsNativeDialogs => false;
    public Task<string?> PickOpenAsync(string filterDescription, string[] extensions) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveAsync(string filterDescription, string[] extensions, string defaultFileName) => Task.FromResult<string?>(null);
}

/// <summary>Common open-dialog / accept filters for Pokémon battery saves (not savestates).</summary>
public static class SaveFileExtensions
{
    /// <summary>Extensions accepted by the open file picker / InputFile accept.</summary>
    public static readonly string[] Open =
    [
        ".sav", ".srm", ".dsv", ".fla", ".sa1", ".sa2", ".dat", ".gci", ".bin", ".main", ".*"
    ];

    public const string InputFileAccept = ".sav,.srm,.dsv,.fla,.sa1,.sa2,.dat,.gci,.bin,.main,*";

    public const string DropzoneHint = ".sav · .srm · .dsv · .fla · .sa1 · .sa2 · main · .dat · .gci · .bin";

    public const string Win32OpenFilter =
        "*.sav;*.srm;*.dsv;*.fla;*.sa1;*.sa2;*.dat;*.gci;*.bin;main;*.*";
}

public sealed class SaveIO
{
    /// <summary>
    /// Official / well-known raw save sizes from PKHeX. Used only to strip emulator padding
    /// (trailing 0x00/0xFF) or tiny RTC footers after a normal TryGetSaveFile failure.
    /// </summary>
    private static readonly int[] KnownRawSizes =
    [
        SaveUtil.SIZE_G1RAW,
        SaveUtil.SIZE_G2RAW_U,
        SaveUtil.SIZE_G2RAW_J,
        SaveUtil.SIZE_G3RAWHALF,
        SaveUtil.SIZE_G3RAW,
        SaveUtil.SIZE_G3COLO,
        SaveUtil.SIZE_G3XD,
        SaveUtil.SIZE_G3BOX,
        SaveUtil.SIZE_G4RAW,
        SaveUtil.SIZE_G4BR,
        SaveUtil.SIZE_G5RAW,
        SaveUtil.SIZE_G6XY,
        SaveUtil.SIZE_G6ORAS,
        SaveUtil.SIZE_G6ORASDEMO,
        SaveUtil.SIZE_G7SM,
        SaveUtil.SIZE_G7USUM,
        SaveUtil.SIZE_G7GG,
        SaveUtil.SIZE_G8BDSP_0,
        SaveUtil.SIZE_G8BDSP_1,
        SaveUtil.SIZE_G8BDSP_2,
        SaveUtil.SIZE_G8BDSP_3,
    ];

    /// <summary>Documented libretro VBA-Next / gbaconv GBA .srm container size (0x20000 + 0x2000).</summary>
    private const int LibretroGbaSrmContainer = 0x22000;

    public SaveLoadResult TryLoad(byte[] data, string? fileName = null)
    {
        try
        {
            if (TryGetSave(data, fileName, out var sav) && sav is not null)
                return SaveLoadResult.Ok(sav, fileName);

            foreach (var trimmed in CandidateTrims(data))
            {
                if (TryGetSave(trimmed, fileName, out sav) && sav is not null)
                    return SaveLoadResult.Ok(sav, fileName);
            }

            return SaveLoadResult.Fail(
                "No se pudo reconocer el archivo de guardado (.sav, .srm, .dsv, etc.). Puede ser un hackrom incompatible, un save cifrado de consola, un savestate (no es un save) o un archivo dañado/con relleno de emulador. Puedes intentar «Forzar como juego…» bajo tu propio riesgo.",
                suggestForce: true);
        }
        catch (Exception ex)
        {
            return SaveLoadResult.Fail(
                $"Error al cargar el save: {ex.Message}. Si proviene de un hackrom o emulador, es posible que no sea compatible (solo best-effort).",
                suggestForce: true);
        }
    }

    public SaveLoadResult TryForceLoad(byte[] data, GameVersion version, string? fileName = null)
    {
        try
        {
            foreach (var candidate in EnumerateLoadCandidates(data))
            {
                var sav = CreateForcedSave(candidate, version);
                if (sav is not null)
                    return SaveLoadResult.Ok(sav, fileName);
            }

            return SaveLoadResult.Fail(
                $"No se pudo forzar la carga como {version}. El tamaño o la estructura no coinciden. Riesgo de corrupción si editas y guardas.");
        }
        catch (Exception ex)
        {
            return SaveLoadResult.Fail($"Fallo al forzar carga ({version}): {ex.Message}");
        }
    }

    public byte[] Export(SaveFile save)
    {
        var written = save.Write();
        return written.ToArray();
    }

    public IReadOnlyList<(GameVersion Version, string Label)> GetForceLoadVersions() =>
    [
        (GameVersion.RD, "Rojo"),
        (GameVersion.BU, "Azul"),
        (GameVersion.GN, "Verde"),
        (GameVersion.YW, "Amarillo"),
        (GameVersion.GD, "Oro"),
        (GameVersion.SI, "Plata"),
        (GameVersion.C, "Cristal"),
        (GameVersion.R, "Rubí"),
        (GameVersion.S, "Zafiro"),
        (GameVersion.E, "Esmeralda"),
        (GameVersion.FR, "Rojo Fuego"),
        (GameVersion.LG, "Verde Hoja"),
        (GameVersion.D, "Diamante"),
        (GameVersion.P, "Perla"),
        (GameVersion.Pt, "Platino"),
        (GameVersion.HG, "HeartGold"),
        (GameVersion.SS, "SoulSilver"),
        (GameVersion.B, "Negro"),
        (GameVersion.W, "Blanco"),
        (GameVersion.B2, "Negro 2"),
        (GameVersion.W2, "Blanco 2"),
        (GameVersion.X, "X"),
        (GameVersion.Y, "Y"),
        (GameVersion.OR, "Rubí Omega"),
        (GameVersion.AS, "Zafiro Alfa"),
        (GameVersion.SN, "Sol"),
        (GameVersion.MN, "Luna"),
        (GameVersion.US, "Ultrasol"),
        (GameVersion.UM, "Ultraluna"),
        (GameVersion.GP, "Let's Go Pikachu"),
        (GameVersion.GE, "Let's Go Eevee"),
        (GameVersion.SW, "Espada"),
        (GameVersion.SH, "Escudo"),
        (GameVersion.BD, "Diamante Brillante"),
        (GameVersion.SP, "Perla Reluciente"),
        (GameVersion.PLA, "Leyendas Arceus"),
        (GameVersion.SL, "Escarlata"),
        (GameVersion.VL, "Púrpura"),
        (GameVersion.ZA, "Leyendas Z-A"),
    ];

    private static bool TryGetSave(byte[] data, string? fileName, out SaveFile? sav) =>
        SaveUtil.TryGetSaveFile(data, out sav, fileName ?? string.Empty) && sav is not null;

    private static IEnumerable<byte[]> EnumerateLoadCandidates(byte[] data)
    {
        yield return data;
        foreach (var trimmed in CandidateTrims(data))
            yield return trimmed;
    }

    /// <summary>
    /// Best-effort trims for emulator battery saves: homogeneous trailing pad, small RTC footers,
    /// and the documented libretro GBA 0x22000 .srm container (raw flash/SRAM at offset 0).
    /// </summary>
    private static IEnumerable<byte[]> CandidateTrims(byte[] data)
    {
        var seen = new HashSet<int> { data.Length };

        foreach (var size in KnownRawSizes.Where(s => s < data.Length).OrderByDescending(s => s))
        {
            if (!seen.Add(size))
                continue;

            var tail = data.AsSpan(size);
            if (IsHomogeneous(tail, 0x00) || IsHomogeneous(tail, 0xFF))
                yield return data.AsSpan(0, size).ToArray();
            else if (tail.Length is > 0 and <= 64)
                yield return data.AsSpan(0, size).ToArray();
        }

        // libretro VBA-Next / gbaconv: padded GBA battery container; Pokémon GBA is usually 128 KiB flash at start.
        if (data.Length == LibretroGbaSrmContainer)
        {
            foreach (var size in new[] { SaveUtil.SIZE_G3RAW, SaveUtil.SIZE_G3RAWHALF, SaveUtil.SIZE_G1RAW })
            {
                if (seen.Add(size))
                    yield return data.AsSpan(0, size).ToArray();
            }
        }
    }

    private static bool IsHomogeneous(ReadOnlySpan<byte> span, byte value)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] != value)
                return false;
        }

        return span.Length > 0;
    }

    private static SaveFile? CreateForcedSave(byte[] data, GameVersion version)
    {
        Memory<byte> mem = data;
        return version switch
        {
            GameVersion.RD or GameVersion.BU or GameVersion.GN or GameVersion.YW
                => new SAV1(mem, LanguageID.English, version),
            GameVersion.GD or GameVersion.SI or GameVersion.C
                => new SAV2(mem, LanguageID.English, version),
            GameVersion.R or GameVersion.S
                => new SAV3RS(mem),
            GameVersion.E
                => new SAV3E(mem),
            GameVersion.FR or GameVersion.LG
                => new SAV3FRLG(mem),
            GameVersion.D or GameVersion.P
                => new SAV4DP(mem),
            GameVersion.Pt
                => new SAV4Pt(mem),
            GameVersion.HG or GameVersion.SS
                => new SAV4HGSS(mem),
            GameVersion.B or GameVersion.W
                => new SAV5BW(mem),
            GameVersion.B2 or GameVersion.W2
                => new SAV5B2W2(mem),
            GameVersion.X or GameVersion.Y
                => new SAV6XY(mem),
            GameVersion.OR or GameVersion.AS
                => new SAV6AO(mem),
            GameVersion.SN or GameVersion.MN
                => new SAV7SM(mem),
            GameVersion.US or GameVersion.UM
                => new SAV7USUM(mem),
            GameVersion.GP or GameVersion.GE
                => new SAV7b(mem),
            GameVersion.SW or GameVersion.SH
                => new SAV8SWSH(mem),
            GameVersion.BD or GameVersion.SP
                => new SAV8BS(mem),
            GameVersion.PLA
                => new SAV8LA(mem),
            GameVersion.SL or GameVersion.VL
                => new SAV9SV(mem),
            GameVersion.ZA
                => new SAV9ZA(mem),
            _ => null
        };
    }
}
