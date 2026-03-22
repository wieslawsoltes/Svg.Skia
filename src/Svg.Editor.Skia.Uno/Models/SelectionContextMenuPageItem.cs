namespace Svg.Editor.Skia.Uno.Models;

public sealed class SelectionContextMenuPageItem
{
    public SelectionContextMenuPageItem(string key, string displayText)
    {
        Key = key;
        DisplayText = displayText;
    }

    public string Key { get; }

    public string DisplayText { get; }
}
