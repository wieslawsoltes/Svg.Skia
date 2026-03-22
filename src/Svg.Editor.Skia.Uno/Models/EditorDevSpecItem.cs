using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorDevSpecItem
{
    public EditorDevSpecItem(string label, string value, string description = "")
    {
        Label = label;
        Value = value;
        Description = description;
    }

    public string Label { get; }

    public string Value { get; }

    public string Description { get; }

    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description)
        ? Visibility.Collapsed
        : Visibility.Visible;
}
