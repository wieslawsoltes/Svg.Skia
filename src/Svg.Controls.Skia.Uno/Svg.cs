using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Model;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaAutoCanvasRestore = SkiaSharp.SKAutoCanvasRestore;
using SkiaCanvas = SkiaSharp.SKCanvas;
using SkiaMatrix = SkiaSharp.SKMatrix;
using SkiaPicture = SkiaSharp.SKPicture;
using SkiaRect = SkiaSharp.SKRect;

namespace Uno.Svg.Skia;

[ContentProperty(Name = nameof(Path))]
public sealed class Svg : SKCanvasElement
{
    private SvgSource? _svg;
    private Dictionary<string, SvgSource>? _cache;
    private CancellationTokenSource? _pendingLoadCts;
    private long _loadVersion;
    private SKSvg? _trackedAnimationSvg;
    private readonly Stopwatch _animationPlaybackStopwatch = new();
    private DispatcherQueueTimer? _animationDispatcherTimer;
    private SvgAnimationHostBackendResolution _animationBackendResolution =
        new(SvgAnimationHostBackend.Default, SvgAnimationHostBackend.Default, null);
    private TimeSpan _lastAnimationPlaybackTimestamp;
    private bool _animationRenderingSubscribed;
    private static readonly InputSystemCursor s_arrowCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private static readonly InputSystemCursor s_appStartingCursor = InputSystemCursor.Create(InputSystemCursorShape.AppStarting);
    private static readonly InputSystemCursor s_crossCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    private static readonly InputSystemCursor s_handCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    private static readonly InputSystemCursor s_helpCursor = InputSystemCursor.Create(InputSystemCursorShape.Help);
    private static readonly InputSystemCursor s_iBeamCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
    private static readonly InputSystemCursor s_sizeAllCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    private static readonly InputSystemCursor s_sizeNorthSouthCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private static readonly InputSystemCursor s_sizeWestEastCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private static readonly InputSystemCursor s_sizeNorthEastSouthWestCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);
    private static readonly InputSystemCursor s_sizeNorthWestSouthEastCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
    private static readonly InputSystemCursor s_universalNoCursor = InputSystemCursor.Create(InputSystemCursorShape.UniversalNo);
    private static readonly InputSystemCursor s_upArrowCursor = InputSystemCursor.Create(InputSystemCursorShape.UpArrow);
    private static readonly InputSystemCursor s_waitCursor = InputSystemCursor.Create(InputSystemCursorShape.Wait);

    public SvgInteractionDispatcher Interaction { get; } = new();

    public static readonly DependencyProperty SvgSourceProperty =
        DependencyProperty.Register(
            nameof(SvgSource),
            typeof(SvgSource),
            typeof(Svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register(
            nameof(Path),
            typeof(string),
            typeof(Svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(string),
            typeof(Svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(Svg),
            new PropertyMetadata(Stretch.Uniform, OnLayoutPropertyChanged));

    public static readonly DependencyProperty StretchDirectionProperty =
        DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(Svg),
            new PropertyMetadata(StretchDirection.Both, OnLayoutPropertyChanged));

    public static readonly DependencyProperty EnableCacheProperty =
        DependencyProperty.Register(
            nameof(EnableCache),
            typeof(bool),
            typeof(Svg),
            new PropertyMetadata(false, OnEnableCachePropertyChanged));

    public static readonly DependencyProperty WireframeProperty =
        DependencyProperty.Register(
            nameof(Wireframe),
            typeof(bool),
            typeof(Svg),
            new PropertyMetadata(false, OnRenderOptionPropertyChanged));

    public static readonly DependencyProperty DisableFiltersProperty =
        DependencyProperty.Register(
            nameof(DisableFilters),
            typeof(bool),
            typeof(Svg),
            new PropertyMetadata(false, OnRenderOptionPropertyChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(Svg),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PanXProperty =
        DependencyProperty.Register(
            nameof(PanX),
            typeof(double),
            typeof(Svg),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PanYProperty =
        DependencyProperty.Register(
            nameof(PanY),
            typeof(double),
            typeof(Svg),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty CssProperty =
        DependencyProperty.Register(
            nameof(Css),
            typeof(string),
            typeof(Svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty CurrentCssProperty =
        DependencyProperty.Register(
            nameof(CurrentCss),
            typeof(string),
            typeof(Svg),
            new PropertyMetadata(null, OnSourcePropertyChanged));

    public static readonly DependencyProperty AnimationBackendProperty =
        DependencyProperty.Register(
            nameof(AnimationBackend),
            typeof(SvgAnimationHostBackend),
            typeof(Svg),
            new PropertyMetadata(SvgAnimationHostBackend.Default, OnAnimationPlaybackPropertyChanged));

    public static readonly DependencyProperty AnimationFrameIntervalProperty =
        DependencyProperty.Register(
            nameof(AnimationFrameInterval),
            typeof(TimeSpan),
            typeof(Svg),
            new PropertyMetadata(TimeSpan.FromMilliseconds(16), OnAnimationPlaybackPropertyChanged));

    public static readonly DependencyProperty AnimationPlaybackRateProperty =
        DependencyProperty.Register(
            nameof(AnimationPlaybackRate),
            typeof(double),
            typeof(Svg),
            new PropertyMetadata(1.0, OnAnimationPlaybackPropertyChanged));

    public Svg()
    {
        PointerMoved += OnControlPointerMoved;
        PointerPressed += OnControlPointerPressed;
        PointerReleased += OnControlPointerReleased;
        PointerExited += OnControlPointerExited;
        PointerWheelChanged += OnControlPointerWheelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SvgSource? SvgSource
    {
        get => (SvgSource?)GetValue(SvgSourceProperty);
        set => SetValue(SvgSourceProperty, value);
    }

    public string? Path
    {
        get => (string?)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => (StretchDirection)GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public bool EnableCache
    {
        get => (bool)GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
    }

    public bool Wireframe
    {
        get => (bool)GetValue(WireframeProperty);
        set => SetValue(WireframeProperty, value);
    }

    public bool DisableFilters
    {
        get => (bool)GetValue(DisableFiltersProperty);
        set => SetValue(DisableFiltersProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PanX
    {
        get => (double)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => (double)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public string? Css
    {
        get => (string?)GetValue(CssProperty);
        set => SetValue(CssProperty, value);
    }

    public string? CurrentCss
    {
        get => (string?)GetValue(CurrentCssProperty);
        set => SetValue(CurrentCssProperty, value);
    }

    public SvgAnimationHostBackend AnimationBackend
    {
        get => (SvgAnimationHostBackend)GetValue(AnimationBackendProperty);
        set => SetValue(AnimationBackendProperty, value);
    }

    public TimeSpan AnimationFrameInterval
    {
        get => NormalizeAnimationFrameInterval((TimeSpan)GetValue(AnimationFrameIntervalProperty));
        set => SetValue(AnimationFrameIntervalProperty, NormalizeAnimationFrameInterval(value));
    }

    public double AnimationPlaybackRate
    {
        get => NormalizeAnimationPlaybackRate((double)GetValue(AnimationPlaybackRateProperty));
        set => SetValue(AnimationPlaybackRateProperty, NormalizeAnimationPlaybackRate(value));
    }

    public SvgAnimationHostBackend ActualAnimationBackend => _animationBackendResolution.ActualBackend;

    public string? AnimationBackendFallbackReason => _animationBackendResolution.FallbackReason;

    public SvgAnimationHostBackendResolution AnimationBackendResolution => _animationBackendResolution;

    public SvgAnimationHostBackendCapabilities AnimationBackendCapabilities => CreateAnimationBackendCapabilities();

    public SkiaPicture? Picture => _svg?.Picture;

    public SKSvg? SkSvg => _svg?.Svg;

    public void ZoomToPoint(double newZoom, Point point)
    {
        var result = SvgRenderLayout.ZoomToPoint(
            Zoom,
            PanX,
            PanY,
            newZoom,
            new SvgPoint(point.X, point.Y));

        PanX = result.PanX;
        PanY = result.PanY;
        Zoom = result.Zoom;
    }

    public bool TryGetPicturePoint(Point point, out ShimPoint picturePoint)
    {
        picturePoint = default;

        if (!TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        if (!renderInfo.TryMapToPicture(new SvgPoint(point.X, point.Y), out var mappedPoint))
        {
            return false;
        }

        picturePoint = new ShimPoint((float)mappedPoint.X, (float)mappedPoint.Y);
        return true;
    }

    public IEnumerable<SvgElement> HitTestElements(Point point)
    {
        if (SkSvg is { } skSvg && TryGetPicturePoint(point, out var picturePoint))
        {
            return skSvg.HitTestElements(picturePoint);
        }

        return Array.Empty<SvgElement>();
    }

    private void OnControlPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        DispatchPointerMoved(e);
    }

    private void OnControlPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        DispatchPointerPressed(e);
    }

    private void OnControlPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        DispatchPointerReleased(e);
    }

    private void OnControlPointerExited(object sender, PointerRoutedEventArgs e)
    {
        var result = Interaction.DispatchPointerExited(SkSvg, CreatePointerInput(e, SvgMouseButton.None, 0, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(null);
    }

    private void OnControlPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        DispatchPointerWheelChanged(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = _svg?.Picture;
        if (picture is null)
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(availableSize.Width, availableSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picture = _svg?.Picture;
        if (picture is null)
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(finalSize.Width, finalSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override void RenderOverride(SkiaCanvas canvas, Size area)
    {
        var source = _svg;
        if (source is null)
        {
            return;
        }

        var skSvg = source?.Svg;
        var picture = source?.Picture;
        if (skSvg is null || picture is null)
        {
            return;
        }

        if (!SvgRenderLayout.TryCreateRenderInfo(
                new SvgSize(area.Width, area.Height),
                new SvgRect(
                    picture.CullRect.Left,
                    picture.CullRect.Top,
                    picture.CullRect.Width,
                    picture.CullRect.Height),
                Stretch,
                StretchDirection,
                Zoom,
                PanX,
                PanY,
                out var renderInfo))
        {
            return;
        }

        if (!source!.BeginRender())
        {
            return;
        }

        try
        {
            using var restore = new SkiaAutoCanvasRestore(canvas, true);
            canvas.ClipRect(ToSKRect(renderInfo.DestinationRect));
            var matrix = ToSKMatrix(renderInfo.Matrix);
            canvas.Concat(in matrix);
            skSvg.Draw(canvas);
        }
        finally
        {
            source.EndRender();
        }
    }

    internal static SvgParameters? BuildParameters(SvgSource? source, string? css, string? currentCss)
    {
        var entities = source?.Parameters?.Entities is { } parametersEntities
            ? new Dictionary<string, string>(parametersEntities)
            : source?.Entities is { } sourceEntities
                ? new Dictionary<string, string>(sourceEntities)
                : null;

        var combinedCss = CombineCss(source?.Parameters?.Css ?? source?.Css, css, currentCss);
        return entities is null && string.IsNullOrWhiteSpace(combinedCss)
            ? null
            : new SvgParameters(entities, combinedCss);
    }

    internal static SvgSource PrepareWorkingSource(SvgSource source, string? css, string? currentCss, bool wireframe, bool disableFilters)
    {
        var clone = source.Clone();
        var parameters = BuildParameters(source, css, currentCss);

        if (parameters is not null)
        {
            if (clone.HasPathSource)
            {
                clone.ReLoadAsync(parameters).GetAwaiter().GetResult();
            }
            else if (clone.HasLoadedSource)
            {
                clone.ReLoad(parameters);
            }
        }

        ApplyRenderOptions(clone, wireframe, disableFilters);
        return clone;
    }

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Svg)d;
        control.Interaction.Reset();
        control.ApplyNativeCursor(null);
        control.QueueSourceReload();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Svg)d;
        control.InvalidateMeasure();
        control.InvalidateArrange();
        control.Invalidate();
    }

    private static void OnEnableCachePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Svg)d;
        if ((bool)e.NewValue)
        {
            control._cache ??= new Dictionary<string, SvgSource>(StringComparer.Ordinal);
        }
        else
        {
            control.DisposeCache();
        }
    }

    private static void OnRenderOptionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Svg)d;
        ApplyRenderOptions(control._svg, control.Wireframe, control.DisableFilters);
        control.Invalidate();
    }

    private static void OnAnimationPlaybackPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Svg)d;
        control.UpdateAnimationPlayback();
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

    private static SkiaRect ToSKRect(SvgRect rect)
    {
        return new SkiaRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    }

    private static SkiaMatrix ToSKMatrix(Matrix3x2 matrix)
    {
        return new SkiaMatrix
        {
            ScaleX = matrix.M11,
            SkewX = matrix.M21,
            TransX = matrix.M31,
            SkewY = matrix.M12,
            ScaleY = matrix.M22,
            TransY = matrix.M32,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    private static string? CombineCss(params string?[] values)
    {
        var filtered = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        return filtered.Length == 0 ? null : string.Join(" ", filtered);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TrackAnimationSvg(_svg?.Svg);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Interaction.Reset();
        ApplyNativeCursor(null);
        CancelPendingLoad();
        TrackAnimationSvg(null);
    }

    private void QueueSourceReload()
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        CancelPendingLoad();

        if (SvgSource is null && string.IsNullOrWhiteSpace(Path) && string.IsNullOrWhiteSpace(Source))
        {
            ClearSource();
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _pendingLoadCts = cancellationTokenSource;
        _ = ReloadSourceAsync(loadVersion, cancellationTokenSource.Token);
    }

    private async Task ReloadSourceAsync(long loadVersion, CancellationToken cancellationToken)
    {
        LoadResult result = default;

        try
        {
            if (SvgSource is { } externalSource)
            {
                result = await LoadExternalSourceAsync(externalSource, cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(Path))
            {
                result = await LoadPathAsync(Path!, cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(Source))
            {
                result = LoadInlineSource(Source!);
            }

            if (cancellationToken.IsCancellationRequested || loadVersion != Volatile.Read(ref _loadVersion))
            {
                DisposeResultIfOwned(result);
                return;
            }

            SetCurrentSource(result);
        }
        catch (OperationCanceledException)
        {
            DisposeResultIfOwned(result);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to load Uno svg control source.");
            Debug.WriteLine(e);
            DisposeResultIfOwned(result);
            ClearSource();
        }
    }

    private async Task<LoadResult> LoadExternalSourceAsync(SvgSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clone = source.Clone();
        var parameters = BuildParameters(source, Css, CurrentCss);

        if (clone.HasPathSource)
        {
            await clone.ReLoadAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        else if (clone.HasLoadedSource && parameters is not null)
        {
            clone.ReLoad(parameters);
        }

        ApplyRenderOptions(clone, Wireframe, DisableFilters);
        return new LoadResult(clone, false);
    }

    private async Task<LoadResult> LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(null, Css, CurrentCss);
        var normalizedPath = SvgSource.NormalizePath(path).ToString();
        var cacheKey = SvgCacheKey.Create(normalizedPath, parameters);

        if (EnableCache && _cache is { } cache && cache.TryGetValue(cacheKey, out var cached))
        {
            var workingSource = CreateWorkingSource(cached);
            ApplyRenderOptions(workingSource, Wireframe, DisableFilters);
            return new LoadResult(workingSource, ReferenceEquals(workingSource, cached));
        }

        var source = await SvgSource.LoadAsync(path, parameters: parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ApplyRenderOptions(source, Wireframe, DisableFilters);

        if (EnableCache && _cache is { } cacheStore)
        {
            cacheStore[cacheKey] = CreateWorkingSource(source);
            return new LoadResult(source, false);
        }

        return new LoadResult(source, false);
    }

    private LoadResult LoadInlineSource(string sourceText)
    {
        var parameters = BuildParameters(null, Css, CurrentCss);
        var source = SvgSource.LoadFromSvg(sourceText, parameters);
        ApplyRenderOptions(source, Wireframe, DisableFilters);
        return new LoadResult(source, false);
    }

    private bool TryGetRenderInfo(out SvgRenderInfo renderInfo)
    {
        renderInfo = default;

        var picture = _svg?.Picture;
        if (picture is null)
        {
            return false;
        }

        return SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(ActualWidth, ActualHeight),
            new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection,
            Zoom,
            PanX,
            PanY,
            out renderInfo);
    }

    private void SetCurrentSource(LoadResult result)
    {
        var previous = _svg;
        _svg = result.Source;
        TrackAnimationSvg(_svg?.Svg);

        if (previous is not null && !ReferenceEquals(previous, _svg) && !IsCached(previous))
        {
            previous.Dispose();
        }

        InvalidateMeasure();
        InvalidateArrange();
        Invalidate();
    }

    private void ClearSource()
    {
        CancelPendingLoad();
        Interaction.Reset();

        var previous = _svg;
        _svg = null;
        TrackAnimationSvg(null);

        if (previous is not null && !IsCached(previous))
        {
            previous.Dispose();
        }

        DisposeCache();
        InvalidateMeasure();
        InvalidateArrange();
        Invalidate();
    }

    private void CancelPendingLoad()
    {
        _pendingLoadCts?.Cancel();
        _pendingLoadCts?.Dispose();
        _pendingLoadCts = null;
    }

    private void DisposeCache()
    {
        if (_cache is null)
        {
            return;
        }

        foreach (var cached in _cache.Values)
        {
            if (!ReferenceEquals(cached, _svg))
            {
                cached.Dispose();
            }
        }

        _cache = null;
    }

    private static SvgSource CreateWorkingSource(SvgSource source)
    {
        return source.Svg?.HasAnimations == true ? source.Clone() : source;
    }

    private bool IsCached(SvgSource source)
    {
        return _cache is { } cache && cache.Values.Any(value => ReferenceEquals(value, source));
    }

    private void DisposeResultIfOwned(LoadResult result)
    {
        if (result.Source is not null && !result.IsCacheEntry)
        {
            result.Source.Dispose();
        }
    }

    private readonly record struct LoadResult(SvgSource? Source, bool IsCacheEntry);

    private void DispatchPointerMoved(PointerRoutedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetCurrentPoint(this).Position, out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var result = Interaction.DispatchPointerMoved(skSvg, CreatePointerInput(e, SvgMouseButton.None, 0, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private void DispatchPointerPressed(PointerRoutedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetCurrentPoint(this).Position, out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        CapturePointer(e.Pointer);
        var point = e.GetCurrentPoint(this);
        var button = MapPointerUpdateKind(point.Properties.PointerUpdateKind);
        var result = Interaction.DispatchPointerPressed(skSvg, CreatePointerInput(e, button, 1, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private void DispatchPointerReleased(PointerRoutedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetCurrentPoint(this).Position, out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var point = e.GetCurrentPoint(this);
        var button = MapPointerUpdateKind(point.Properties.PointerUpdateKind);
        var result = Interaction.DispatchPointerReleased(skSvg, CreatePointerInput(e, button, 1, 0));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
        ReleasePointerCapture(e.Pointer);
    }

    private void DispatchPointerWheelChanged(PointerRoutedEventArgs e)
    {
        if (_svg?.Svg is not { } skSvg || !TryGetPicturePoint(e.GetCurrentPoint(this).Position, out _))
        {
            ApplyNativeCursor(null);
            return;
        }

        var wheelDelta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        var result = Interaction.DispatchPointerWheelChanged(skSvg, CreatePointerInput(e, SvgMouseButton.None, 0, wheelDelta));
        e.Handled |= result.Handled;
        ApplyNativeCursor(result.Cursor);
    }

    private SvgPointerInput CreatePointerInput(PointerRoutedEventArgs e, SvgMouseButton button, int clickCount, int wheelDelta)
    {
        if (!TryGetPicturePoint(e.GetCurrentPoint(this).Position, out var picturePoint))
        {
            picturePoint = default;
        }

        return new SvgPointerInput(
            picturePoint,
            MapPointerType(e.Pointer.PointerDeviceType),
            button,
            clickCount,
            wheelDelta,
            e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu),
            e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift),
            e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control),
            e.Pointer.PointerId.ToString());
    }

    private static SvgPointerDeviceType MapPointerType(PointerDeviceType pointerType)
    {
        return pointerType switch
        {
            PointerDeviceType.Mouse => SvgPointerDeviceType.Mouse,
            PointerDeviceType.Touch => SvgPointerDeviceType.Touch,
            PointerDeviceType.Pen => SvgPointerDeviceType.Pen,
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

    private void ApplyNativeCursor(string? svgCursor)
    {
        ProtectedCursor = ResolveNativeCursor(svgCursor);
    }

    private static InputSystemCursor? ResolveNativeCursor(string? svgCursor)
    {
        return NormalizeSvgCursorKeyword(svgCursor) switch
        {
            null => null,
            "auto" or "default" or "arrow" => s_arrowCursor,
            "pointer" or "hand" => s_handCursor,
            "text" or "ibeam" => s_iBeamCursor,
            "crosshair" => s_crossCursor,
            "help" => s_helpCursor,
            "wait" => s_waitCursor,
            "progress" => s_appStartingCursor,
            "move" or "all-scroll" or "grab" or "grabbing" => s_sizeAllCursor,
            "ew-resize" or "col-resize" or "e-resize" or "w-resize" => s_sizeWestEastCursor,
            "ns-resize" or "row-resize" or "n-resize" or "s-resize" => s_sizeNorthSouthCursor,
            "ne-resize" or "sw-resize" or "nesw-resize" => s_sizeNorthEastSouthWestCursor,
            "nw-resize" or "se-resize" or "nwse-resize" => s_sizeNorthWestSouthEastCursor,
            "not-allowed" or "no-drop" => s_universalNoCursor,
            "up-arrow" => s_upArrowCursor,
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
        var isHostReady = XamlRoot is not null && IsLoaded;
        return new SvgAnimationHostBackendCapabilities(
            isHostReady,
            isHostReady && DispatcherQueue is not null,
            isHostReady,
            supportsNativeComposition: false);
    }

    private void UpdateAnimationPlayback()
    {
        if (DispatcherQueue is { HasThreadAccess: false } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(UpdateAnimationPlayback);
            return;
        }

        StopAnimationPlaybackDrivers();

        var capabilities = CreateAnimationBackendCapabilities();
        var hasAnimations = _trackedAnimationSvg?.HasAnimations == true;
        _animationBackendResolution = SvgAnimationHostBackendResolver.Resolve(AnimationBackend, capabilities, hasAnimations);

        if (_trackedAnimationSvg is not { } skSvg)
        {
            return;
        }

        skSvg.AnimationMinimumRenderInterval = _animationBackendResolution.ActualBackend == SvgAnimationHostBackend.Manual
            ? TimeSpan.Zero
            : NormalizeAnimationFrameInterval(AnimationFrameInterval);

        ResetAnimationPlaybackClock();

        switch (_animationBackendResolution.ActualBackend)
        {
            case SvgAnimationHostBackend.DispatcherTimer:
                {
                    StartDispatcherTimer();
                    break;
                }
            case SvgAnimationHostBackend.RenderLoop:
                {
                    StartRenderLoop();
                    break;
                }
        }
    }

    private void StartDispatcherTimer()
    {
        if (DispatcherQueue is not { } dispatcherQueue)
        {
            return;
        }

        _animationDispatcherTimer ??= CreateAnimationDispatcherTimer(dispatcherQueue);
        _animationDispatcherTimer.Interval = NormalizeAnimationFrameInterval(AnimationFrameInterval);
        _animationDispatcherTimer.Start();
    }

    private void StartRenderLoop()
    {
        if (_animationRenderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering += OnCompositionTargetRendering;
        _animationRenderingSubscribed = true;
    }

    private DispatcherQueueTimer CreateAnimationDispatcherTimer(DispatcherQueue dispatcherQueue)
    {
        var timer = dispatcherQueue.CreateTimer();
        timer.IsRepeating = true;
        timer.Tick += OnAnimationDispatcherTimerTick;
        return timer;
    }

    private void StopAnimationPlaybackDrivers()
    {
        if (_animationDispatcherTimer is { } dispatcherTimer)
        {
            dispatcherTimer.Stop();
        }

        if (_animationRenderingSubscribed)
        {
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            _animationRenderingSubscribed = false;
        }

        if (_trackedAnimationSvg is { } skSvg)
        {
            skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        }
    }

    private void OnAnimationDispatcherTimerTick(DispatcherQueueTimer sender, object args)
    {
        TickAnimationPlayback();
    }

    private void OnCompositionTargetRendering(object? sender, object args)
    {
        TickAnimationPlayback();
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

    private void OnAnimationInvalidated(object? sender, SvgAnimationFrameChangedEventArgs e)
    {
        if (DispatcherQueue is { } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                InvalidateMeasure();
                InvalidateArrange();
                Invalidate();
            });
            return;
        }

        InvalidateMeasure();
        InvalidateArrange();
        Invalidate();
    }
}
