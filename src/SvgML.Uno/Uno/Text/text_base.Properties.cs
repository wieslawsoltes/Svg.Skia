using Microsoft.UI.Xaml;

namespace SvgML;

public abstract partial class text_base
{
    public static readonly DependencyProperty spaceProperty =
        DependencyProperty.Register(
            "space",
            typeof(string),
            typeof(text_base),
            new PropertyMetadata(string.Empty, OnSvgPropertyChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(text_base),
            new PropertyMetadata(string.Empty, OnSvgPropertyChanged));

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? space
    {
        get => (string?)GetValue(spaceProperty);
        set => SetValue(spaceProperty, value);
    }
}
