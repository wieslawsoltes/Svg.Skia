using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class OutlineNodeRequestEventArgs : RoutedEventArgs
{
    public OutlineNodeRequestEventArgs(EditorObjectNode node)
    {
        Node = node;
    }

    public EditorObjectNode Node { get; }
}
