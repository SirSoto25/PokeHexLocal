using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;

namespace PokeHex.Services;

public sealed class LegalityService
{
    private readonly SaveSession _session;

    public LegalityService(SaveSession session) => _session = session;

    public LegalityReport Analyze(PKM pkm)
    {
        var la = new LegalityAnalysis(pkm);
        var lines = la.Results
            .Select(chk =>
            {
                var judge = chk.Judgement switch
                {
                    Severity.Invalid => "Inválido",
                    Severity.Fishy => "Sospechoso",
                    _ => "OK"
                };
                return $"[{judge}] {chk.Identifier}: {chk}";
            })
            .ToList();

        return new LegalityReport
        {
            Valid = la.Valid,
            Summary = la.Valid ? "Legal según PKHeX" : "Problemas de legalidad detectados",
            Lines = lines,
            HackromWarning = _session.WasForceLoaded
                ? "Este save se abrió con carga forzada / posible hackrom: los avisos de legalidad pueden ser falsos positivos."
                : null
        };
    }
}

public sealed class LegalityReport
{
    public bool Valid { get; init; }
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    public string? HackromWarning { get; init; }
}

public sealed class TrainerEditFacade
{
    private readonly SaveSession _session;
    public TrainerEditFacade(SaveSession session) => _session = session;

    public string OT
    {
        get => _session.Save?.OT ?? "";
        set
        {
            if (_session.Save is null) return;
            _session.SnapshotForUndo();
            _session.Save.OT = value;
            _session.MarkDirty();
        }
    }

    public uint DisplayTID
    {
        get => _session.Save?.DisplayTID ?? 0;
        set
        {
            if (_session.Save is null) return;
            _session.SnapshotForUndo();
            _session.Save.DisplayTID = value;
            _session.MarkDirty();
        }
    }

    public uint DisplaySID
    {
        get => _session.Save?.DisplaySID ?? 0;
        set
        {
            if (_session.Save is null) return;
            _session.SnapshotForUndo();
            _session.Save.DisplaySID = value;
            _session.MarkDirty();
        }
    }

    public uint Money
    {
        get => _session.Save?.Money ?? 0;
        set
        {
            if (_session.Save is null) return;
            _session.SnapshotForUndo();
            _session.Save.Money = value;
            _session.MarkDirty();
        }
    }

    public int Gender
    {
        get => _session.Save?.Gender ?? 0;
        set
        {
            if (_session.Save is null) return;
            _session.SnapshotForUndo();
            _session.Save.Gender = (byte)value;
            _session.MarkDirty();
        }
    }

    public int PlayedHours
    {
        get => _session.Save?.PlayedHours ?? 0;
        set => SetPlayed(value, PlayedMinutes, PlayedSeconds);
    }

    public int PlayedMinutes
    {
        get => _session.Save?.PlayedMinutes ?? 0;
        set => SetPlayed(PlayedHours, value, PlayedSeconds);
    }

    public int PlayedSeconds
    {
        get => _session.Save?.PlayedSeconds ?? 0;
        set => SetPlayed(PlayedHours, PlayedMinutes, value);
    }

    public void SetPlayed(int hours, int minutes, int seconds)
    {
        if (_session.Save is null) return;
        _session.SnapshotForUndo();
        _session.Save.PlayedHours = Math.Max(0, hours);
        _session.Save.PlayedMinutes = Math.Clamp(minutes, 0, 59);
        _session.Save.PlayedSeconds = Math.Clamp(seconds, 0, 59);
        _session.MarkDirty();
    }
}

public sealed class InventoryService
{
    private readonly SaveSession _session;
    public InventoryService(SaveSession session) => _session = session;

    public IReadOnlyList<InventoryPouchView> GetPouches()
    {
        var sav = _session.Save;
        if (sav is null) return Array.Empty<InventoryPouchView>();
        try
        {
            var strings = GameInfo.GetStrings("es");
            var result = new List<InventoryPouchView>();
            foreach (var pouch in sav.Inventory.Pouches)
            {
                var items = new List<InventoryItemView>();
                foreach (var item in pouch.Items)
                {
                    if (item.Index <= 0 && item.Count <= 0) continue;
                    var name = item.Index < strings.Item.Count ? strings.Item[item.Index] : $"Item {item.Index}";
                    items.Add(new InventoryItemView { Index = item.Index, Name = name, Count = item.Count, PouchType = pouch.Type });
                }
                result.Add(new InventoryPouchView
                {
                    Type = pouch.Type.ToString(),
                    PouchType = pouch.Type,
                    Items = items
                });
            }
            return result;
        }
        catch
        {
            return Array.Empty<InventoryPouchView>();
        }
    }

