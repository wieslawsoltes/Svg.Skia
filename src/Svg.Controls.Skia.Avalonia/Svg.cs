// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
using DrawingColor = System.Drawing.Color;

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
    private readonly object _cacheSync = new();
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
    private long _sourceLoadVersion;
    private CancellationTokenSource? _pendingLoadCts;
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
    /// Defines the <see cref="SvgSource"/> property.
    /// </summary>
    public static readonly StyledProperty<SvgSource?> SvgSourceProperty =
        AvaloniaProperty.Register<Svg, SvgSource?>(nameof(SvgSource));

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
    /// Defines the CurrentColor property.
    /// </summary>
    public static readonly AttachedProperty<Color?> CurrentColorProperty =
        AvaloniaProperty.RegisterAttached<Svg, AvaloniaObject, Color?>("CurrentColor", inherits: true);

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
    /// Gets or sets the Svg source object.
    /// </summary>
    public SvgSource? SvgSource
    {
        get => GetValue(SvgSourceProperty);
        set => SetValue(SvgSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the default SVG currentColor value.
    /// </summary>
    public Color? CurrentColor
    {
        get => GetCurrentColor(this);
        set => SetCurrentColor(this, value);
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
        AffectsRender<Svg>(SvgSourceProperty, PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<Svg>(SvgSourceProperty, PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);

        CssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
        CurrentCssProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
        CurrentColorProperty.Changed.AddClassHandler<Control>(OnCssPropertyAttachedPropertyChanged);
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

    public static Color? GetCurrentColor(AvaloniaObject element)
    {
        return element.GetValue(CurrentColorProperty);
    }

    public static void SetCurrentColor(AvaloniaObject element, Color? value)
    {
        element.SetValue(CurrentColorProperty, value);
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
        CancelPendingLoad();
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

        if (change.Property == SvgSourceProperty ||
            change.Property == PathProperty ||
            change.Property == SourceProperty ||
            change.Property == CssProperty ||
            change.Property == CurrentCssProperty ||
            change.Property == CurrentColorProperty)
        {
            Interaction.Reset();
            ApplyNativeCursor(null);
            QueueSourceReload();
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
                lock (_cacheSync)
                {
                    _cache = new Dictionary<string, SvgSource>();
                }
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

    private void QueueSourceReload()
    {
        CancelPendingLoad();

        var source = SvgSource;
        var path = Path;
        var inlineSource = Source;
        var css = GetCss(this);
        var currentCss = GetCurrentCss(this);
        var currentColor = CurrentColor;
        var wireframe = _wireframe;
        var disableFilters = _disableFilters;
        var enableCache = _enableCache;
        Dictionary<string, SvgSource>? cache;

        lock (_cacheSync)
        {
            cache = _cache;
        }

        var loadVersion = Interlocked.Increment(ref _sourceLoadVersion);
        if (source is null &&
            string.IsNullOrWhiteSpace(path) &&
            string.IsNullOrWhiteSpace(inlineSource))
        {
            ClearSource();
            return;
        }

        var cts = new CancellationTokenSource();
        _pendingLoadCts = cts;
        _ = ReloadSourceAsync(
            source,
            path,
            inlineSource,
            css,
            currentCss,
            currentColor,
            wireframe,
            disableFilters,
            enableCache,
            cache,
            loadVersion,
            cts.Token);
    }

    private async Task ReloadSourceAsync(
        SvgSource? source,
        string? path,
        string? inlineSource,
        string? css,
        string? currentCss,
        Color? currentColor,
        bool wireframe,
        bool disableFilters,
        bool enableCache,
        Dictionary<string, SvgSource>? cache,
        long loadVersion,
        CancellationToken cancellationToken)
    {
        LoadResult result = default;

        try
        {
            if (source is { })
            {
                result = await LoadExternalSourceAsync(
                    source,
                    css,
                    currentCss,
                    currentColor,
                    wireframe,
                    disableFilters,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                result = await LoadPathAsync(
                    path,
                    css,
                    currentCss,
                    currentColor,
                    wireframe,
                    disableFilters,
                    enableCache,
                    cache,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(inlineSource))
            {
                result = await LoadInlineSourceAsync(
                    inlineSource,
                    css,
                    currentCss,
                    currentColor,
                    wireframe,
                    disableFilters,
                    cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested ||
                loadVersion != Volatile.Read(ref _sourceLoadVersion))
            {
                DisposeResultIfOwned(result);
                return;
            }

            var applied = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested ||
                    loadVersion != Volatile.Read(ref _sourceLoadVersion))
                {
                    return false;
                }

                SetCurrentSource(result);
                return true;
            }, DispatcherPriority.Normal);

            if (!applied)
            {
                DisposeResultIfOwned(result);
            }
        }
        catch (OperationCanceledException)
        {
            DisposeResultIfOwned(result);
        }
        catch (Exception e)
        {
            DisposeResultIfOwned(result);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (loadVersion != Volatile.Read(ref _sourceLoadVersion))
                {
                    return;
                }

                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load svg image: " + e);
                ClearSource();
            }, DispatcherPriority.Normal);
        }
        finally
        {
            CompletePendingLoad(loadVersion);
        }
    }

    private async Task<LoadResult> LoadExternalSourceAsync(
        SvgSource source,
        string? css,
        string? currentCss,
        Color? currentColor,
        bool wireframe,
        bool disableFilters,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workingSource = source.Clone();
            var parameters = BuildParameters(source, css, currentCss, currentColor);

            try
            {
                if (workingSource.HasPathSource || parameters is not null)
                {
                    await workingSource.ReLoadAsync(parameters, cancellationToken).ConfigureAwait(false);
                }

                ApplyRenderOptions(workingSource, wireframe, disableFilters);
                return new LoadResult(workingSource, isCacheEntry: false);
            }
            catch
            {
                workingSource.Dispose();
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoadResult> LoadPathAsync(
        string path,
        string? css,
        string? currentCss,
        Color? currentColor,
        bool wireframe,
        bool disableFilters,
        bool enableCache,
        Dictionary<string, SvgSource>? cache,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameters = BuildParameters(null, css, currentCss, currentColor);
            var normalizedPath = SvgSource.NormalizePath(path, _baseUri).ToString();
            var cacheKey = CreateCacheKey(normalizedPath, parameters);

            if (enableCache && cache is { })
            {
                SvgSource? cachedSource = null;
                lock (_cacheSync)
                {
                    if (ReferenceEquals(cache, _cache))
                    {
                        cache.TryGetValue(cacheKey, out cachedSource);
                    }
                }

                if (cachedSource is { })
                {
                    var workingSource = CreateWorkingSource(cachedSource);
                    ApplyRenderOptions(workingSource, wireframe, disableFilters);
                    return new LoadResult(workingSource, ReferenceEquals(workingSource, cachedSource));
                }
            }

            var loaded = await SvgSource.LoadAsync(path, _baseUri, parameters, cancellationToken)
                .ConfigureAwait(false);
            ApplyRenderOptions(loaded, wireframe, disableFilters);

            var isCacheEntry = false;
            if (enableCache && cache is { })
            {
                var cacheEntry = CreateWorkingSource(loaded);
                var stored = false;

                lock (_cacheSync)
                {
                    if (ReferenceEquals(cache, _cache))
                    {
                        cache[cacheKey] = cacheEntry;
                        stored = true;
                    }
                }

                if (!stored && !ReferenceEquals(cacheEntry, loaded))
                {
                    cacheEntry.Dispose();
                }

                isCacheEntry = stored && ReferenceEquals(cacheEntry, loaded);
            }

            return new LoadResult(loaded, isCacheEntry);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoadResult> LoadInlineSourceAsync(
        string source,
        string? css,
        string? currentCss,
        Color? currentColor,
        bool wireframe,
        bool disableFilters,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameters = BuildParameters(null, css, currentCss, currentColor);
            var loaded = SvgSource.LoadFromSvg(source, parameters);
            ApplyRenderOptions(loaded, wireframe, disableFilters);
            return new LoadResult(loaded, isCacheEntry: false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private void SetCurrentSource(LoadResult result)
    {
        ApplyRenderOptions(result.Source, _wireframe, _disableFilters);
        ReplaceCurrentSource(result.Source);
        TrackAnimationSvg(_svg?.Svg);
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void ClearSource()
    {
        ReplaceCurrentSource(null);
        TrackAnimationSvg(null);
        DisposeCache();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void CancelPendingLoad()
    {
        Interlocked.Increment(ref _sourceLoadVersion);
        var cts = Interlocked.Exchange(ref _pendingLoadCts, null);
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private void CompletePendingLoad(long loadVersion)
    {
        if (loadVersion != Volatile.Read(ref _sourceLoadVersion))
        {
            return;
        }

        var cts = Interlocked.Exchange(ref _pendingLoadCts, null);
        cts?.Dispose();
    }

    private static void ApplyRenderOptions(SvgSource? source, bool wireframe, bool disableFilters)
    {
        if (source?.Svg is { } skSvg)
        {
            skSvg.Wireframe = wireframe;
            skSvg.IgnoreAttributes = disableFilters ? DrawAttributes.Filter : DrawAttributes.None;
            skSvg.ClearWireframePicture();
        }
    }

    private static void DisposeResultIfOwned(LoadResult result)
    {
        if (result.Source is { } source && !result.IsCacheEntry)
        {
            source.Dispose();
        }
    }

    private void DisposeCache()
    {
        Dictionary<string, SvgSource>? cache;
        lock (_cacheSync)
        {
            cache = _cache;
            _cache = null;
        }

        if (cache is null)
        {
            return;
        }

        foreach (var kvp in cache)
        {
            if (kvp.Value != _svg)
            {
                kvp.Value.Dispose();
            }
        }
    }

    private static SvgSource CreateWorkingSource(SvgSource source)
    {
        return source.Svg?.HasAnimations == true ? source.Clone() : source;
    }

    private static SvgParameters? BuildParameters(SvgSource? source, string? css, string? currentCss, Color? currentColor)
    {
        var sourceParameters = source?.Parameters;
        var entities = sourceParameters?.Entities ?? source?.Entities;
        var entitiesCopy = entities is null ? null : new Dictionary<string, string>(entities);
        var combinedCss = CombineCss(sourceParameters?.Css ?? source?.Css, css, currentCss);
        var effectiveCurrentColor = ToDrawingColor(currentColor) ??
                                    sourceParameters?.CurrentColor ??
                                    ToDrawingColor(source?.CurrentColor);
        var loadOptions = sourceParameters?.LoadOptions;

        if ((entitiesCopy is null || entitiesCopy.Count == 0) &&
            string.IsNullOrWhiteSpace(combinedCss) &&
            effectiveCurrentColor is null &&
            loadOptions is null)
        {
            return null;
        }

        return new SvgParameters(entitiesCopy, combinedCss, effectiveCurrentColor, loadOptions);
    }

    private static string? CombineCss(params string?[] values)
    {
        StringBuilder? builder = null;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (builder is null)
            {
                builder = new StringBuilder(value);
            }
            else
            {
                builder.Append(' ');
                builder.Append(value);
            }
        }

        return builder?.ToString();
    }

    private readonly struct LoadResult
    {
        public LoadResult(SvgSource? source, bool isCacheEntry)
        {
            Source = source;
            IsCacheEntry = isCacheEntry;
        }

        public SvgSource? Source { get; }

        public bool IsCacheEntry { get; }
    }

    private static DrawingColor? ToDrawingColor(Color? color)
    {
        return color is { } value
            ? DrawingColor.FromArgb(value.A, value.R, value.G, value.B)
            : null;
    }

    private static string CreateCacheKey(string path, SvgParameters? parameters)
    {
        if ((parameters?.Entities is null || parameters.Value.Entities.Count == 0) &&
            string.IsNullOrWhiteSpace(parameters?.Css) &&
            parameters?.CurrentColor is null &&
            parameters?.LoadOptions is null)
        {
            return path;
        }

        var builder = new StringBuilder()
            .Append(path)
            .Append("\ncss:")
            .Append(parameters?.Css)
            .Append("\ncurrentColor:")
            .Append(parameters?.CurrentColor?.ToArgb().ToString("X8", CultureInfo.InvariantCulture))
            .Append("\nprocessingMode:")
            .Append(parameters?.LoadOptions?.ProcessingMode)
            .Append("\nexternalResources:")
            .Append(parameters?.LoadOptions?.ExternalResources)
            .Append("\npreserveUnknownElements:")
            .Append(parameters?.LoadOptions?.PreserveUnknownElements)
            .Append("\npreferSvg2Href:")
            .Append(parameters?.LoadOptions?.PreferSvg2Href);

        if (parameters?.Entities is { Count: > 0 } entities)
        {
            var keys = new List<string>(entities.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                builder
                    .Append("\nentity:")
                    .Append(key)
                    .Append('=')
                    .Append(entities[key]);
            }
        }

        return builder.ToString();
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
        lock (_cacheSync)
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
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (Interaction.CapturedElement is not null || Interaction.PressedElement is not null)
        {
            Interaction.Reset();
            ApplyNativeCursor(null);
        }
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
