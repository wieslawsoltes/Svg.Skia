using Svg.Editor.Skia;
using Windows.Storage;

namespace Svg.Editor.Skia.Uno;

/// <summary>A document owns its editor history, file identity and recovery identity.</summary>
public sealed class EditorDocument : IDisposable
{
    public Guid RecoveryId { get; internal set; } = Guid.NewGuid();
    public SvgEditorController Controller { get; } = new();
    public StorageFile? File { get; internal set; }
    // An open picker may grant only read access (for example a browser upload).
    // Save reuses a platform file only after a save picker has granted write access.
    internal bool CanWriteBack { get; set; }
    public string Name { get; internal set; } = "Untitled.svg";
    public string Title => Controller.IsDirty ? $"{Name} •" : Name;
    internal long RecoveredRevision { get; set; } = -1;
    internal SemaphoreSlim SaveGate { get; } = new(1, 1);
    internal (double Zoom, float CenterX, float CenterY)? Camera { get; set; }
    public void Dispose() => Controller.Dispose();
}
