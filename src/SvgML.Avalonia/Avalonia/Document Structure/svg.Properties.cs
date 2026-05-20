using Avalonia;
using Avalonia.Media;
using Svg;

namespace SvgML;

public partial class svg
{
    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<svg, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<svg, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    /// <summary>
    /// Defines the Css property.
    /// </summary>
    public static readonly AttachedProperty<string?> CssProperty =
        AvaloniaProperty.RegisterAttached<svg, AvaloniaObject, string?>("Css", inherits: true);

    /// <summary>
    /// Defines the CurrentCss property.
    /// </summary>
    public static readonly AttachedProperty<string?> CurrentCssProperty =
        AvaloniaProperty.RegisterAttached<svg, AvaloniaObject, string?>("CurrentCss", inherits: true);

    /// <summary>
    /// Defines the <see cref="ProcessingMode"/> property.
    /// </summary>
    public static readonly StyledProperty<SvgProcessingMode> ProcessingModeProperty =
        AvaloniaProperty.Register<svg, SvgProcessingMode>(
            nameof(ProcessingMode),
            SvgProcessingMode.Static);

    /// <summary>
    /// Defines the <see cref="ExternalResources"/> property.
    /// </summary>
    public static readonly StyledProperty<SvgExternalResourcePolicy> ExternalResourcesProperty =
        AvaloniaProperty.Register<svg, SvgExternalResourcePolicy>(
            nameof(ExternalResources),
            SvgExternalResourcePolicy.Enabled);

    /// <summary>
    /// Defines the <see cref="PreserveUnknownElements"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> PreserveUnknownElementsProperty =
        AvaloniaProperty.Register<svg, bool>(
            nameof(PreserveUnknownElements),
            defaultValue: true);

    /// <summary>
    /// Defines the <see cref="PreferSvg2Href"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> PreferSvg2HrefProperty =
        AvaloniaProperty.Register<svg, bool>(
            nameof(PreferSvg2Href),
            defaultValue: true);

    /// <summary>
    /// Gets or sets a value controlling how the image will be stretched.
    /// </summary>
    public Stretch Stretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling in what direction the image will be stretched.
    /// </summary>
    public StretchDirection StretchDirection
    {
        get { return GetValue(StretchDirectionProperty); }
        set { SetValue(StretchDirectionProperty, value); }
    }

    public static string? GetCss(AvaloniaObject element)
    {
        return element.GetValue(CssProperty);
    }

    public static void SetCss(AvaloniaObject element, string? value)
    {
        element.SetValue(CssProperty, value);
    }

    public static string? GetCurrentCss(AvaloniaObject element)
    {
        return element.GetValue(CurrentCssProperty);
    }

    public static void SetCurrentCss(AvaloniaObject element, string? value)
    {
        element.SetValue(CurrentCssProperty, value);
    }

    /// <summary>
    /// Gets or sets the SVG processing mode used when the inline tree is loaded.
    /// </summary>
    public SvgProcessingMode ProcessingMode
    {
        get { return GetValue(ProcessingModeProperty); }
        set { SetValue(ProcessingModeProperty, value); }
    }

    /// <summary>
    /// Gets or sets the external resource policy used when the inline tree is loaded.
    /// </summary>
    public SvgExternalResourcePolicy ExternalResources
    {
        get { return GetValue(ExternalResourcesProperty); }
        set { SetValue(ExternalResourcesProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether unsupported SVG elements should be preserved in the loaded model.
    /// </summary>
    public bool PreserveUnknownElements
    {
        get { return GetValue(PreserveUnknownElementsProperty); }
        set { SetValue(PreserveUnknownElementsProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether SVG 2 unnamespaced href wins over xlink:href when both are present.
    /// </summary>
    public bool PreferSvg2Href
    {
        get { return GetValue(PreferSvg2HrefProperty); }
        set { SetValue(PreferSvg2HrefProperty, value); }
    }
}
