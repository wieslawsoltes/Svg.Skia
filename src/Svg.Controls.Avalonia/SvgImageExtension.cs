// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Avalonia.Svg;

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
        var source = SvgSource.Load(path, baseUri);
        var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget))!;
        var image = new SvgImage { Source = source };

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
            return CreateSvgBrush(image);
        }

        return new Image { Source = image };
    }

    private static IBrush CreateSvgBrush(IImage image)
    {
        return new VisualBrush
        {
            Visual = new Image
            {
                Source = image
            }
        };
    }
}
