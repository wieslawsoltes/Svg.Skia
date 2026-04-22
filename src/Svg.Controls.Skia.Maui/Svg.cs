using System.Diagnostics;
using System.Numerics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Svg;
using Svg.Model;
using Svg.Skia;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaAutoCanvasRestore = SkiaSharp.SKAutoCanvasRestore;
using SkiaCanvas = SkiaSharp.SKCanvas;
using SkiaMatrix = SkiaSharp.SKMatrix;
using SkiaPicture = SkiaSharp.SKPicture;
using SkiaRect = SkiaSharp.SKRect;

namespace Maui.Svg.Skia;

[ContentProperty(nameof(Path))]
public sealed class Svg : SkiaSharp.Views.Maui.Controls.SKCanvasView
{
    private SvgSource? _svg;
    private Dictionary<string, SvgSource>? _cache;
    private CancellationTokenSource? _pendingLoadCts;
    private long _loadVersion;
    private SKSvg? _trackedAnimationSvg;
    private readonly Stopwatch _animationPlaybackStopwatch = new();
    private IDispatcherTimer? _animationDispatcherTimer;
    private SvgAnimationHostBackendResolution _animationBackendResolution =
        new(SvgAnimationHostBackend.Default, SvgAnimationHostBackend.Default, null);
    private TimeSpan _lastAnimationPlaybackTimestamp;

    public static readonly BindableProperty SvgSourceProperty =
        BindableProperty.Create(
            nameof(SvgSource),
            typeof(SvgSource),
            typeof(Svg),
            null,
            propertyChanged: OnSourcePropertyChanged);

    public static readonly BindableProperty PathProperty =
        BindableProperty.Create(
            nameof(Path),
            typeof(string),
            typeof(Svg),
            null,
            propertyChanged: OnSourcePropertyChanged);

    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(
            nameof(Source),
            typeof(string),
            typeof(Svg),
            null,
            propertyChanged: OnSourcePropertyChanged);

