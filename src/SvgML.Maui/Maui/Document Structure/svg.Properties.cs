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
