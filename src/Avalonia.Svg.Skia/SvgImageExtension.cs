using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Svg.Model;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Svg image markup extension.
/// </summary>
public class SvgImageExtension : MarkupExtension
{
    /// <summary>
    /// Initialises a new instance of an <see cref="SvgContentExtension"/>.
    /// </summary>
    /// <param name="path">The resource or file path</param>
    public SvgImageExtension(string path) => Path = path;

    /// <summary>
    /// Gets or sets resource or file path.
    /// </summary>
    public string Path { get; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var path = Path;
        var context = (IUriContext)serviceProvider.GetService(typeof(IUriContext))!;
        var baseUri = context.BaseUri;
        var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget))!;
        var targetControl = target.TargetObject as Control;
        var iimage = ProvideValue(path, baseUri, targetControl);
        if (target.TargetProperty is AvaloniaProperty property)
        {
            if (property.PropertyType == typeof(IImage))
            {
                return iimage;
            }
            return new Image { Source = iimage };
        }
        return iimage;
    }

    public static IImage ProvideValue(string path, Uri baseUri, Control targetControl)
    {
       var css = targetControl != null ? Svg.GetCSS(targetControl) : null;
        var cssCurrent = targetControl != null ? Svg.GetCSSCurrent(targetControl) : null;
        var source = SvgSource.Load<SvgSource>(path, baseUri, new SvgParameters() { CSS = SvgSource.CombineCSS(css, cssCurrent) });
        return CreateSvgImage(source, targetControl);
    }

    public static SvgImage CreateSvgImage(SvgSource source, Control? targetControl)
    {
        var result = new SvgImage();
        result.Source = source;
        if (targetControl != null)
        {
            result.Bind(SvgImage.CSSProperty, targetControl.GetObservable(Svg.CSSProperty).ToBinding());
            result.Bind(SvgImage.CSSCurrentProperty, targetControl.GetObservable(Svg.CSSCurrentProperty).ToBinding());
        }
        return result;
    }
}
