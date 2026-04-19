using Avalonia;
using Avalonia.Metadata;

namespace SvgML;

internal partial class content
{
    public static readonly StyledProperty<string?> ContentProperty =
        AvaloniaProperty.Register<element, string?>("Content");

    [Content]
    public string? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
