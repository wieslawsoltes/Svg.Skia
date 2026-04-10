// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Svg control.
/// </summary>
public class Svg : Control
{
    private readonly Uri _baseUri;
    private SvgSource? _svg;
    private bool _enableCache;
    private bool _wireframe;
    private bool _disableFilters;
    private double _zoom = 1.0;
    private double _panX;
    private double _panY;
    private Dictionary<string, SvgSource>? _cache;
    private SKSvg? _trackedAnimationSvg;
    private readonly Stopwatch _animationPlaybackStopwatch = new();
    private DispatcherTimer? _animationDispatcherTimer;
    private SvgAnimationHostBackend _animationBackend = SvgAnimationHostBackend.Default;
    private SvgAnimationHostBackendResolution _animationBackendResolution =
        new(SvgAnimationHostBackend.Default, SvgAnimationHostBackend.Default, null);
    private TimeSpan _animationFrameInterval = TimeSpan.FromMilliseconds(16);
    private double _animationPlaybackRate = 1.0;
    private TimeSpan _lastAnimationPlaybackTimestamp;
    private bool _animationRenderLoopActive;
    private bool _animationRenderLoopRequested;
    private long _animationRenderLoopGeneration;
    private bool _nativeCompositionHostSupported = true;
    private SvgCompositionVisualScene? _nativeCompositionScene;
    private static readonly Cursor s_arrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor s_appStartingCursor = new(StandardCursorType.AppStarting);
    private static readonly Cursor s_crossCursor = new(StandardCursorType.Cross);
    private static readonly Cursor s_handCursor = new(StandardCursorType.Hand);
    private static readonly Cursor s_helpCursor = new(StandardCursorType.Help);
    private static readonly Cursor s_iBeamCursor = new(StandardCursorType.Ibeam);
    private static readonly Cursor s_sizeAllCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor s_sizeNorthSouthCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor s_sizeWestEastCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor s_topSideCursor = new(StandardCursorType.TopSide);
    private static readonly Cursor s_bottomSideCursor = new(StandardCursorType.BottomSide);
    private static readonly Cursor s_leftSideCursor = new(StandardCursorType.LeftSide);
    private static readonly Cursor s_rightSideCursor = new(StandardCursorType.RightSide);
    private static readonly Cursor s_topLeftCornerCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor s_topRightCornerCursor = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor s_bottomLeftCornerCursor = new(StandardCursorType.BottomLeftCorner);
    private static readonly Cursor s_bottomRightCornerCursor = new(StandardCursorType.BottomRightCorner);

