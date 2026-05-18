using Svg;

namespace SvgML;

public enum StretchDirection
{
    UpOnly,
    DownOnly,
    Both,
}

public partial class svg
{
    public static readonly BindableProperty StretchProperty = 
        BindableProperty.Create(nameof(Stretch), typeof(Stretch), typeof(svg), Stretch.Uniform);

    public static readonly BindableProperty StretchDirectionProperty = 
        BindableProperty.Create(nameof(StretchDirection), typeof(StretchDirection), typeof(svg), StretchDirection.Both);

    // TODO: inherits = true
    public static readonly BindableProperty CssProperty = 
        BindableProperty.CreateAttached("Css", typeof(string), typeof(svg), null, propertyChanged: OnCssPropertyAttachedPropertyChanged);

    // TODO: inherits = true
    public static readonly BindableProperty CurrentCssProperty = 
        BindableProperty.CreateAttached("CurrentCss", typeof(string), typeof(svg), null, propertyChanged: OnCssPropertyAttachedPropertyChanged);

    public static readonly BindableProperty ProcessingModeProperty =
        BindableProperty.Create(nameof(ProcessingMode), typeof(SvgProcessingMode), typeof(svg), SvgProcessingMode.Static);

    public static readonly BindableProperty ExternalResourcesProperty =
        BindableProperty.Create(nameof(ExternalResources), typeof(SvgExternalResourcePolicy), typeof(svg), SvgExternalResourcePolicy.Enabled);

    public static readonly BindableProperty PreserveUnknownElementsProperty =
        BindableProperty.Create(nameof(PreserveUnknownElements), typeof(bool), typeof(svg), true);

    public static readonly BindableProperty PreferSvg2HrefProperty =
        BindableProperty.Create(nameof(PreferSvg2Href), typeof(bool), typeof(svg), true);

    public Stretch Stretch
    {
        get { return (Stretch)GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    public StretchDirection StretchDirection
    {
        get { return (StretchDirection)GetValue(StretchDirectionProperty); }
        set { SetValue(StretchDirectionProperty, value); }
    }

    public string? Css
    {
        get { return GetCss(this); }
        set { SetCss(this, value); }
    }

    public string? CurrentCss
    {
        get { return GetCurrentCss(this); }
        set { SetCurrentCss(this, value); }
    }

    public SvgProcessingMode ProcessingMode
    {
        get { return (SvgProcessingMode)GetValue(ProcessingModeProperty); }
        set { SetValue(ProcessingModeProperty, value); }
    }

    public SvgExternalResourcePolicy ExternalResources
    {
        get { return (SvgExternalResourcePolicy)GetValue(ExternalResourcesProperty); }
        set { SetValue(ExternalResourcesProperty, value); }
    }

    public bool PreserveUnknownElements
    {
        get { return (bool)GetValue(PreserveUnknownElementsProperty); }
        set { SetValue(PreserveUnknownElementsProperty, value); }
    }

    public bool PreferSvg2Href
    {
        get { return (bool)GetValue(PreferSvg2HrefProperty); }
        set { SetValue(PreferSvg2HrefProperty, value); }
    }

    public static string? GetCss(BindableObject element)
    {
        return (string)element.GetValue(CssProperty);
    }

    public static void SetCss(BindableObject element, string? value)
    {
        element.SetValue(CssProperty, value);
    }

    public static string? GetCurrentCss(BindableObject element)
    {
        return (string)element.GetValue(CurrentCssProperty);
    }

    public static void SetCurrentCss(BindableObject element, string? value)
    {
        element.SetValue(CurrentCssProperty, value);
    }

    private static void OnCssPropertyAttachedPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        switch (bindable)
        {
            case svg svgControl:
                svgControl.ReloadAndInvalidate();
                break;
            case element elementControl:
                elementControl.RootSvg?.InvalidateSvgTree();
                break;
        }
    }
}
