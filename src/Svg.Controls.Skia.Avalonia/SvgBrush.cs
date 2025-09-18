// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Metadata;
using Svg.Model;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Provides an SVG-backed brush that can be declared in XAML resources.
/// </summary>
public class SvgBrush : MarkupExtension
{
    /// <summary>
    /// Gets or sets the SVG resource or file path.
    /// </summary>
    [Content]
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the CSS applied when loading the SVG resource.
    /// </summary>
    public string? Css { get; set; }

    /// <summary>
    /// Gets or sets the current CSS applied when loading the SVG resource.
    /// </summary>
    public string? CurrentCss { get; set; }

    /// <summary>
    /// Creates a brush instance for a provided image.
    /// </summary>
    /// <param name="image">The image that should be rendered by the brush.</param>
    /// <returns>A brush that renders the supplied image.</returns>
    internal static IBrush CreateFromImage(IImage image)
    {
        return new VisualBrush
        {
            Visual = new Image
            {
                Source = image
            }
        };
    }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        if (Path is null)
        {
            throw new InvalidOperationException("SvgBrush requires a non-null Path.");
        }

        var baseUri = serviceProvider.GetContextBaseUri();
        var parameters = CreateParameters(Css, CurrentCss);
        var source = SvgSource.Load(Path, baseUri, parameters);
        var image = new SvgImage
        {
            Source = source,
            Css = Css,
            CurrentCss = CurrentCss
        };

        return CreateFromImage(image);
    }

    private static SvgParameters? CreateParameters(string? css, string? currentCss)
    {
        var combined = CombineCss(css, currentCss);
        return string.IsNullOrWhiteSpace(combined)
            ? null
            : new SvgParameters(null, combined);
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
}