    public SvgInteractionDispatcher Interaction { get; } = new();

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
    /// Defines the <see cref="Wireframe"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, bool> WireframeProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(Wireframe),
            o => o.Wireframe,
            (o, v) => o.Wireframe = v);

    /// <summary>
    /// Defines the <see cref="DisableFilters"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, bool> DisableFiltersProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(DisableFilters),
            o => o.DisableFilters,
            (o, v) => o.DisableFilters = v);

    /// <summary>
    /// Defines the <see cref="Zoom"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(Zoom),
            o => o.Zoom,
            (o, v) => o.Zoom = v);

    /// <summary>
    /// Defines the <see cref="PanX"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, double> PanXProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(PanX),
            o => o.PanX,
            (o, v) => o.PanX = v);

    /// <summary>
    /// Defines the <see cref="PanY"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, double> PanYProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(PanY),
            o => o.PanY,
            (o, v) => o.PanY = v);

    /// <summary>
    /// Defines the <see cref="AnimationBackend"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, SvgAnimationHostBackend> AnimationBackendProperty =
        AvaloniaProperty.RegisterDirect<Svg, SvgAnimationHostBackend>(nameof(AnimationBackend),
            o => o.AnimationBackend,
            (o, v) => o.AnimationBackend = v);

    /// <summary>
    /// Defines the <see cref="AnimationFrameInterval"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, TimeSpan> AnimationFrameIntervalProperty =
        AvaloniaProperty.RegisterDirect<Svg, TimeSpan>(nameof(AnimationFrameInterval),
            o => o.AnimationFrameInterval,
            (o, v) => o.AnimationFrameInterval = v);

    /// <summary>
    /// Defines the <see cref="AnimationPlaybackRate"/> property.
    /// </summary>
    public static readonly DirectProperty<Svg, double> AnimationPlaybackRateProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(AnimationPlaybackRate),
            o => o.AnimationPlaybackRate,
            (o, v) => o.AnimationPlaybackRate = v);

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
    /// Gets or sets a value controlling wireframe rendering mode.
    /// </summary>
    public bool Wireframe
    {
        get { return _wireframe; }
        set { SetAndRaise(WireframeProperty, ref _wireframe, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling whether SVG filters are rendered.
    /// </summary>
    public bool DisableFilters
    {
        get { return _disableFilters; }
        set { SetAndRaise(DisableFiltersProperty, ref _disableFilters, value); }
    }

    /// <summary>
    /// Gets or sets the zoom factor.
    /// </summary>
    public double Zoom
    {
        get { return _zoom; }
        set { SetAndRaise(ZoomProperty, ref _zoom, value); }
    }

    /// <summary>
    /// Gets or sets the horizontal pan offset.
    /// </summary>
    public double PanX
    {
        get { return _panX; }
        set { SetAndRaise(PanXProperty, ref _panX, value); }
    }

    /// <summary>
    /// Gets or sets the vertical pan offset.
    /// </summary>
    public double PanY
    {
        get { return _panY; }
        set { SetAndRaise(PanYProperty, ref _panY, value); }
    }

    /// <summary>
    /// Gets or sets the requested host animation backend.
    /// </summary>
    public SvgAnimationHostBackend AnimationBackend
    {
        get => _animationBackend;
        set
        {
            if (SetAndRaise(AnimationBackendProperty, ref _animationBackend, value))
            {
                UpdateAnimationPlayback();
            }
        }
    }

    /// <summary>
    /// Gets or sets the host animation frame interval.
    /// </summary>
    public TimeSpan AnimationFrameInterval
    {
        get => _animationFrameInterval;
        set
        {
            var normalized = NormalizeAnimationFrameInterval(value);
            if (SetAndRaise(AnimationFrameIntervalProperty, ref _animationFrameInterval, normalized))
            {
                UpdateAnimationPlayback();
            }
        }
    }

    /// <summary>
    /// Gets or sets the host animation playback rate multiplier.
    /// </summary>
    public double AnimationPlaybackRate
    {
        get => _animationPlaybackRate;
        set
        {
            var normalized = NormalizeAnimationPlaybackRate(value);
            if (SetAndRaise(AnimationPlaybackRateProperty, ref _animationPlaybackRate, normalized))
            {
                ResetAnimationPlaybackClock();
            }
        }
    }

    /// <summary>
    /// Gets the currently resolved host animation backend.
    /// </summary>
    public SvgAnimationHostBackend ActualAnimationBackend => _animationBackendResolution.ActualBackend;

    /// <summary>
    /// Gets the current animation backend fallback reason, if any.
    /// </summary>
    public string? AnimationBackendFallbackReason => _animationBackendResolution.FallbackReason;

    /// <summary>
    /// Gets the current animation backend resolution.
    /// </summary>
    public SvgAnimationHostBackendResolution AnimationBackendResolution => _animationBackendResolution;

    /// <summary>
    /// Gets the current animation backend capability matrix for the attached host.
    /// </summary>
    public SvgAnimationHostBackendCapabilities AnimationBackendCapabilities => CreateAnimationBackendCapabilities();

    /// <summary>
    /// Adjusts <see cref="Zoom"/> and pan offsets so that zooming keeps the
    /// specified control point fixed.
    /// </summary>
    /// <param name="newZoom">The new zoom factor.</param>
    /// <param name="point">Point in control coordinates that should stay in place.</param>
    public void ZoomToPoint(double newZoom, Point point)
    {
        var oldZoom = _zoom;

        if (newZoom < 0.1)
            newZoom = 0.1;
        if (newZoom > 10)
            newZoom = 10;

        var zoomFactor = newZoom / oldZoom;

        // adjust pan so that the supplied point remains under the cursor
        _panX = point.X - (point.X - _panX) * zoomFactor;
        _panY = point.Y - (point.Y - _panY) * zoomFactor;

        SetAndRaise(PanXProperty, ref _panX, _panX);
        SetAndRaise(PanYProperty, ref _panY, _panY);
        SetAndRaise(ZoomProperty, ref _zoom, newZoom);
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

        var source = _svg;
        var picture = source?.Picture;
        if (picture is null)
        {
            return false;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(-sourceRect.X + destRect.X - bounds.Left, -sourceRect.Y + destRect.Y - bounds.Top);
        var userMatrix = Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(PanX, PanY);
        var matrix = scaleMatrix * translateMatrix * userMatrix;
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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        DispatchPointerMoved(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        DispatchPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        DispatchPointerReleased(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        var result = Interaction.DispatchPointerExited(SkSvg, CreatePointerInput(e, SvgMouseButton.None, 0, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        DispatchPointerWheelChanged(e);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAnimationPlayback();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ApplyNativeCursor(null);
        DeactivateNativeComposition();
        UpdateAnimationPlayback();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = _svg?.Picture;
        if (picture is null)
        {
            return new Size();
        }

        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);

        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picture = _svg?.Picture;
        if (picture is null)
        {
            return new Size();
        }

        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        RefreshNativeCompositionLayout();

        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        if (IsNativeCompositionActive)
        {
            return;
        }

        var source = _svg;
        var picture = source?.Picture;
        if (picture is null)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
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

        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Left,
            -sourceRect.Y + destRect.Y - bounds.Top);

        var userMatrix = Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(PanX, PanY);

        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix * userMatrix))
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

        if (change.Property == WireframeProperty)
        {
            if (_svg?.Svg is { } skSvg)
            {
                skSvg.Wireframe = change.GetNewValue<bool>();
                skSvg.ClearWireframePicture();
            }
            InvalidateVisual();
        }

        if (change.Property == DisableFiltersProperty)
        {
            if (_svg?.Svg is { } skSvg)
            {
                skSvg.IgnoreAttributes = change.GetNewValue<bool>() ? DrawAttributes.Filter : DrawAttributes.None;
            }

            if (IsNativeCompositionActive)
            {
                UpdateAnimationPlayback();
            }

            InvalidateVisual();
        }

        if (change.Property == ZoomProperty ||
            change.Property == PanXProperty ||
            change.Property == PanYProperty)
        {
            RefreshNativeCompositionLayout();
            InvalidateVisual();
        }

        if (change.Property == StretchProperty ||
            change.Property == StretchDirectionProperty)
        {
            RefreshNativeCompositionLayout();
        }

        if (change.Property == WireframeProperty)
        {
            _nativeCompositionScene?.UpdateWireframe(change.GetNewValue<bool>());
        }
    }

    private void LoadFromPath(string? path, SvgParameters? parameters = null)
    {
        Interaction.Reset();
        ApplyNativeCursor(null);

        if (path is null)
        {
            ReplaceCurrentSource(null);
            TrackAnimationSvg(null);
            DisposeCache();
            return;
        }

        if (_enableCache && _cache is { } && _cache.TryGetValue(path, out var svg))
        {
            ReplaceCurrentSource(CreateWorkingSource(svg));
            if (_svg?.Svg is { } skSvg)
            {
                skSvg.Wireframe = _wireframe;
                skSvg.IgnoreAttributes = _disableFilters ? DrawAttributes.Filter : DrawAttributes.None;
                skSvg.ClearWireframePicture();
            }
            TrackAnimationSvg(_svg?.Svg);
            return;
        }

        if (!_enableCache)
        {
            _svg?.Dispose();
            _svg = null;
        }

        try
        {
            var loaded = SvgSource.Load(path, _baseUri, parameters);
            ReplaceCurrentSource(loaded);
            if (_svg?.Svg is { } skSvg2)
            {
                skSvg2.Wireframe = _wireframe;
                skSvg2.IgnoreAttributes = _disableFilters ? DrawAttributes.Filter : DrawAttributes.None;
                skSvg2.ClearWireframePicture();
            }
            TrackAnimationSvg(_svg?.Svg);

            if (_enableCache && _cache is { } && _svg is { })
            {
                _cache[path] = CreateWorkingSource(_svg);
            }
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image: " + e);
            ReplaceCurrentSource(null);
            TrackAnimationSvg(null);
        }
    }

    private void LoadFromSource(string? source, SvgParameters? parameters = null)
    {
        Interaction.Reset();
        ApplyNativeCursor(null);

        if (source is null)
        {
            ReplaceCurrentSource(null);
            TrackAnimationSvg(null);
            DisposeCache();
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(source);
            using var ms = new MemoryStream(bytes);
            ReplaceCurrentSource(SvgSource.LoadFromStream(ms, parameters));
            if (_svg?.Svg is { } skSvg)
            {
                skSvg.Wireframe = _wireframe;
                skSvg.IgnoreAttributes = _disableFilters ? DrawAttributes.Filter : DrawAttributes.None;
                skSvg.ClearWireframePicture();
            }
            TrackAnimationSvg(_svg?.Svg);
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image: " + e);
            ReplaceCurrentSource(null);
            TrackAnimationSvg(null);
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

    private static SvgSource CreateWorkingSource(SvgSource source)
    {
        return source.Svg?.HasAnimations == true ? source.Clone() : source;
    }

    private void ReplaceCurrentSource(SvgSource? source)
    {
        var previous = _svg;
        _svg = source;

        if (!ReferenceEquals(previous, source))
        {
            DeactivateNativeComposition();
        }

        if (previous is not null && !ReferenceEquals(previous, source) && !IsCachedSource(previous))
        {
            previous.Dispose();
        }
    }

    private bool IsCachedSource(SvgSource source)
    {
        if (_cache is null)
        {
            return false;
        }

        foreach (var cached in _cache.Values)
        {
            if (ReferenceEquals(cached, source))
            {
                return true;
            }
        }

        return false;
    }

    private void DispatchPointerMoved(PointerEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetPosition(this), out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var result = Interaction.DispatchPointerMoved(skSvg, CreatePointerInput(e, SvgMouseButton.None, 0, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private void DispatchPointerPressed(PointerPressedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetPosition(this), out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        e.Pointer.Capture(this);
        var currentPoint = e.GetCurrentPoint(this);
        var button = MapPointerUpdateKind(currentPoint.Properties.PointerUpdateKind);
        var result = Interaction.DispatchPointerPressed(skSvg, CreatePointerInput(e, button, e.ClickCount, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private void DispatchPointerReleased(PointerReleasedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetPosition(this), out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var button = MapMouseButton(e.InitialPressMouseButton);
        var result = Interaction.DispatchPointerReleased(skSvg, CreatePointerInput(e, button, 0, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
        e.Pointer.Capture(null);
    }

    private void DispatchPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetPosition(this), out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var wheelDelta = (int)e.Delta.Y;
        var result = Interaction.DispatchPointerWheelChanged(skSvg, CreatePointerInput(e, SvgMouseButton.None, 0, wheelDelta));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private SvgPointerInput CreatePointerInput(PointerEventArgs e, SvgMouseButton button, int clickCount, int wheelDelta)
    {
        if (!TryGetPicturePoint(e.GetPosition(this), out var picturePoint))
        {
            picturePoint = default;
        }

        return new SvgPointerInput(
            picturePoint,
            MapPointerType(e.Pointer.Type),
            button,
            clickCount,
            wheelDelta,
            e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.Pointer.Id.ToString());
    }

    private static SvgPointerDeviceType MapPointerType(PointerType pointerType)
    {
        return pointerType switch
        {
            PointerType.Mouse => SvgPointerDeviceType.Mouse,
            PointerType.Touch => SvgPointerDeviceType.Touch,
            PointerType.Pen => SvgPointerDeviceType.Pen,
            _ => SvgPointerDeviceType.Unknown
        };
    }

    private static SvgMouseButton MapPointerUpdateKind(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => SvgMouseButton.Left,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => SvgMouseButton.Middle,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => SvgMouseButton.Right,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => SvgMouseButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => SvgMouseButton.XButton2,
            _ => SvgMouseButton.None
        };
    }

    private static SvgMouseButton MapMouseButton(MouseButton mouseButton)
    {
        return mouseButton switch
        {
            MouseButton.Left => SvgMouseButton.Left,
            MouseButton.Middle => SvgMouseButton.Middle,
            MouseButton.Right => SvgMouseButton.Right,
            MouseButton.XButton1 => SvgMouseButton.XButton1,
            MouseButton.XButton2 => SvgMouseButton.XButton2,
            _ => SvgMouseButton.None
        };
    }

    private void ApplyNativeCursor(string? svgCursor)
    {
        var cursor = ResolveNativeCursor(svgCursor);
        if (cursor is null)
        {
            ClearValue(InputElement.CursorProperty);
        }
        else
        {
            SetCurrentValue(InputElement.CursorProperty, cursor);
        }
    }

    private static Cursor? ResolveNativeCursor(string? svgCursor)
    {
        return NormalizeSvgCursorKeyword(svgCursor) switch
        {
            null => null,
            "auto" or "default" or "arrow" => s_arrowCursor,
            "pointer" or "hand" => s_handCursor,
            "text" or "ibeam" => s_iBeamCursor,
            "crosshair" => s_crossCursor,
            "help" => s_helpCursor,
            "wait" or "progress" => s_appStartingCursor,
            "move" or "all-scroll" or "grab" or "grabbing" => s_sizeAllCursor,
            "ew-resize" or "col-resize" => s_sizeWestEastCursor,
            "ns-resize" or "row-resize" => s_sizeNorthSouthCursor,
            "n-resize" => s_topSideCursor,
            "s-resize" => s_bottomSideCursor,
            "e-resize" => s_rightSideCursor,
            "w-resize" => s_leftSideCursor,
            "nw-resize" or "nwse-resize" => s_topLeftCornerCursor,
            "se-resize" => s_bottomRightCornerCursor,
            "ne-resize" or "nesw-resize" => s_topRightCornerCursor,
            "sw-resize" => s_bottomLeftCornerCursor,
            _ => null
        };
    }

    private static string? NormalizeSvgCursorKeyword(string? svgCursor)
    {
        var keyword = svgCursor ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        keyword = keyword.Trim();
        var fallbackSeparator = keyword.LastIndexOf(',');
        if (fallbackSeparator >= 0 && fallbackSeparator < keyword.Length - 1)
        {
            keyword = keyword.Substring(fallbackSeparator + 1).Trim();
        }

        if (keyword.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase)
            ? null
            : keyword.ToLowerInvariant();
    }

    private void TrackAnimationSvg(SKSvg? skSvg)
    {
        if (ReferenceEquals(_trackedAnimationSvg, skSvg))
        {
            return;
        }

        ApplyNativeCursor(null);
        _nativeCompositionHostSupported = true;

        if (_trackedAnimationSvg is { })
        {
            _trackedAnimationSvg.AnimationInvalidated -= OnAnimationInvalidated;
        }

        _trackedAnimationSvg = skSvg;

        if (skSvg is { })
        {
            skSvg.AnimationInvalidated += OnAnimationInvalidated;
        }

        UpdateAnimationPlayback();
    }

    private SvgAnimationHostBackendCapabilities CreateAnimationBackendCapabilities()
    {
        var isHostReady = TopLevel.GetTopLevel(this) is not null;
        return new SvgAnimationHostBackendCapabilities(
            isHostReady,
            isHostReady,
            isHostReady,
            isHostReady && _nativeCompositionHostSupported && _trackedAnimationSvg?.SupportsNativeComposition == true);
    }

    private void UpdateAnimationPlayback()
    {
        StopAnimationPlaybackDrivers();
        DeactivateNativeComposition();

        var capabilities = CreateAnimationBackendCapabilities();
        var hasAnimations = _trackedAnimationSvg?.HasAnimations == true;
        _animationBackendResolution = SvgAnimationHostBackendResolver.Resolve(AnimationBackend, capabilities, hasAnimations);

        if (_trackedAnimationSvg is not { } skSvg)
        {
            return;
        }

        if (_animationBackendResolution.ActualBackend == SvgAnimationHostBackend.NativeComposition &&
            !TryActivateNativeComposition(skSvg))
        {
            var fallbackCapabilities = new SvgAnimationHostBackendCapabilities(
                capabilities.IsHostReady,
                capabilities.SupportsDispatcherTimer,
                capabilities.SupportsRenderLoop,
                supportsNativeComposition: false);
            _animationBackendResolution = SvgAnimationHostBackendResolver.Resolve(AnimationBackend, fallbackCapabilities, hasAnimations);
        }

        skSvg.AnimationMinimumRenderInterval = _animationBackendResolution.ActualBackend == SvgAnimationHostBackend.Manual
            ? TimeSpan.Zero
            : NormalizeAnimationFrameInterval(AnimationFrameInterval);

        ResetAnimationPlaybackClock();

        switch (_animationBackendResolution.ActualBackend)
        {
            case SvgAnimationHostBackend.DispatcherTimer:
                {
                    _animationDispatcherTimer ??= CreateAnimationDispatcherTimer();
                    _animationDispatcherTimer.Interval = NormalizeAnimationFrameInterval(AnimationFrameInterval);
                    _animationDispatcherTimer.Start();
                    break;
                }
            case SvgAnimationHostBackend.RenderLoop:
                {
                    _animationRenderLoopActive = true;
                    RequestNextAnimationFrame();
                    break;
                }
            case SvgAnimationHostBackend.NativeComposition:
                {
                    if (capabilities.SupportsRenderLoop)
                    {
                        _animationRenderLoopActive = true;
                        RequestNextAnimationFrame();
                    }
                    else if (capabilities.SupportsDispatcherTimer)
                    {
                        _animationDispatcherTimer ??= CreateAnimationDispatcherTimer();
                        _animationDispatcherTimer.Interval = NormalizeAnimationFrameInterval(AnimationFrameInterval);
                        _animationDispatcherTimer.Start();
                    }

                    break;
                }
        }

        if (_animationBackendResolution.ActualBackend != SvgAnimationHostBackend.NativeComposition)
        {
            InvalidateVisual();
        }
    }

    private DispatcherTimer CreateAnimationDispatcherTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render);
        timer.Tick += OnAnimationDispatcherTimerTick;
        return timer;
    }

    private void StopAnimationPlaybackDrivers()
    {
        _animationRenderLoopActive = false;
        _animationRenderLoopRequested = false;
        unchecked
        {
            _animationRenderLoopGeneration++;
        }
        _animationDispatcherTimer?.Stop();

        if (_trackedAnimationSvg is { } skSvg)
        {
            skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        }
    }

    private void RequestNextAnimationFrame()
    {
        if (!_animationRenderLoopActive || _animationRenderLoopRequested)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var generation = _animationRenderLoopGeneration;
        _animationRenderLoopRequested = true;
        topLevel.RequestAnimationFrame(time => OnAnimationFrameRequested(time, generation));
    }

    private void OnAnimationDispatcherTimerTick(object? sender, EventArgs e)
    {
        TickAnimationPlayback();
    }

    private void OnAnimationFrameRequested(TimeSpan time, long generation)
    {
        if (generation != _animationRenderLoopGeneration)
        {
            return;
        }

        _animationRenderLoopRequested = false;
        if (!_animationRenderLoopActive ||
            (_animationBackendResolution.ActualBackend != SvgAnimationHostBackend.RenderLoop &&
             _animationBackendResolution.ActualBackend != SvgAnimationHostBackend.NativeComposition))
        {
            return;
        }

        TickAnimationPlayback();
        RequestNextAnimationFrame();
    }

    private void ResetAnimationPlaybackClock()
    {
        _animationPlaybackStopwatch.Restart();
        _lastAnimationPlaybackTimestamp = TimeSpan.Zero;
    }

    private void TickAnimationPlayback()
    {
        if (_trackedAnimationSvg is not { HasAnimations: true } skSvg)
        {
            return;
        }

        var currentTimestamp = _animationPlaybackStopwatch.Elapsed;
        var delta = currentTimestamp - _lastAnimationPlaybackTimestamp;
        _lastAnimationPlaybackTimestamp = currentTimestamp;

        var scaledDelta = ScaleAnimationDelta(delta, AnimationPlaybackRate);
        if (scaledDelta > TimeSpan.Zero)
        {
            skSvg.AdvanceAnimation(scaledDelta);
            return;
        }

        if (skSvg.HasPendingAnimationFrame)
        {
            skSvg.FlushPendingAnimationFrame();
        }
    }

    private static TimeSpan NormalizeAnimationFrameInterval(TimeSpan interval)
    {
        return interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(16) : interval;
    }

    private static double NormalizeAnimationPlaybackRate(double playbackRate)
    {
        return !IsFinite(playbackRate) || playbackRate < 0 ? 0 : playbackRate;
    }

    private static TimeSpan ScaleAnimationDelta(TimeSpan delta, double playbackRate)
    {
        if (delta <= TimeSpan.Zero || playbackRate <= 0 || !IsFinite(playbackRate))
        {
            return TimeSpan.Zero;
        }

        var scaledTicks = delta.Ticks * playbackRate;
        if (double.IsNaN(scaledTicks))
        {
            return TimeSpan.Zero;
        }

        if (double.IsInfinity(scaledTicks) || scaledTicks >= long.MaxValue)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks((long)Math.Round(scaledTicks, MidpointRounding.AwayFromZero));
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private bool IsNativeCompositionActive => _nativeCompositionScene is not null;

    private bool TryActivateNativeComposition(SKSvg skSvg)
    {
        if (!skSvg.TryCreateNativeCompositionScene(out var scene) || scene is null)
        {
            return false;
        }

        if (!SvgCompositionVisualScene.TryCreate(this, scene, Wireframe, out var compositionScene) || compositionScene is null)
        {
            return false;
        }

        _nativeCompositionScene = compositionScene;
        return true;
    }

    private void DeactivateNativeComposition()
    {
        _nativeCompositionScene?.Dispose();
        _nativeCompositionScene = null;
    }

    private void RefreshNativeCompositionLayout()
    {
        _nativeCompositionScene?.RefreshLayout();
    }

    private bool TryUpdateNativeCompositionFrame()
    {
        if (_nativeCompositionScene is null || _trackedAnimationSvg is not { } skSvg)
        {
            return false;
        }

        if (!skSvg.TryCreateNativeCompositionFrame(out var frame) || frame is null)
        {
            return false;
        }

        _nativeCompositionScene.UpdateFrame(frame, Wireframe);
        return true;
    }

    private void OnAnimationInvalidated(object? sender, SvgAnimationFrameChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            InvalidateMeasure();
            InvalidateArrange();
            if (!TryUpdateNativeCompositionFrame())
            {
                InvalidateVisual();
            }
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            InvalidateMeasure();
            InvalidateArrange();
            if (!TryUpdateNativeCompositionFrame())
            {
                InvalidateVisual();
            }
        });
    }

    internal void OnNativeCompositionRenderUnavailable(string reason)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_nativeCompositionHostSupported)
            {
                return;
            }

            _nativeCompositionHostSupported = false;

            if (_animationBackendResolution.ActualBackend == SvgAnimationHostBackend.NativeComposition)
            {
                UpdateAnimationPlayback();
                InvalidateVisual();
            }

            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)
                ?.Log(this, $"Native composition backend became unavailable: {reason}");
        });
    }
}
