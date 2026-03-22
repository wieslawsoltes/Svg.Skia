namespace Svg.Editor.Skia.Uno.Models;

public sealed class SelectionContextMenuLayerItem
{
    public SelectionContextMenuLayerItem(string key, string label, string? subtitle, FigmaIconKind iconKind, bool isSelected)
    {
        Key = key;
        Label = label;
        Subtitle = subtitle;
        IconKind = iconKind;
        IsSelected = isSelected;
    }

    public string Key { get; }

    public string Label { get; }

    public string? Subtitle { get; }

    public FigmaIconKind IconKind { get; }

    public bool IsSelected { get; }

    public string DisplayText => string.IsNullOrWhiteSpace(Subtitle) ? Label : $"{Label} • {Subtitle}";
}
