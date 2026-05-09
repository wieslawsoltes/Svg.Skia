// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Svg.Model;
using DrawingColor = System.Drawing.Color;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Svg image markup extension.
/// </summary>
public class SvgImageExtension : MarkupExtension
{
    /// <summary>
    /// Initialises a new instance of an <see cref="SvgImageExtension"/>.
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
        var image = CreateImage(path, baseUri, targetControl);

        if (target.TargetProperty is not AvaloniaProperty property)
        {
            return image;
        }

        if (typeof(IImage).IsAssignableFrom(property.PropertyType))
        {
            return image;
        }

        if (typeof(IBrush).IsAssignableFrom(property.PropertyType))
        {
            return SvgResourceExtension.CreateBrush(image);
        }

        return new Image { Source = image };
    }

    private static IImage CreateImage(string path, Uri baseUri, Control? targetControl)
    {
        if (targetControl is not null)
        {
            var css = Svg.GetCss(targetControl);
            var currentCss = Svg.GetCurrentCss(targetControl);
            var currentColor = Svg.GetCurrentColor(targetControl);
            var source = SvgSource.Load(
                path,
                baseUri,
                CreateParameters(css, currentCss, currentColor));

            return CreateSvgImage(source, targetControl);
        }
        else
        {
            var source = SvgSource.Load(
                path,
                baseUri);

            return CreateSvgImage(source, targetControl);
        }
    }

    private static SvgImage CreateSvgImage(SvgSource? source, Control? targetControl)
    {
        var result = new SvgImage();

        if (targetControl == null)
        {
            result.Source = source;
            return result;
        }

        result.Css = Svg.GetCss(targetControl);
        result.CurrentCss = Svg.GetCurrentCss(targetControl);
        result.CurrentColor = Svg.GetCurrentColor(targetControl);
        result.Source = source;

        var styleBinding = targetControl.GetObservable(Svg.CssProperty).ToBinding();
        var currentStyleBinding = targetControl.GetObservable(Svg.CurrentCssProperty).ToBinding();
        var currentColorBinding = targetControl.GetObservable(Svg.CurrentColorProperty).ToBinding();

        result.Bind(SvgImage.CssProperty, styleBinding);
        result.Bind(SvgImage.CurrentCssProperty, currentStyleBinding);
        result.Bind(SvgImage.CurrentColorProperty, currentColorBinding);

        return result;
    }

    private static SvgParameters? CreateParameters(string? css, string? currentCss, Color? currentColor)
    {
        var combinedCss = CombineCss(css, currentCss);
        var drawingColor = ToDrawingColor(currentColor);
        return string.IsNullOrWhiteSpace(combinedCss) && drawingColor is null
            ? null
            : new SvgParameters(null, combinedCss, drawingColor);
    }

    private static string? CombineCss(string? css, string? currentCss)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return string.IsNullOrWhiteSpace(currentCss) ? null : currentCss;
        }

        if (string.IsNullOrWhiteSpace(currentCss))
        {
            return css;
        }

        return string.Concat(css, ' ', currentCss);
    }

    private static DrawingColor? ToDrawingColor(Color? color)
    {
        return color is { } value
            ? DrawingColor.FromArgb(value.A, value.R, value.G, value.B)
            : null;
    }
}
