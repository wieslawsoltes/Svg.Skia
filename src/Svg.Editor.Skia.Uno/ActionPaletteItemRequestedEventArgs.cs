using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class ActionPaletteItemRequestedEventArgs : RoutedEventArgs
{
    public ActionPaletteItemRequestedEventArgs(EditorActionPaletteItem item)
    {
        Item = item;
    }

    public EditorActionPaletteItem Item { get; }
}
