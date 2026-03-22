using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class PageRequestedEventArgs : RoutedEventArgs
{
    public PageRequestedEventArgs(EditorPageItem page)
    {
        Page = page;
    }

    public EditorPageItem Page { get; }
}
