using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class OutlineSelectionRequestedEventArgs : RoutedEventArgs
{
    public OutlineSelectionRequestedEventArgs(IReadOnlyList<EditorObjectNode> selection, EditorObjectNode? primaryNode)
    {
        Selection = selection;
        PrimaryNode = primaryNode;
    }

    public IReadOnlyList<EditorObjectNode> Selection { get; }

    public EditorObjectNode? PrimaryNode { get; }
}
