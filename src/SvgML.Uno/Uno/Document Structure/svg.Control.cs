using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia;
using Windows.Foundation;
using ShimMatrix = ShimSkiaSharp.SKMatrix;
using ShimPoint = ShimSkiaSharp.SKPoint;
using ShimRect = ShimSkiaSharp.SKRect;
using SkiaCanvas = SkiaSharp.SKCanvas;
using SkiaMatrix = SkiaSharp.SKMatrix;
using SkiaPicture = SkiaSharp.SKPicture;
using SkiaRect = SkiaSharp.SKRect;

namespace SvgML;

public partial class svg
{
    private static readonly TimeSpan s_animationFrameInterval = TimeSpan.FromMilliseconds(16);

    private SkiaPicture? _picture;
    private SKSvg? _skSvg;
    private SvgDocument? _svgDocument;
    private bool _reloadQueued;
    private readonly Stopwatch _animationPlaybackStopwatch = new();
    private DispatcherQueueTimer? _animationTimer;
    private SKSvg? _trackedAnimationSvg;
    private TimeSpan _lastAnimationPlaybackTimestamp;
    private readonly Dictionary<SvgElement, element> _elementBySvgElement = new();
    private readonly Dictionary<SvgSceneNode, element> _elementBySceneNode = new();
    private Size _arrangedSvgSize;

    static svg()
    {
        Initialize();
    }

