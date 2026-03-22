using Svg.Editor.Skia.Uno.Models;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed class OutlineContextRequestedEventArgs : RoutedEventArgs
{
    public OutlineContextRequestedEventArgs(EditorObjectNode node, FrameworkElement target, Point? position)
    {
        Node = node;
        Target = target;
        Position = position;
    }

    public EditorObjectNode Node { get; }

    public FrameworkElement Target { get; }

    public Point? Position { get; }
}
