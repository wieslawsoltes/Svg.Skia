// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Avalonia.Svg;

/// <summary>
/// Provides an SVG-backed brush that can be declared in XAML resources.
/// </summary>
public class SvgResourceExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SvgResourceExtension" /> class.
    /// </summary>
    public SvgResourceExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgResourceExtension" /> class, with the provided initial key.
    /// </summary>
    /// <param name="path">The path of the SVG resource that this markup extension references or file path.</param>
    public SvgResourceExtension(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets or sets the SVG resource or file path.
    /// </summary>
    [ConstructorArgument("path")]
    [Content]
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the base URI used when resolving <see cref="Path"/> outside of XAML.
    /// </summary>
    public Uri? BaseUri { get; set; }

    /// <summary>
    /// Gets or sets the stretch applied to the resulting brush.
    /// </summary>
    public Stretch? Stretch { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment applied to the resulting brush.
    /// </summary>
    public AlignmentX? AlignmentX { get; set; }

    /// <summary>
    /// Gets or sets the vertical alignment applied to the resulting brush.
    /// </summary>
    public AlignmentY? AlignmentY { get; set; }

    /// <summary>
    /// Gets or sets the tile mode applied to the resulting brush.
    /// </summary>
    public TileMode? TileMode { get; set; }

    /// <summary>
    /// Gets or sets the destination rectangle applied to the resulting brush.
    /// </summary>
    public RelativeRect? DestinationRect { get; set; }

    /// <summary>
    /// Gets or sets the source rectangle applied to the resulting brush.
    /// </summary>
    public RelativeRect? SourceRect { get; set; }

    /// <summary>
    /// Gets or sets the opacity applied to the resulting brush.
    /// </summary>
    public double? Opacity { get; set; }

    /// <summary>
    /// Gets or sets the transform applied to the resulting brush.
    /// </summary>
    public Transform? Transform { get; set; }

    /// <summary>
    /// Gets or sets the transform origin applied to the resulting brush.
    /// </summary>
    public RelativePoint? TransformOrigin { get; set; }

    /// <summary>
    /// Creates a <see cref="VisualBrush"/> configured with the supplied image and optional overrides.
    /// </summary>
    /// <param name="image">The SVG image instance rendered by the brush.</param>
    /// <param name="stretch">Optional stretch applied to the brush.</param>
    /// <param name="alignmentX">Optional horizontal alignment applied to the brush.</param>
    /// <param name="alignmentY">Optional vertical alignment applied to the brush.</param>
    /// <param name="tileMode">Optional tile mode applied to the brush.</param>
    /// <param name="destinationRect">Optional destination rectangle for the brush content.</param>
    /// <param name="sourceRect">Optional source rectangle cropping the brush content.</param>
    /// <param name="opacity">Optional opacity multiplier applied to the brush.</param>
    /// <param name="transform">Optional transform applied to the brush.</param>
    /// <param name="transformOrigin">Optional transform origin applied when <paramref name="transform"/> is set.</param>
    /// <returns>A <see cref="VisualBrush"/> that renders <paramref name="image"/>.</returns>
    public static IBrush CreateBrush(
        IImage image,
        Stretch? stretch = null,
        AlignmentX? alignmentX = null,
        AlignmentY? alignmentY = null,
        TileMode? tileMode = null,
        RelativeRect? destinationRect = null,
        RelativeRect? sourceRect = null,
        double? opacity = null,
        Transform? transform = null,
        RelativePoint? transformOrigin = null)
    {
        var brush = new VisualBrush
        {
            Visual = new Image
            {
                Source = image
            }
        };

        if (stretch.HasValue)
        {
            brush.Stretch = stretch.Value;
        }

        if (alignmentX.HasValue)
        {
            brush.AlignmentX = alignmentX.Value;
        }

        if (alignmentY.HasValue)
        {
            brush.AlignmentY = alignmentY.Value;
        }

        if (tileMode.HasValue)
        {
            brush.TileMode = tileMode.Value;
        }

        if (destinationRect.HasValue)
        {
            brush.DestinationRect = destinationRect.Value;
        }

        if (sourceRect.HasValue)
        {
            brush.SourceRect = sourceRect.Value;
        }

        if (opacity.HasValue)
        {
            brush.Opacity = opacity.Value;
        }

        if (transform is not null)
        {
            brush.Transform = transform;
        }

        if (transformOrigin.HasValue)
        {
            brush.TransformOrigin = transformOrigin.Value;
        }

        return brush;
    }

    /// <summary>
    /// Creates a <see cref="IBrush"/> directly from an SVG path for convenient code usage.
    /// </summary>
    public static IBrush CreateBrush(
        string path,
        Uri? baseUri = null,
        Stretch? stretch = null,
        AlignmentX? alignmentX = null,
        AlignmentY? alignmentY = null,
        TileMode? tileMode = null,
        RelativeRect? destinationRect = null,
        RelativeRect? sourceRect = null,
        double? opacity = null,
        Transform? transform = null,
        RelativePoint? transformOrigin = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        return CreateBrushCore(
            path,
            baseUri,
            stretch,
            alignmentX,
            alignmentY,
            tileMode,
            destinationRect,
            sourceRect,
            opacity,
            transform,
            transformOrigin);
    }

    /// <summary>
    /// Creates an <see cref="IBrush"/> instance for use in code-behind.
    /// </summary>
    /// <param name="serviceProvider">Optional XAML service provider used to resolve relative URIs.</param>
    /// <returns>The generated <see cref="IBrush"/>.</returns>
    public IBrush ToBrush(IServiceProvider? serviceProvider = null)
    {
        if (Path is null)
        {
            throw new InvalidOperationException("SvgBrush requires a non-null Path.");
        }

        return CreateBrushCore(
            Path,
            ResolveBaseUri(serviceProvider),
            Stretch,
            AlignmentX,
            AlignmentY,
            TileMode,
            DestinationRect,
            SourceRect,
            Opacity,
            Transform,
            TransformOrigin);
    }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return ToBrush(serviceProvider);
    }

    private static IBrush CreateBrushCore(
        string path,
        Uri? baseUri,
        Stretch? stretch,
        AlignmentX? alignmentX,
        AlignmentY? alignmentY,
        TileMode? tileMode,
        RelativeRect? destinationRect,
        RelativeRect? sourceRect,
        double? opacity,
        Transform? transform,
        RelativePoint? transformOrigin)
    {
        var source = SvgSource.Load(path, baseUri);
        var image = new SvgImage
        {
            Source = source
        };

        return CreateBrush(
            image,
            stretch,
            alignmentX,
            alignmentY,
            tileMode,
            destinationRect,
            sourceRect,
            opacity,
            transform,
            transformOrigin);
    }

    private Uri? ResolveBaseUri(IServiceProvider? serviceProvider)
    {
        if (BaseUri is { } baseUri)
        {
            return baseUri;
        }

        if (serviceProvider is null)
        {
            return null;
        }

        return serviceProvider.GetContextBaseUri();
    }

    public static implicit operator Brush(SvgResourceExtension extension)
    {
        if (extension is null)
        {
            throw new ArgumentNullException(nameof(extension));
        }

        return (Brush)extension.ToBrush();
    }
}
