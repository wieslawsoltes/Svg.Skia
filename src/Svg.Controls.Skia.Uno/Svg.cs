using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
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

    public Svg()
    {
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

    public SvgDocument? Document
    {
        get
        {
            var root = (SkSvg?.Drawable as DrawableBase)?.Element;
            return root switch
            {
                SvgDocument svgDocument => svgDocument,
                SvgElement element => element.OwnerDocument,
                _ => null
            };
        }
    }

    public bool TryGetViewPoint(ShimPoint picturePoint, out Point viewPoint)
    {
        viewPoint = default;

        if (!TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        var mapped = Vector2.Transform(new Vector2(picturePoint.X, picturePoint.Y), renderInfo.Matrix);
        viewPoint = new Point(mapped.X, mapped.Y);
        return true;
    }

    public bool TryGetViewMatrix(out Matrix3x2 matrix)
    {
        matrix = default;

        if (!TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        matrix = renderInfo.Matrix;
        return true;
    }

    public bool ReloadFromDocument(SvgDocument document)
    {
        if (_svg?.Svg is not { } skSvg)
        {
            return false;
        }

        var previousPicture = _svg.Picture;
        skSvg.FromSvgDocument(document);
        var currentPicture = skSvg.Picture;
        _svg.Picture = currentPicture;
        InvalidateAfterPictureChange(previousPicture, currentPicture);
        return true;
    }

    public bool RebuildFromModel()
    {
        if (_svg is null)
        {
            return false;
        }

        var previousPicture = _svg.Picture;
        _svg.RebuildFromModel();
        InvalidateAfterPictureChange(previousPicture, _svg.Picture);
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!TryGetLayoutBounds(out var layoutBounds))
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(availableSize.Width, availableSize.Height),
            new SvgSize(layoutBounds.Width, layoutBounds.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!TryGetLayoutBounds(out var layoutBounds))
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(finalSize.Width, finalSize.Height),
            new SvgSize(layoutBounds.Width, layoutBounds.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override void RenderOverride(SkiaCanvas canvas, Size area)
    {
        var source = _svg;
        if (source?.Svg is null || source.Picture is null)
        {
            return;
        }

        if (!SvgRenderLayout.TryCreateRenderInfo(
                new SvgSize(area.Width, area.Height),
                GetLayoutBounds(source.Picture),
                Stretch,
                StretchDirection,
                Zoom,
                PanX,
                PanY,
                out var renderInfo))
        {
            return;
        }

        if (!source.BeginRender())
        {
            return;
        }

        try
        {
            using var restore = new SkiaAutoCanvasRestore(canvas, true);
            canvas.ClipRect(new SkiaRect(0f, 0f, (float)area.Width, (float)area.Height));
            var matrix = ToSKMatrix(renderInfo.Matrix);
            canvas.Concat(in matrix);
            source.Svg.Draw(canvas);
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
        ((Svg)d).QueueSourceReload();
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

    private static bool HavePictureBoundsChanged(SkiaPicture? previousPicture, SkiaPicture? currentPicture)
    {
        if (previousPicture is null || currentPicture is null)
        {
            return !ReferenceEquals(previousPicture, currentPicture);
        }

        return !previousPicture.CullRect.Equals(currentPicture.CullRect);
    }

    private void InvalidateAfterPictureChange(SkiaPicture? previousPicture, SkiaPicture? currentPicture)
    {
        if (HavePictureBoundsChanged(previousPicture, currentPicture))
        {
            InvalidateMeasure();
            InvalidateArrange();
        }

        Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPendingLoad();
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
            ApplyRenderOptions(cached, Wireframe, DisableFilters);
            return new LoadResult(cached, true);
        }

        var source = await SvgSource.LoadAsync(path, parameters: parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ApplyRenderOptions(source, Wireframe, DisableFilters);

        if (EnableCache && _cache is { } cacheStore)
        {
            cacheStore[cacheKey] = source;
            return new LoadResult(source, true);
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

        if (!TryGetLayoutBounds(out var layoutBounds))
        {
            return false;
        }

        return SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(ActualWidth, ActualHeight),
            layoutBounds,
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

        var previous = _svg;
        _svg = null;

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

    private bool TryGetLayoutBounds(out SvgRect bounds)
    {
        bounds = default;

        if (_svg?.Picture is not { } picture)
        {
            return false;
        }

        bounds = GetLayoutBounds(picture);
        return bounds.Width > 0.0 && bounds.Height > 0.0;
    }

    private SvgRect GetLayoutBounds(SkiaPicture picture)
    {
        if (Document is { ViewBox.Width: > 0f, ViewBox.Height: > 0f } document)
        {
            return new SvgRect(
                document.ViewBox.MinX,
                document.ViewBox.MinY,
                document.ViewBox.Width,
                document.ViewBox.Height);
        }

        return new SvgRect(
            picture.CullRect.Left,
            picture.CullRect.Top,
            picture.CullRect.Width,
            picture.CullRect.Height);
    }
}