    public svg()
    {
        InitializeHostedControls();
        AttachToTree(parent: null, root: this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SkiaPicture? Picture => _picture;

    public SKSvg? SkSvg => _skSvg;

    private static void Initialize()
    {
    }

    internal void InvalidateSvgTree()
    {
        QueueReload();
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

    public bool TryGetPictureRect(Rect rect, out ShimRect pictureRect)
    {
        pictureRect = default;

        if (!TryGetRenderInfo(out var renderInfo)
            || !renderInfo.TryMapToPicture(new SvgRect(rect.X, rect.Y, rect.Width, rect.Height), out var mappedRect))
        {
            return false;
        }

        pictureRect = new ShimRect(
            (float)mappedRect.Left,
            (float)mappedRect.Top,
            (float)mappedRect.Right,
            (float)mappedRect.Bottom);
        return true;
    }

    public IEnumerable<SvgElement> HitTestSvgElements(ShimPoint point)
    {
        if (_skSvg is null)
        {
            return Array.Empty<SvgElement>();
        }

        return _skSvg.HitTestElements(point);
    }

    public IEnumerable<SvgElement> HitTestSvgElements(ShimRect rect)
    {
        if (_skSvg is null)
        {
            return Array.Empty<SvgElement>();
        }

        return _skSvg.HitTestElements(rect);
    }

    public IEnumerable<SvgElement> HitTestSvgElements(Point point)
    {
        if (_skSvg is { } skSvg && TryGetPicturePoint(point, out var picturePoint))
        {
            return skSvg.HitTestElements(picturePoint);
        }

        return Array.Empty<SvgElement>();
    }

    public IEnumerable<SvgElement> HitTestSvgElements(Rect rect)
    {
        if (_skSvg is { } skSvg && TryGetPictureRect(rect, out var pictureRect))
        {
            return skSvg.HitTestElements(pictureRect);
        }

        return Array.Empty<SvgElement>();
    }

    public IEnumerable<element> HitTestElements(ShimPoint point)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        var visited = new HashSet<element>();
        foreach (var svgElement in _skSvg.HitTestElements(point))
        {
            if (_elementBySvgElement.TryGetValue(svgElement, out var control) && visited.Add(control))
            {
                yield return control;
            }
        }
    }

    public IEnumerable<element> HitTestElements(ShimRect rect)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        var visited = new HashSet<element>();
        foreach (var svgElement in _skSvg.HitTestElements(rect))
        {
            if (_elementBySvgElement.TryGetValue(svgElement, out var control) && visited.Add(control))
            {
                yield return control;
            }
        }
    }

    public IEnumerable<element> HitTestElements(Point point)
    {
        if (!TryGetPicturePoint(point, out var picturePoint))
        {
            yield break;
        }

        foreach (var element in HitTestElements(picturePoint))
        {
            yield return element;
        }
    }

    public IEnumerable<element> HitTestElements(Rect rect)
    {
        if (!TryGetPictureRect(rect, out var pictureRect))
        {
            yield break;
        }

        foreach (var element in HitTestElements(pictureRect))
        {
            yield return element;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(ShimPoint point)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        foreach (var sceneNode in _skSvg.HitTestSceneNodes(point))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(ShimRect rect)
    {
        if (_skSvg is null)
        {
            yield break;
        }

        foreach (var sceneNode in _skSvg.HitTestSceneNodes(rect))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(Point point)
    {
        if (!TryGetPicturePoint(point, out var picturePoint))
        {
            yield break;
        }

        foreach (var sceneNode in HitTestSceneNodes(picturePoint))
        {
            yield return sceneNode;
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(Rect rect)
    {
        if (!TryGetPictureRect(rect, out var pictureRect))
        {
            yield break;
        }

        foreach (var sceneNode in HitTestSceneNodes(pictureRect))
        {
            yield return sceneNode;
        }
    }

    public Rect GetControlBounds(element element)
    {
        if (element is null || !TryGetRenderInfo(out var renderInfo))
        {
            return default;
        }

        return element.GetControlBounds(renderInfo.Matrix);
    }

    public element? GetElementForSceneNode(SvgSceneNode? sceneNode)
    {
        if (sceneNode is null)
        {
            return null;
        }

        return _elementBySceneNode.TryGetValue(sceneNode, out var control)
            ? control
            : null;
    }

    public element? GetElementForSvgElement(SvgElement? svgElement)
    {
        if (svgElement is null)
        {
            return null;
        }

        return _elementBySvgElement.TryGetValue(svgElement, out var control)
            ? control
            : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = _picture;
        if (picture is null)
        {
            return default;
        }

        _layoutRoot.Measure(availableSize);

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(availableSize.Width, availableSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picture = _picture;
        if (picture is null)
        {
            _arrangedSvgSize = default;
            _layoutRoot.Arrange(new Rect(0D, 0D, 0D, 0D));
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(finalSize.Width, finalSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        _arrangedSvgSize = new Size(size.Width, size.Height);
        _layoutRoot.Arrange(new Rect(0D, 0D, size.Width, size.Height));
        ArrangeHostedControls();
        return new Size(size.Width, size.Height);
    }

    private void QueueReload()
    {
        if (_reloadQueued)
        {
            return;
        }

        _reloadQueued = true;

        if (DispatcherQueue is { } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                _reloadQueued = false;
                ReloadAndInvalidate();
            });

            return;
        }

        _reloadQueued = false;
        ReloadAndInvalidate();
    }

    private void ReloadAndInvalidate()
    {
        ReloadFromInlineTree();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateDrawingSurface();
    }

    private void UpdateAnimationPlayback()
    {
        if (DispatcherQueue is { HasThreadAccess: false } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(UpdateAnimationPlayback);
            return;
        }

        StopAnimationPlayback();

        if (!IsLoaded || _skSvg is not { HasAnimations: true } skSvg)
        {
            return;
        }

        _trackedAnimationSvg = skSvg;
        skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        skSvg.AnimationInvalidated += OnAnimationInvalidated;

        if (DispatcherQueue is not { } dispatcherQueueWithTimer)
        {
            return;
        }

        _animationTimer ??= CreateAnimationTimer(dispatcherQueueWithTimer);
        _lastAnimationPlaybackTimestamp = TimeSpan.Zero;
        _animationPlaybackStopwatch.Restart();
        _animationTimer.Interval = s_animationFrameInterval;
        _animationTimer.Start();
    }

    private DispatcherQueueTimer CreateAnimationTimer(DispatcherQueue dispatcherQueue)
    {
        var timer = dispatcherQueue.CreateTimer();
        timer.IsRepeating = true;
        timer.Tick += OnAnimationTimerTick;
        return timer;
    }

    private void StopAnimationPlayback()
    {
        _animationTimer?.Stop();

        if (_trackedAnimationSvg is { } skSvg)
        {
            skSvg.AnimationInvalidated -= OnAnimationInvalidated;
            skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        }

        _trackedAnimationSvg = null;
        _lastAnimationPlaybackTimestamp = TimeSpan.Zero;
        _animationPlaybackStopwatch.Reset();
    }

    private void OnAnimationTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_trackedAnimationSvg is not { HasAnimations: true } skSvg)
        {
            return;
        }

        var currentTimestamp = _animationPlaybackStopwatch.Elapsed;
        var delta = currentTimestamp - _lastAnimationPlaybackTimestamp;
        _lastAnimationPlaybackTimestamp = currentTimestamp;

        if (delta > TimeSpan.Zero)
        {
            skSvg.AdvanceAnimation(delta);
        }
    }

    private void OnAnimationInvalidated(object? sender, SvgAnimationFrameChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _trackedAnimationSvg))
        {
            return;
        }

        void Refresh()
        {
            if (_trackedAnimationSvg is not { } skSvg)
            {
                return;
            }

            _picture = skSvg.Picture;
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateDrawingSurface();
        }

        if (DispatcherQueue is { HasThreadAccess: false } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(Refresh);
            return;
        }

        Refresh();
    }

    private void ReloadFromInlineTree()
    {
        try
        {
            var markup = ToSvgString(this);
            using var stream = GenerateStreamFromString(markup);
            _ = LoadFromStream(stream, BuildParameters(Css, CurrentCss));
        }
        catch
        {
            ClearLoadedData();
        }
    }

    private static Stream GenerateStreamFromString(string value)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static SvgParameters? BuildParameters(string? css, string? currentCss)
    {
        var combinedCss = CombineCss(css, currentCss);
        return string.IsNullOrWhiteSpace(combinedCss) ? null : new SvgParameters(null, combinedCss);
    }

    private static string? CombineCss(params string?[] values)
    {
        var filtered = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        return filtered.Length == 0 ? null : string.Join(" ", filtered);
    }

    private bool LoadFromStream(Stream stream, SvgParameters? parameters)
    {
        var skSvg = new SKSvg();
        var picture = skSvg.Load(stream, parameters);
        var svgDocument = skSvg.SourceDocument;
        if (picture is null || svgDocument is null)
        {
            skSvg.Dispose();
            ClearLoadedData();
            return false;
        }

        SKSvg? previousSvg;

        lock (this)
        {
            previousSvg = _skSvg;
            _skSvg = skSvg;
            _picture = picture;
            _svgDocument = svgDocument;
        }

        previousSvg?.Dispose();
        UpdateElementMappings(skSvg, svgDocument);
        UpdateAnimationPlayback();
        return true;
    }

    private void ClearLoadedData()
    {
        SKSvg? previousSvg;

        lock (this)
        {
            previousSvg = _skSvg;
            _skSvg = null;
            _picture = null;
            _svgDocument = null;
        }

        previousSvg?.Dispose();
        UpdateElementMappings(null, null);
        UpdateAnimationPlayback();
    }

    private bool TryGetRenderInfo(out SvgRenderInfo renderInfo)
    {
        renderInfo = default;

        var picture = _picture;
        if (picture is null)
        {
            return false;
        }

        var width = ActualWidth > 0D ? ActualWidth : _arrangedSvgSize.Width;
        var height = ActualHeight > 0D ? ActualHeight : _arrangedSvgSize.Height;
        if (width <= 0D || height <= 0D)
        {
            return false;
        }

        return SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(width, height),
            new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection,
            out renderInfo);
    }

    private void UpdateElementMappings(SKSvg? skSvg, SvgDocument? document)
    {
        _elementBySvgElement.Clear();
        _elementBySceneNode.Clear();
        ClearElementData(this);

        if (document is null || skSvg is null || !skSvg.TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            SynchronizeHostedControls();
            return;
        }

        var metrics = BuildSceneNodeMetrics(sceneDocument);
        var textBoundsFallback = new TextBoundsFallbackContext(skSvg.AssetLoader, sceneDocument.CullRect);
        var aggregateMetrics = new Dictionary<SvgElement, SceneNodeMetrics>();
        MapElementRecursive(this, document, metrics, aggregateMetrics, textBoundsFallback);
        SynchronizeHostedControls();
    }

    private static Dictionary<SvgElement, SceneNodeMetrics> BuildSceneNodeMetrics(SvgSceneDocument sceneDocument)
    {
        var metrics = new Dictionary<SvgElement, SceneNodeMetrics>();

        foreach (var current in sceneDocument.Traverse())
        {
            if (current.Element is SvgElement svgElement)
            {
                if (!metrics.TryGetValue(svgElement, out var entry))
                {
                    entry = new SceneNodeMetrics
                    {
                        Geometry = current.GeometryBounds,
                        Transformed = current.TransformedBounds,
                        Transform = ToMatrix3x2(current.Transform),
                        TotalTransform = ToMatrix3x2(current.TotalTransform)
                    };
                    metrics.Add(svgElement, entry);
                }
                else
                {
                    entry.Geometry = UnionRect(entry.Geometry, current.GeometryBounds);
                    entry.Transformed = UnionRect(entry.Transformed, current.TransformedBounds);
                }

                entry.SceneNodes.Add(current);
            }
        }

        return metrics;
    }

    private void MapElementRecursive(
        element control,
        SvgElement svgElement,
        Dictionary<SvgElement, SceneNodeMetrics> metrics,
        Dictionary<SvgElement, SceneNodeMetrics> aggregateMetrics,
        TextBoundsFallbackContext textBoundsFallback)
    {
        _elementBySvgElement[svgElement] = control;

        if (TryGetSceneNodeMetrics(svgElement, metrics, aggregateMetrics, textBoundsFallback, out var metric))
        {
            control.UpdateSvgData(
                svgElement,
                metric.SceneNodes,
                ToRect(metric.Geometry),
                ToRect(metric.Transformed),
                metric.Transform,
                metric.TotalTransform);

            foreach (var sceneNode in metric.SceneNodes)
            {
                _elementBySceneNode[sceneNode] = control;
            }
        }
        else
        {
            control.UpdateSvgData(
                svgElement,
                Array.Empty<SvgSceneNode>(),
                default,
                default,
                Matrix3x2.Identity,
                Matrix3x2.Identity);
        }

        var svgChildren = svgElement.Children.OfType<SvgElement>().ToList();
        foreach (var childControl in control.Children.OfType<element>())
        {
            if (IsContentElement(childControl))
            {
                ClearElementData(childControl);
                continue;
            }

            var match = TakeChildSvgElement(childControl, svgChildren);
            if (match is null)
            {
                ClearElementData(childControl);
                continue;
            }

            MapElementRecursive(childControl, match, metrics, aggregateMetrics, textBoundsFallback);
        }
    }

    private static bool IsContentElement(element control) => control is content;

    private static SvgElement? TakeChildSvgElement(element control, List<SvgElement> svgChildren)
    {
        if (svgChildren.Count == 0)
        {
            return null;
        }

        var mappingId = control.GetSvgMappingId();
        if (!string.IsNullOrEmpty(mappingId))
        {
            var byId = svgChildren.FirstOrDefault(e => string.Equals(e.ID, mappingId, StringComparison.Ordinal));
            if (byId is not null)
            {
                svgChildren.Remove(byId);
                return byId;
            }
        }

        var tag = control.SvgElementName;
        if (!string.IsNullOrEmpty(tag))
        {
            var byTag = svgChildren.FirstOrDefault(e => string.Equals(GetElementName(e), tag, StringComparison.OrdinalIgnoreCase));
            if (byTag is not null)
            {
                svgChildren.Remove(byTag);
                return byTag;
            }
        }

        var first = svgChildren[0];
        svgChildren.RemoveAt(0);
        return first;
    }

    private static string? GetElementName(SvgElement element)
    {
        var attribute = element.GetType().GetCustomAttribute<SvgElementAttribute>();
        return attribute?.ElementName ?? element.ID;
    }

    private static bool TryGetSceneNodeMetrics(
        SvgElement svgElement,
        Dictionary<SvgElement, SceneNodeMetrics> metrics,
        Dictionary<SvgElement, SceneNodeMetrics> aggregateMetrics,
        TextBoundsFallbackContext textBoundsFallback,
        out SceneNodeMetrics metric)
    {
        if (aggregateMetrics.TryGetValue(svgElement, out var cached))
        {
            metric = CloneSceneNodeMetrics(cached);
            return true;
        }

        var aggregate = CreateAggregateSceneNodeMetrics(svgElement, metrics, aggregateMetrics, textBoundsFallback);
        if (aggregate is null)
        {
            metric = null!;
            return false;
        }

        aggregateMetrics[svgElement] = CloneSceneNodeMetrics(aggregate);
        metric = CloneSceneNodeMetrics(aggregate);
        return true;
    }

    private static SceneNodeMetrics? CreateAggregateSceneNodeMetrics(
        SvgElement svgElement,
        Dictionary<SvgElement, SceneNodeMetrics> metrics,
        Dictionary<SvgElement, SceneNodeMetrics> aggregateMetrics,
        TextBoundsFallbackContext textBoundsFallback)
    {
        SceneNodeMetrics? aggregate = metrics.TryGetValue(svgElement, out var direct)
            ? CloneSceneNodeMetrics(direct)
            : null;

        foreach (var child in svgElement.Children.OfType<SvgElement>())
        {
            if (!TryGetSceneNodeMetrics(child, metrics, aggregateMetrics, textBoundsFallback, out var childMetrics))
            {
                continue;
            }

            if (childMetrics is null)
            {
                continue;
            }

            if (aggregate is null)
            {
                aggregate = CloneSceneNodeMetrics(childMetrics);
                continue;
            }

            aggregate.Geometry = UnionRect(aggregate.Geometry, childMetrics.Geometry);
            aggregate.Transformed = UnionRect(aggregate.Transformed, childMetrics.Transformed);
            aggregate.SceneNodes.AddRange(childMetrics.SceneNodes);
        }

        if ((aggregate is null || aggregate.Transformed.IsEmpty)
            && svgElement is SvgTextBase svgTextBase
            && SvgSceneCompiler.TryMeasureTextBounds(
                svgTextBase,
                textBoundsFallback.Viewport,
                textBoundsFallback.AssetLoader,
                out var measuredBounds))
        {
            var totalTransform = FindNearestAncestorTotalTransform(svgElement, metrics);
            aggregate = new SceneNodeMetrics
            {
                Geometry = measuredBounds,
                Transformed = TransformBounds(measuredBounds, totalTransform),
                Transform = Matrix3x2.Identity,
                TotalTransform = totalTransform
            };
        }

        return aggregate;
    }

    private static Matrix3x2 FindNearestAncestorTotalTransform(
        SvgElement svgElement,
        Dictionary<SvgElement, SceneNodeMetrics> metrics)
    {
        for (var parent = svgElement.Parent; parent is not null; parent = parent.Parent)
        {
            if (metrics.TryGetValue(parent, out var parentMetrics))
            {
                return parentMetrics.TotalTransform;
            }
        }

        return Matrix3x2.Identity;
    }

    private static ShimRect TransformBounds(ShimRect bounds, Matrix3x2 transform)
    {
        if (bounds.IsEmpty)
        {
            return default;
        }

        var topLeft = Vector2.Transform(new Vector2(bounds.Left, bounds.Top), transform);
        var topRight = Vector2.Transform(new Vector2(bounds.Right, bounds.Top), transform);
        var bottomRight = Vector2.Transform(new Vector2(bounds.Right, bounds.Bottom), transform);
        var bottomLeft = Vector2.Transform(new Vector2(bounds.Left, bounds.Bottom), transform);

        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));

        return new ShimRect(minX, minY, maxX, maxY);
    }

