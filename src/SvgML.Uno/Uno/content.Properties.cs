using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace SvgML;

[ContentProperty(Name = nameof(Content))]
public partial class content
{
    public new static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(string),
            typeof(content),
            new PropertyMetadata(string.Empty, OnSvgPropertyChanged));

    public new string? Content
    {
        get => (string?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
