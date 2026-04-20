using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace SvgML;

public partial class svg
{
    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(svg),
            new PropertyMetadata(Stretch.Uniform, OnLayoutPropertyChanged));

    public static readonly DependencyProperty StretchDirectionProperty =
        DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(svg),
            new PropertyMetadata(StretchDirection.Both, OnLayoutPropertyChanged));

    public static readonly DependencyProperty CssProperty =
        DependencyProperty.Register(
            nameof(Css),
            typeof(string),
            typeof(svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty CurrentCssProperty =
        DependencyProperty.Register(
            nameof(CurrentCss),
            typeof(string),
            typeof(svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => (StretchDirection)GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public string? Css
    {
        get => (string?)GetValue(CssProperty);
        set => SetValue(CssProperty, value);
    }

    public string? CurrentCss
    {
        get => (string?)GetValue(CurrentCssProperty);
        set => SetValue(CurrentCssProperty, value);
    }
}