    private static SceneNodeMetrics CloneSceneNodeMetrics(SceneNodeMetrics source)
    {
        var clone = new SceneNodeMetrics
        {
            Geometry = source.Geometry,
            Transformed = source.Transformed,
            Transform = source.Transform,
            TotalTransform = source.TotalTransform
        };

        clone.SceneNodes.AddRange(source.SceneNodes);
        return clone;
    }

    private static void ClearElementData(element control)
    {
        control.ClearSvgData();

        foreach (var child in control.Children.OfType<element>())
        {
            ClearElementData(child);
        }
    }

    private static ShimRect UnionRect(ShimRect a, ShimRect b)
    {
        if (a.IsEmpty)
        {
            return b;
        }

        if (b.IsEmpty)
        {
            return a;
        }

        return ShimRect.Union(a, b);
    }

    private static Rect ToRect(ShimRect rect)
    {
        if (rect.IsEmpty)
        {
            return default;
        }

        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private static Matrix3x2 ToMatrix3x2(ShimMatrix matrix)
    {
        if (matrix.Equals(default(ShimMatrix)))
        {
            return Matrix3x2.Identity;
        }

        return new Matrix3x2(
            matrix.ScaleX,
            matrix.SkewY,
            matrix.SkewX,
            matrix.ScaleY,
            matrix.TransX,
            matrix.TransY);
    }

    private readonly record struct TextBoundsFallbackContext(ISvgAssetLoader AssetLoader, ShimRect Viewport);

    private sealed class SceneNodeMetrics
    {
        public ShimRect Geometry { get; set; }
        public ShimRect Transformed { get; set; }
        public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;
        public Matrix3x2 TotalTransform { get; set; } = Matrix3x2.Identity;
        public List<SvgSceneNode> SceneNodes { get; } = new();
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateSvgTree();
        UpdateAnimationPlayback();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _reloadQueued = false;
        StopAnimationPlayback();
        CloseHostedControlPresenters();
    }

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((svg)d).InvalidateSvgTree();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (svg)d;
        control.InvalidateMeasure();
        control.InvalidateArrange();
        control.InvalidateDrawingSurface();
    }
}
