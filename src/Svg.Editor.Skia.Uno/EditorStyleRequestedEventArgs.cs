namespace Svg.Editor.Skia.Uno;

public sealed class EditorStyleRequestedEventArgs : EventArgs
{
    public EditorStyleRequestedEventArgs(EditorStyleKind kind, EditorStyleAction action, object? item = null)
    {
        Kind = kind;
        Action = action;
        Item = item;
    }

    public EditorStyleKind Kind { get; }

    public EditorStyleAction Action { get; }

    public object? Item { get; }
}
