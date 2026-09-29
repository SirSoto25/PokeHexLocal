using PKHeX.Core;

namespace PokeHex.Services;

public sealed class SaveSession
{
    private readonly SaveIO _io;
    private readonly Stack<byte[]> _undo = new();
    private const int MaxUndo = 20;

    public SaveSession(SaveIO io) => _io = io;

    public SaveFile? Save { get; private set; }
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public bool WasForceLoaded { get; private set; }
    public string? LastError { get; private set; }
    public bool CanUndo => _undo.Count > 0;

    public event Action? Changed;

    public bool HasSave => Save is not null;

    public string Summary
    {
        get
        {
            if (Save is null) return "Sin archivo cargado";
            var force = WasForceLoaded ? " (forzado)" : "";
            return $"{Save.Version} · {Save.OT} · Gen {Save.Generation}{force}";
        }
    }

    public SaveLoadResult LoadBytes(byte[] data, string? fileName = null, string? filePath = null, bool force = false, GameVersion? forceVersion = null)
    {
        LastError = null;
        SaveLoadResult result;
        if (force && forceVersion is { } v)
            result = _io.TryForceLoad(data, v, fileName);
        else
            result = _io.TryLoad(data, fileName);

        if (!result.Success)
        {
            LastError = result.ErrorMessage;
            Changed?.Invoke();
            return result;
        }

        PushUndoSnapshot();
        Save = result.Save;
        FileName = fileName ?? result.FileName;
        FilePath = filePath;
        WasForceLoaded = force;
        IsDirty = false;
        Changed?.Invoke();
        return result;
    }

    public void MarkDirty()
    {
        IsDirty = true;
        Changed?.Invoke();
    }

    public void SnapshotForUndo()
    {
        if (Save is null) return;
        PushUndoSnapshot();
    }

    public bool Undo()
    {
        if (_undo.Count == 0 || Save is null) return false;
        var bytes = _undo.Pop();
        var result = _io.TryLoad(bytes, FileName);
        if (!result.Success || result.Save is null) return false;
        Save = result.Save;
        IsDirty = true;
        Changed?.Invoke();
        return true;
    }

    public byte[]? ExportBytes()
    {
        if (Save is null) return null;
        try
        {
            var data = _io.Export(Save);
            IsDirty = false;
            Changed?.Invoke();
            return data;
        }
        catch (Exception ex)
        {
            LastError = $"Error al exportar: {ex.Message}";
            Changed?.Invoke();
            return null;
        }
    }

    public void NotifyChanged() => Changed?.Invoke();

    public void Clear()
    {
        Save = null;
        FileName = null;
        FilePath = null;
        IsDirty = false;
        WasForceLoaded = false;
        LastError = null;
        _undo.Clear();
        Changed?.Invoke();
    }

    private void PushUndoSnapshot()
    {
        if (Save is null) return;
        try
        {
            var bytes = _io.Export(Save);
            _undo.Push(bytes);
            while (_undo.Count > MaxUndo)
            {
                // Drop oldest: Stack can't easily drop bottom; rebuild
                var arr = _undo.Reverse().Take(MaxUndo).Reverse().ToArray();
                _undo.Clear();
                foreach (var b in arr) _undo.Push(b);
                break;
            }
        }
        catch
        {
            // ignore snapshot failures
        }
    }
}
