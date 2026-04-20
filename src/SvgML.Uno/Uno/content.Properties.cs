using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace SvgML;

[ContentProperty(Name = nameof(Content))]
internal partial class content
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(string),
            typeof(content),
            new PropertyMetadata(string.Empty, OnSvgPropertyChanged));

    public string? Content
    {
        get => (string?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
