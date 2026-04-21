using System.Numerics;
using System.Reflection;
using System.Text;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia;
using ShimMatrix = ShimSkiaSharp.SKMatrix;
using ShimRect = ShimSkiaSharp.SKRect;

namespace SvgML;

public partial class svg
{
    internal object Sync { get; } = new();

    private SKPicture? _picture;
    private SKSvg? _skSvg;
    private SvgDocument? _svgDocument;
    private readonly Dictionary<SvgElement, element> _elementBySvgElement = new();
    private readonly Dictionary<SvgSceneNode, element> _elementBySceneNode = new();

    public SKPicture? Picture => _picture;

    public SKSvg? SkSvg => _skSvg;

    private static void Initialize()
    {
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

    private bool Load(svg? source, SvgParameters? parameters)
    {
        if (source is null)
        {
            ClearLoadedData();
            return false;
        }

        try
        {
            var markup = source.ToSvgString(source);
            using var stream = GenerateStreamFromString(markup);
            return LoadFromStream(stream, parameters);
        }
        catch
        {
            ClearLoadedData();
            return false;
        }
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

        lock (Sync)
        {
            previousSvg = _skSvg;
            _skSvg = skSvg;
            _picture = picture;
            _svgDocument = svgDocument;
        }

        previousSvg?.Dispose();
        UpdateElementMappings(skSvg, svgDocument);
        return true;
    }

    private void ClearLoadedData()
    {
        SKSvg? previousSvg;

        lock (Sync)
        {
            previousSvg = _skSvg;
            _skSvg = null;
            _picture = null;
            _svgDocument = null;
        }

        previousSvg?.Dispose();
        UpdateElementMappings(null, null);
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
        MapElementRecursive(this, document, metrics);
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

    private void MapElementRecursive(element control, SvgElement svgElement, Dictionary<SvgElement, SceneNodeMetrics> metrics)
    {
        _elementBySvgElement[svgElement] = control;

        if (TryGetSceneNodeMetrics(svgElement, metrics, out var metric))
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

            MapElementRecursive(childControl, match, metrics);
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

    private static bool TryGetSceneNodeMetrics(SvgElement svgElement, Dictionary<SvgElement, SceneNodeMetrics> metrics, out SceneNodeMetrics metric)
    {
        var aggregate = CreateAggregateSceneNodeMetrics(svgElement, metrics);
        if (aggregate is null)
        {
            metric = null!;
            return false;
        }

        metric = aggregate;
        return true;
    }

    private static SceneNodeMetrics? CreateAggregateSceneNodeMetrics(SvgElement svgElement, Dictionary<SvgElement, SceneNodeMetrics> metrics)
    {
        SceneNodeMetrics? aggregate = metrics.TryGetValue(svgElement, out var direct)
            ? CloneSceneNodeMetrics(direct)
            : null;

        foreach (var child in svgElement.Children.OfType<SvgElement>())
        {
            var childMetrics = CreateAggregateSceneNodeMetrics(child, metrics);
            if (childMetrics is null)
            {
                continue;
            }

            if (aggregate is null)
            {
                aggregate = childMetrics;
                continue;
            }

            aggregate.Geometry = UnionRect(aggregate.Geometry, childMetrics.Geometry);
            aggregate.Transformed = UnionRect(aggregate.Transformed, childMetrics.Transformed);
            aggregate.SceneNodes.AddRange(childMetrics.SceneNodes);
        }

        return aggregate;
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

    private sealed class SceneNodeMetrics
    {
        public ShimRect Geometry { get; set; }
        public ShimRect Transformed { get; set; }
        public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;
        public Matrix3x2 TotalTransform { get; set; } = Matrix3x2.Identity;
        public List<SvgSceneNode> SceneNodes { get; } = new();
    }
}