    public void SetItemCount(InventoryType type, int itemIndex, int count)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        _session.SnapshotForUndo();
        var pouch = sav.Inventory.Pouches.FirstOrDefault(p => p.Type == type);
        if (pouch is null) return;
        var item = pouch.Items.FirstOrDefault(i => i.Index == itemIndex);
        if (item is null) return;
        item.Count = count;
        // Persist pouch bytes back into the save buffer.
        InventoryPouchExtensions.SaveAll(sav.Inventory.Pouches.ToList(), sav.Data);
        _session.MarkDirty();
    }
}

public sealed class InventoryPouchView
{
    public string Type { get; init; } = "";
    public InventoryType PouchType { get; init; }
    public IReadOnlyList<InventoryItemView> Items { get; init; } = Array.Empty<InventoryItemView>();
}

public sealed class InventoryItemView
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public InventoryType PouchType { get; init; }
}

public sealed class PokedexService
{
    private readonly SaveSession _session;
    public PokedexService(SaveSession session) => _session = session;

    public IReadOnlyList<PokedexEntryView> GetEntries(int max = 200)
    {
        var sav = _session.Save;
        if (sav is null) return Array.Empty<PokedexEntryView>();
        var strings = GameInfo.GetStrings("es");
        var maxSpecies = Math.Min(max, strings.Species.Count - 1);
        var list = new List<PokedexEntryView>();
        for (ushort sp = 1; sp <= maxSpecies; sp++)
        {
            bool caught = false, seen = false;
            try
            {
                caught = sav.GetCaught(sp);
                seen = sav.GetSeen(sp);
            }
            catch
            {
                // ignore
            }
            if (!caught && !seen) continue;
            list.Add(new PokedexEntryView
            {
                Species = sp,
                Name = strings.Species[sp],
                Caught = caught,
                Seen = seen
            });
        }
        return list;
    }

    public void SetCaught(ushort species, bool caught)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        _session.SnapshotForUndo();
        sav.SetCaught(species, caught);
        if (caught) sav.SetSeen(species, true);
        _session.MarkDirty();
    }
}

public sealed class PokedexEntryView
{
    public ushort Species { get; init; }
    public string Name { get; init; } = "";
    public bool Caught { get; init; }
    public bool Seen { get; init; }
}

public sealed class MysteryGiftService
{
    private readonly SaveSession _session;
    public MysteryGiftService(SaveSession session) => _session = session;

    public string Status =>
        _session.Save is null
            ? "Sin save"
            : "Importa un archivo de regalo (.wc*, .pgf, .pgt, etc.). Se colocará en la primera ranura vacía de las cajas.";

    public string ImportGift(byte[] data)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        try
        {
            var gift = MysteryGift.GetMysteryGift(data);
            if (gift is null) return "No se reconoció el archivo de regalo.";
            _session.SnapshotForUndo();
            var pkm = gift.ConvertToPKM(sav);
            if (sav.HasBox)
            {
                for (int b = 0; b < sav.BoxCount; b++)
                {
                    for (int s = 0; s < sav.BoxSlotCount; s++)
                    {
                        var existing = sav.GetBoxSlotAtIndex(b, s);
                        if (existing.Species == 0)
                        {
                            sav.SetBoxSlotAtIndex(pkm, b, s);
                            _session.MarkDirty();
                            return $"Regalo importado a caja {b + 1}, ranura {s + 1}.";
                        }
                    }
                }
            }
            return "Regalo leído pero no hay espacio en cajas.";
        }
        catch (Exception ex)
        {
            return $"Error al importar regalo: {ex.Message}";
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPokeHexServices(this IServiceCollection services)
    {
        services.AddSingleton<SaveIO>();
        services.AddSingleton<SaveSession>();
        services.AddSingleton<PokemonEditFacade>();
        services.AddSingleton<LegalityService>();
        services.AddSingleton<TrainerEditFacade>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<PokedexService>();
        services.AddSingleton<MysteryGiftService>();
        services.AddSingleton<AppNavigation>();
        return services;
    }
}

public enum AppSection
{
    Inicio,
    Equipo,
    Cajas,
    Editor,
    Entrenador,
    Mochila,
    Pokedex,
    Misc,
    Legalidad
}

public sealed class AppNavigation
{
    public AppSection Current { get; private set; } = AppSection.Inicio;
    public SlotRef? SelectedSlot { get; private set; }
    public event Action? Changed;

    public void Go(AppSection section)
    {
        Current = section;
        Changed?.Invoke();
    }

    public void SelectSlot(SlotRef slot, bool openEditor = true)
    {
        SelectedSlot = slot;
        if (openEditor) Current = AppSection.Editor;
        Changed?.Invoke();
    }
}
