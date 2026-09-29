using PKHeX.Core;

namespace PokeHex.Services;

public sealed class SlotRef
{
    public required bool IsParty { get; init; }
    public int Box { get; init; }
    public int Slot { get; init; }
}

public sealed class PokemonEditFacade
{
    private readonly SaveSession _session;

    public PokemonEditFacade(SaveSession session) => _session = session;

    public PKM? GetParty(int slot)
    {
        var sav = _session.Save;
        if (sav is null || slot < 0 || slot >= sav.PartyCount) return null;
        return sav.GetPartySlotAtIndex(slot);
    }

    public IReadOnlyList<PKM?> GetPartyList()
    {
        var sav = _session.Save;
        if (sav is null) return Array.Empty<PKM?>();
        var list = new List<PKM?>();
        for (int i = 0; i < 6; i++)
            list.Add(i < sav.PartyCount ? sav.GetPartySlotAtIndex(i) : null);
        return list;
    }

    public PKM? GetBoxSlot(int box, int slot)
    {
        var sav = _session.Save;
        if (sav is null || !sav.HasBox) return null;
        if (box < 0 || box >= sav.BoxCount) return null;
        if (slot < 0 || slot >= sav.BoxSlotCount) return null;
        var pkm = sav.GetBoxSlotAtIndex(box, slot);
        return pkm.Species == 0 ? null : pkm;
    }

    public void SetPartySlot(int slot, PKM pkm)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        _session.SnapshotForUndo();
        sav.SetPartySlotAtIndex(pkm, slot);
        _session.MarkDirty();
    }

    public void SetBoxSlot(int box, int slot, PKM pkm)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        _session.SnapshotForUndo();
        sav.SetBoxSlotAtIndex(pkm, box, slot);
        _session.MarkDirty();
    }

    public void SwapSlots(SlotRef a, SlotRef b)
    {
        var sav = _session.Save ?? throw new InvalidOperationException("Sin save");
        _session.SnapshotForUndo();
        var pka = (a.IsParty ? GetParty(a.Slot) : GetBoxSlot(a.Box, a.Slot)) ?? sav.BlankPKM;
        var pkb = (b.IsParty ? GetParty(b.Slot) : GetBoxSlot(b.Box, b.Slot)) ?? sav.BlankPKM;

        if (a.IsParty) sav.SetPartySlotAtIndex(pkb, a.Slot);
        else sav.SetBoxSlotAtIndex(pkb, a.Box, a.Slot);

        if (b.IsParty) sav.SetPartySlotAtIndex(pka, b.Slot);
        else sav.SetBoxSlotAtIndex(pka, b.Box, b.Slot);

        _session.MarkDirty();
    }

    public string GetSpeciesName(PKM? pkm)
    {
        if (pkm is null || pkm.Species == 0) return "(vacío)";
        var strings = GameInfo.GetStrings("es");
        if (pkm.Species < strings.Species.Count)
            return strings.Species[pkm.Species];
        return $"#{pkm.Species}";
    }

    public IReadOnlyList<string> SpeciesNames => GameInfo.GetStrings("es").Species;
    public IReadOnlyList<string> MoveNames => GameInfo.GetStrings("es").Move;
    public IReadOnlyList<string> AbilityNames => GameInfo.GetStrings("es").Ability;
    public IReadOnlyList<string> NatureNames => GameInfo.GetStrings("es").Natures;

    public void ApplyBasicEdits(PKM pkm, Action<PKM> edit)
    {
        _session.SnapshotForUndo();
        edit(pkm);
        pkm.RefreshChecksum();
        _session.MarkDirty();
    }

    public byte[] ExportPkm(PKM pkm) => pkm.Data.ToArray();

    public PKM? ImportPkm(byte[] data)
    {
        var sav = _session.Save;
        if (sav is null) return null;
        try
        {
            var pkm = EntityFormat.GetFromBytes(data, sav.Context);
            if (pkm is null) return null;
            return sav.GetCompatiblePKM(pkm);
        }
        catch
        {
            return null;
        }
    }

    public PKM? GetSelected(SlotRef? slot)
    {
        if (slot is null) return null;
        return slot.IsParty ? GetParty(slot.Slot) : GetBoxSlot(slot.Box, slot.Slot);
    }

    public void PersistSelected(SlotRef slot, PKM pkm)
    {
        if (slot.IsParty) SetPartySlot(slot.Slot, pkm);
        else SetBoxSlot(slot.Box, slot.Slot, pkm);
    }
}
