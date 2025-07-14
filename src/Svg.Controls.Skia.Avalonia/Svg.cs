﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Skia;
using Svg;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Svg control.
/// </summary>
public class Svg : Control
{
    private readonly Uri _baseUri;
    private SvgSource? _svg;
    private bool _enableCache;
    private Dictionary<string, SvgSource>? _cache;

    /// <summary>
    /// Defines the <see cref="Path"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<Svg, string?>(nameof(Path));

    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<Svg, string?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<Svg, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<Svg, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    /// <summary>
    /// Defines the <see cref="EnableCache"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, bool> EnableCacheProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(EnableCache),
            o => o.EnableCache,
            (o, v) => o.EnableCache = v);

    /// <summary>
    /// Defines the Css property.
    /// </summary>
    public static readonly AttachedProperty<string?> CssProperty =
        AvaloniaProperty.RegisterAttached<Svg, AvaloniaObject, string?>("Css", inherits: true);

    /// <summary>
    /// Defines the CurrentCss property.
    /// </summary>
    public static readonly AttachedProperty<string?> CurrentCssProperty =
        AvaloniaProperty.RegisterAttached<Svg, AvaloniaObject, string?>("CurrentCss", inherits: true);

    /// <summary>
    /// Gets or sets the Svg path.
    /// </summary>
    [Content]
    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>
    /// Gets or sets the Svg source.
    /// </summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

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

    /// <summary>
    /// Gets or sets a value controlling whether the loaded images are cached.
    /// </summary>
    public bool EnableCache
    {
        get { return _enableCache; }
        set { SetAndRaise(EnableCacheProperty, ref _enableCache, value); }
    }

    /// <summary>
    /// Gets svg picture.
    /// </summary>
    public SkiaSharp.SKPicture? Picture => _svg?.Picture;

    public SKSvg? SkSvg => _svg?.Svg;

    /// <summary>
    /// Converts a point from control coordinates to picture coordinates.
    /// </summary>
    /// <param name="point">Point in control coordinates.</param>
    /// <param name="picturePoint">Converted point in picture coordinates.</param>
    /// <returns>True if the point could be converted.</returns>
    public bool TryGetPicturePoint(Point point, out SKPoint picturePoint)
    {
        picturePoint = default;

        if (_svg?.Picture is null)
        {
            return false;
        }

        var picture = _svg.Picture;
        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Left, -sourceRect.Y + destRect.Y - bounds.Top);
        var matrix = scaleMatrix * translateMatrix;
        var inverse = matrix.Invert();
        var local = inverse.Transform(point);

        picturePoint = new SKPoint((float)local.X, (float)local.Y);
        return true;
    }

    /// <summary>
    /// Hit tests elements using control coordinates.
    /// </summary>
    /// <param name="point">Point in control coordinates.</param>
    /// <returns>Sequence of hit elements.</returns>
    public IEnumerable<SvgElement> HitTestElements(Point point)
    {
        if (SkSvg is { } skSvg && TryGetPicturePoint(point, out var pp))
        {
            return skSvg.HitTestElements(pp);
        }

        return Array.Empty<SvgElement>();
    }

    static Svg()
    {
        AffectsRender<Svg>(PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<Svg>(PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);

        CssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
        CurrentCssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
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

    private static void OnCssPropertyAttachedPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
    {
        if (d is Control control)
        {
            control.InvalidateVisual();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Svg"/> class.
    /// </summary>
    /// <param name="baseUri">The base URL for the XAML context.</param>
    public Svg(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Svg"/> class.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider.</param>
    public Svg(IServiceProvider serviceProvider)
    {
        _baseUri = serviceProvider.GetContextBaseUri();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_svg?.Picture == null)
        {
            return new Size();
        }

        var sourceSize = _svg?.Picture is { }
            ? new Size(_svg.Picture.CullRect.Width, _svg.Picture.CullRect.Height)
            : default;

        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_svg?.Picture == null)
        {
            return new Size();
        }

        var sourceSize = _svg?.Picture is { }
            ? new Size(_svg.Picture.CullRect.Width, _svg.Picture.CullRect.Height)
            : default;

        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        var source = _svg;
        if (source?.Picture is null)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(source.Picture.CullRect.Width, source.Picture.CullRect.Height);
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);
        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale));

        var bounds = source.Picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Left,
            -sourceRect.Y + destRect.Y - bounds.Top);

        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix))
        {
            context.Custom(
                new SvgSourceCustomDrawOperation(
                    new Rect(0, 0, bounds.Width, bounds.Height),
                    source));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
        {
            var css = GetCss(this);
            var currentCss = GetCurrentCss(this);
            var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
            var path = change.GetNewValue<string?>();
            LoadFromPath(path, parameters);
            InvalidateVisual();
        }

        if (change.Property == CssProperty)
        {
            var css = change.GetNewValue<string?>();
            var currentCss = GetCurrentCss(this);
            var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
            var path = Path;
            var source = Source;
            
            if (path is { })
            {
                LoadFromPath(path, parameters);
            }
            else if (source is { })
            {
                LoadFromSource(source, parameters);
            }

            InvalidateVisual();
        }

        if (change.Property == CurrentCssProperty)
        {
            var css = GetCss(this);
            var currentCss = change.GetNewValue<string?>();
            var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
            var path = Path;
            var source = Source;
            
            if (path is { })
            {
                LoadFromPath(path, parameters);
            }
            else if (source is { })
            {
                LoadFromSource(source, parameters);
            }

            InvalidateVisual();
        }

        if (change.Property == SourceProperty)
        {
            var source = change.GetNewValue<string?>();
            var css = GetCss(this);
            var currentCss = GetCurrentCss(this);
            var parameters = new SvgParameters(null, string.Concat(css, ' ', currentCss));
            LoadFromSource(source, parameters);
            InvalidateVisual();
        }

        if (change.Property == EnableCacheProperty)
        {
            var enableCache = change.GetNewValue<bool>();
            if (enableCache == false)
            {
                DisposeCache();
            }
            else
            {
                _cache = new Dictionary<string, SvgSource>();
            }
        }
    }

    private void LoadFromPath(string? path, SvgParameters? parameters = null)
    {
        if (path is null)
        {
            _svg?.Dispose();
            _svg = null;
            DisposeCache();
            return;
        }

        if (_enableCache && _cache is { } && _cache.TryGetValue(path, out var svg))
        {
            _svg = svg;
            return;
        }

        if (!_enableCache)
        {
            _svg?.Dispose();
            _svg = null;
        }

        try
        {
            _svg = SvgSource.Load(path, _baseUri, parameters);

            if (_enableCache && _cache is { } && _svg is { })
            {
                _cache[path] = _svg;
            }
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image: " + e);
            _svg = null;
        }
    }

    private void LoadFromSource(string? source, SvgParameters? parameters = null)
    {
        if (source is null)
        {
            _svg?.Dispose();
            _svg = null;
            DisposeCache();
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(source);
            using var ms = new MemoryStream(bytes);
            _svg = SvgSource.LoadFromStream(ms, parameters);
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image: " + e);
            _svg = null;
        }
    }

    private void DisposeCache()
    {
        if (_cache is null)
        {
            return;
        }

        foreach (var kvp in _cache)
        {
            if (kvp.Value != _svg)
            {
                kvp.Value.Dispose();
            }
        }

        _cache = null;
    }
}
