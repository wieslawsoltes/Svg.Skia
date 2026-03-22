using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class ComponentRequestedEventArgs : RoutedEventArgs
{
    public ComponentRequestedEventArgs(EditorComponentItem component)
    {
        Component = component;
    }

    public EditorComponentItem Component { get; }
}