    public static readonly BindableProperty StretchProperty =
        BindableProperty.Create(
            nameof(Stretch),
            typeof(Stretch),
            typeof(Svg),
            Stretch.Uniform,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty StretchDirectionProperty =
        BindableProperty.Create(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(Svg),
            StretchDirection.Both,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty EnableCacheProperty =
        BindableProperty.Create(
            nameof(EnableCache),
            typeof(bool),
            typeof(Svg),
            false,
            propertyChanged: OnEnableCachePropertyChanged);

    public static readonly BindableProperty WireframeProperty =
        BindableProperty.Create(
            nameof(Wireframe),
            typeof(bool),
            typeof(Svg),
            false,
            propertyChanged: OnRenderOptionPropertyChanged);

    public static readonly BindableProperty DisableFiltersProperty =
        BindableProperty.Create(
            nameof(DisableFilters),
            typeof(bool),
            typeof(Svg),
            false,
            propertyChanged: OnRenderOptionPropertyChanged);

    public static readonly BindableProperty ZoomProperty =
        BindableProperty.Create(
            nameof(Zoom),
            typeof(double),
            typeof(Svg),
            1.0,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty PanXProperty =
        BindableProperty.Create(
            nameof(PanX),
            typeof(double),
            typeof(Svg),
            0.0,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty PanYProperty =
        BindableProperty.Create(
            nameof(PanY),
            typeof(double),
            typeof(Svg),
            0.0,
            propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty CssProperty =
        BindableProperty.Create(
            nameof(Css),
            typeof(string),
            typeof(Svg),
            null,
            propertyChanged: OnSourcePropertyChanged);

    public static readonly BindableProperty CurrentCssProperty =
        BindableProperty.Create(
            nameof(CurrentCss),
            typeof(string),
            typeof(Svg),
            null,
            propertyChanged: OnSourcePropertyChanged);

    public static readonly BindableProperty AnimationBackendProperty =
        BindableProperty.Create(
            nameof(AnimationBackend),
            typeof(SvgAnimationHostBackend),
            typeof(Svg),
            SvgAnimationHostBackend.Default,
            propertyChanged: OnAnimationPlaybackPropertyChanged);

    public static readonly BindableProperty AnimationFrameIntervalProperty =
        BindableProperty.Create(
            nameof(AnimationFrameInterval),
            typeof(TimeSpan),
            typeof(Svg),
            TimeSpan.FromMilliseconds(16),
            propertyChanged: OnAnimationPlaybackPropertyChanged);

    public static readonly BindableProperty AnimationPlaybackRateProperty =
        BindableProperty.Create(
            nameof(AnimationPlaybackRate),
            typeof(double),
            typeof(Svg),
            1.0,
            propertyChanged: OnAnimationPlaybackPropertyChanged);

    public Svg()
    {
        PaintSurface += OnPaintSurface;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
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

    public void ZoomToPoint(double newZoom, Microsoft.Maui.Graphics.Point point)
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

    public bool TryGetPicturePoint(Microsoft.Maui.Graphics.Point point, out ShimPoint picturePoint)
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

    public IEnumerable<SvgElement> HitTestElements(Microsoft.Maui.Graphics.Point point)
    {
        if (SkSvg is { } skSvg && TryGetPicturePoint(point, out var picturePoint))
        {
            return skSvg.HitTestElements(picturePoint);
        }

        return Array.Empty<SvgElement>();
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var picture = _svg?.Picture;
        if (picture is null)
        {
            return new Size();
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(widthConstraint, heightConstraint),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
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

    private static void OnSourcePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (Svg)bindable;
        control.QueueSourceReload();
    }

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (Svg)bindable;
        control.InvalidateControl();
    }

    private static void OnEnableCachePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (Svg)bindable;
        if ((bool)newValue)
        {
            control._cache ??= new Dictionary<string, SvgSource>(StringComparer.Ordinal);
        }
        else
        {
            control.DisposeCache();
        }
    }

    private static void OnRenderOptionPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (Svg)bindable;
        ApplyRenderOptions(control._svg, control.Wireframe, control.DisableFilters);
        control.InvalidateSurface();
    }

    private static void OnAnimationPlaybackPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (Svg)bindable;
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

    private void OnPaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
    {
        Render(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    private void Render(SkiaCanvas canvas, int width, int height)
    {
        var source = _svg;
        if (source is null)
        {
            return;
        }

        if (!source.BeginRender())
        {
            return;
        }

        try
        {
            var skSvg = source.Svg;
            var picture = source.Picture;
            if (skSvg is null || picture is null)
            {
                return;
            }

            if (!SvgRenderLayout.TryCreateRenderInfo(
                    new SvgSize(width, height),
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

            using var restore = new SkiaAutoCanvasRestore(canvas, true);
            canvas.ClipRect(ToSKRect(renderInfo.DestinationRect));
            var matrix = ToSKMatrix(renderInfo.Matrix);
            canvas.Concat(ref matrix);
            skSvg.Draw(canvas);
        }
        finally
        {
            source.EndRender();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        TrackAnimationSvg(_svg?.Svg);
        QueueSourceReload();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CancelPendingLoad();
        TrackAnimationSvg(null);
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        InvalidateSurface();
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

            ApplyLoadResult(loadVersion, result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            DisposeResultIfOwned(result);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to load MAUI svg control source.");
            Debug.WriteLine(e);
            DisposeResultIfOwned(result);

            if (!cancellationToken.IsCancellationRequested && loadVersion == Volatile.Read(ref _loadVersion))
            {
                DispatchOnUiThread(ClearSource);
            }
        }
    }

    private async Task<LoadResult> LoadExternalSourceAsync(SvgSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SvgSource? clone = null;
        try
        {
            clone = source.Clone();
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
            return new LoadResult(clone);
        }
        catch
        {
            clone?.Dispose();
            throw;
        }
    }

    private async Task<LoadResult> LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(null, Css, CurrentCss);
        var normalizedPath = SvgSource.NormalizePath(path).ToString();
        var cacheKey = SvgCacheKey.Create(normalizedPath, parameters);
        var cache = EnableCache ? (_cache ??= new Dictionary<string, SvgSource>(StringComparer.Ordinal)) : null;

        if (cache is { } && cache.TryGetValue(cacheKey, out var cached))
        {
            var workingSource = CreateWorkingSource(cached);
            ApplyRenderOptions(workingSource, Wireframe, DisableFilters);
            return new LoadResult(workingSource);
        }

        var source = await SvgSource.LoadAsync(path, parameters: parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ApplyRenderOptions(source, Wireframe, DisableFilters);

        cache = EnableCache ? (_cache ??= new Dictionary<string, SvgSource>(StringComparer.Ordinal)) : null;
        if (cache is { })
        {
            var cacheEntry = CreateWorkingSource(source);
            cache[cacheKey] = cacheEntry;
            return new LoadResult(source);
        }

        return new LoadResult(source);
    }

    private LoadResult LoadInlineSource(string sourceText)
    {
        var parameters = BuildParameters(null, Css, CurrentCss);
        var source = SvgSource.LoadFromSvg(sourceText, parameters);
        ApplyRenderOptions(source, Wireframe, DisableFilters);
        return new LoadResult(source);
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
            new SvgSize(Width, Height),
            new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection,
            Zoom,
            PanX,
            PanY,
            out renderInfo);
    }

    private void ApplyLoadResult(long loadVersion, LoadResult result, CancellationToken cancellationToken)
    {
        void Apply()
        {
            if (cancellationToken.IsCancellationRequested || loadVersion != Volatile.Read(ref _loadVersion))
            {
                DisposeResultIfOwned(result);
                return;
            }

            SetCurrentSource(result);
        }

        DispatchOnUiThread(Apply);
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

        InvalidateControl();
    }

    private void ClearSource()
    {
        CancelPendingLoad();

        var previous = _svg;
        _svg = null;
        TrackAnimationSvg(null);

        if (previous is not null && !IsCached(previous))
        {
            previous.Dispose();
        }

        DisposeCache();
        InvalidateControl();
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
        if (result.Source is not null && !IsCached(result.Source))
        {
            result.Source.Dispose();
        }
    }

    private readonly record struct LoadResult(SvgSource? Source);

    private void TrackAnimationSvg(SKSvg? skSvg)
    {
        if (ReferenceEquals(_trackedAnimationSvg, skSvg))
        {
            return;
        }

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
        var isHostReady = IsLoaded;
        return new SvgAnimationHostBackendCapabilities(
            isHostReady,
            isHostReady && Dispatcher is not null,
            supportsRenderLoop: false,
            supportsNativeComposition: false);
    }

    private void UpdateAnimationPlayback()
    {
        DispatchOnUiThread(() =>
        {
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

            if (_animationBackendResolution.ActualBackend == SvgAnimationHostBackend.DispatcherTimer)
            {
                StartDispatcherTimer();
            }
        });
    }

    private void StartDispatcherTimer()
    {
        if (Dispatcher is null)
        {
            return;
        }

        _animationDispatcherTimer ??= CreateAnimationDispatcherTimer();
        if (_animationDispatcherTimer is null)
        {
            return;
        }

        _animationDispatcherTimer.Interval = NormalizeAnimationFrameInterval(AnimationFrameInterval);
        _animationDispatcherTimer.Start();
    }

    private IDispatcherTimer? CreateAnimationDispatcherTimer()
    {
        var timer = Dispatcher?.CreateTimer();
        if (timer is null)
        {
            return null;
        }

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

        if (_trackedAnimationSvg is { } skSvg)
        {
            skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        }
    }

    private void OnAnimationDispatcherTimerTick(object? sender, EventArgs e)
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
        DispatchOnUiThread(InvalidateControl);
    }

    private void InvalidateControl()
    {
        InvalidateMeasure();
        InvalidateSurface();
    }

    private void DispatchOnUiThread(Action action)
    {
        if (Dispatcher is { } dispatcher)
        {
            if (dispatcher.IsDispatchRequired)
            {
                if (dispatcher.Dispatch(action))
                {
                    return;
                }
            }
            else
            {
                action();
                return;
            }
        }

        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        MainThread.BeginInvokeOnMainThread(action);
    }
}
