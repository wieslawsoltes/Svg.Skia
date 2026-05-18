using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Svg;

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

    public static readonly DependencyProperty ProcessingModeProperty =
        DependencyProperty.Register(
            nameof(ProcessingMode),
            typeof(SvgProcessingMode),
            typeof(svg),
            new PropertyMetadata(SvgProcessingMode.Static, OnSourcePropertyChanged));

    public static readonly DependencyProperty ExternalResourcesProperty =
        DependencyProperty.Register(
            nameof(ExternalResources),
            typeof(SvgExternalResourcePolicy),
            typeof(svg),
            new PropertyMetadata(SvgExternalResourcePolicy.Enabled, OnSourcePropertyChanged));

    public static readonly DependencyProperty PreserveUnknownElementsProperty =
        DependencyProperty.Register(
            nameof(PreserveUnknownElements),
            typeof(bool),
            typeof(svg),
            new PropertyMetadata(true, OnSourcePropertyChanged));

    public static readonly DependencyProperty PreferSvg2HrefProperty =
        DependencyProperty.Register(
            nameof(PreferSvg2Href),
            typeof(bool),
            typeof(svg),
            new PropertyMetadata(true, OnSourcePropertyChanged));

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

    public SvgProcessingMode ProcessingMode
    {
        get => (SvgProcessingMode)GetValue(ProcessingModeProperty);
        set => SetValue(ProcessingModeProperty, value);
    }

    public SvgExternalResourcePolicy ExternalResources
    {
        get => (SvgExternalResourcePolicy)GetValue(ExternalResourcesProperty);
        set => SetValue(ExternalResourcesProperty, value);
    }

    public bool PreserveUnknownElements
    {
        get => (bool)GetValue(PreserveUnknownElementsProperty);
        set => SetValue(PreserveUnknownElementsProperty, value);
    }

    public bool PreferSvg2Href
    {
        get => (bool)GetValue(PreferSvg2HrefProperty);
        set => SetValue(PreferSvg2HrefProperty, value);
    }
}
