using System;
using System.Collections.Generic;
using System.Linq;
using Svg.Editor.Svg;
using Svg.Model.Services;
using Svg.Pathing;
using Svg.Transforms;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    public SvgVisualElement? CreateElement(ToolService.Tool tool, SK.SKPoint start, SK.SKPoint end)
    {
        if (Document is null)
            return null;
        Tools.CurrentStrokeWidth = CurrentStrokeWidth;
        var element = Tools.CreateElement(tool, Document, new ShimSkiaSharp.SKPoint(start.X, start.Y));
        if (element is null)
            return null;
        Tools.UpdateElement(element, tool, new ShimSkiaSharp.SKPoint(start.X, start.Y),
            new ShimSkiaSharp.SKPoint(end.X, end.Y), Session.Settings.SnapToGrid, Selection.Snap);
        AddElement(element, "Draw " + tool);
        return element;
    }

    public void AddElement(SvgVisualElement element, string label = "Add object")
    {
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        if (element.Parent is not null)
            throw new ArgumentException("The object already belongs to a document.", nameof(element));
        Execute(label, () =>
        {
            if (string.IsNullOrEmpty(element.ID))
                element.ID = CreateId(element.GetType().Name.Replace("Svg", string.Empty).ToLowerInvariant());
            else if (Document!.Descendants().Any(e => e.ID == element.ID))
                throw new ArgumentException("An object with that ID already exists.", nameof(element));
            var openPath = element is SvgLine or SvgPolyline || element is SvgPath path &&
                (path.Fill == SvgPaintServer.None || !path.PathData.OfType<SvgClosePathSegment>().Any());
            element.Fill = openPath ? SvgPaintServer.None : ParsePaint(CurrentFill);
            element.Stroke = openPath && CurrentStroke == "none" ? ParsePaint(CurrentFill) : ParsePaint(CurrentStroke);
            element.StrokeWidth = CurrentStrokeWidth;
            if (element is SvgTextBase text && text.FontSize.Value <= 0)
                text.FontSize = 32;
            Document!.Children.Add(element);
            _selection.Clear();
            _selection.Add(element);
        });
    }

    public void DeleteSelection()
    {
        if (_selection.Count == 0)
            return;
        EnsureSelectionEditable();
        Execute("Delete objects", () =>
        {
            foreach (var element in TopLevelSelection())
                element.Parent?.Children.Remove(element);
            _selection.Clear();
        });
    }

    /// <summary>Moves by a picture-space delta, including under transformed parents.</summary>
    public void MoveSelection(float dx, float dy)
    {
        if (!IsFinite(dx) || !IsFinite(dy))
            throw new ArgumentOutOfRangeException(nameof(dx));
        if (dx == 0 && dy == 0)
            return;
        TransformSelection(SK.SKMatrix.CreateTranslation(dx, dy));
    }

    /// <summary>Applies an affine transform in picture coordinates.</summary>
    public void TransformSelection(SK.SKMatrix transform)
    {
        ValidateMatrix(transform);
        if (_selection.Count == 0 || transform.IsIdentity)
            return;
        EnsureSelectionEditable();
        // Capture all parent matrices before mutating any selected element.
        var targets = TopLevelSelection().Select(element =>
        {
            var parent = GetSceneNode(element)?.Parent;
            var matrix = parent is null ? SK.SKMatrix.Identity : ToMatrix(parent.TotalTransform);
            if (!matrix.TryInvert(out var inverse))
                throw new InvalidOperationException("An object inside a singular transform cannot be transformed.");
            return (Element: element, Transform: Multiply(Multiply(inverse, transform), matrix));
        }).ToArray();
        Execute("Transform objects", () =>
        {
            foreach (var target in targets)
                PrependTransform(target.Element, target.Transform);
        });
    }

    public void RotateSelection(float degrees)
    {
        if (!IsFinite(degrees))
            throw new ArgumentOutOfRangeException(nameof(degrees));
        if (SelectionBounds is { } bounds)
            TransformSelection(SK.SKMatrix.CreateRotationDegrees(degrees, bounds.MidX, bounds.MidY));
    }

    public void FlipHorizontal()
    {
        if (SelectionBounds is { } bounds)
            TransformSelection(SK.SKMatrix.CreateScale(-1, 1, bounds.MidX, bounds.MidY));
    }

    public void FlipVertical()
    {
        if (SelectionBounds is { } bounds)
            TransformSelection(SK.SKMatrix.CreateScale(1, -1, bounds.MidX, bounds.MidY));
    }

    public void GroupSelection()
    {
        EnsureSelectionEditable();
        var elements = TopLevelSelection();
        if (elements.Length == 0)
            return;
        var parent = elements[0].Parent;
        if (parent is null || elements.Any(e => !ReferenceEquals(e.Parent, parent)))
            throw new InvalidOperationException("Group objects within the same layer. Move them to one layer first.");
        var ordered = elements.OrderBy(e => parent.Children.IndexOf(e)).ToArray();
        Execute("Group objects", () =>
        {
            var index = parent.Children.IndexOf(ordered[0]);
            var group = new SvgGroup { ID = CreateId("group") };
            foreach (var element in ordered)
            {
                parent.Children.Remove(element);
                group.Children.Add(element);
            }
            parent.Children.Insert(index, group);
            _selection.Clear();
            _selection.Add(group);
        });
    }

    public void UngroupSelection()
    {
        EnsureSelectionEditable();
        var groups = TopLevelSelection().OfType<SvgGroup>().ToArray();
        if (groups.Length == 0)
            return;
        foreach (var group in groups)
        {
            if (group.Opacity != 1 || group.Filter is not null || (group.TryGetAttribute("mask", out var mask) && !string.IsNullOrEmpty(mask) && mask != "none") || group.ClipPath is not null ||
                group.CustomAttributes.ContainsKey("mix-blend-mode") || group.CustomAttributes.ContainsKey("isolation"))
                throw new InvalidOperationException("This group has a compositing effect. Remove its opacity, clip, mask or filter before ungrouping to preserve appearance.");
        }
        var transforms = groups.ToDictionary(g => g, g => GetSceneNode(g) is { } node ? ToMatrix(node.Transform) : SK.SKMatrix.Identity);
        Execute("Ungroup objects", () =>
        {
            _selection.Clear();
            foreach (var group in groups)
            {
                var parent = group.Parent!;
                var index = parent.Children.IndexOf(group);
                var children = group.Children.ToArray();
                // Materialize inherited paint and typography while the original ancestry exists.
                foreach (var child in children.OfType<SvgVisualElement>())
                    MaterializePresentation(child, child);
                foreach (var child in children)
                {
                    group.Children.Remove(child);
                    if (child is SvgVisualElement visual)
                    {
                        PrependTransform(visual, transforms[group]);
                        _selection.Add(visual);
                    }
                    parent.Children.Insert(index++, child);
                }
                parent.Children.Remove(group);
            }
        });
    }

    public void BringToFront() => ReorderSelection(true, true);
    public void SendToBack() => ReorderSelection(false, true);
    public void BringForward() => ReorderSelection(true, false);
    public void SendBackward() => ReorderSelection(false, false);

    private void ReorderSelection(bool forward, bool toEnd)
    {
        if (_selection.Count == 0)
            return;
        EnsureSelectionEditable();
        Execute(forward ? "Raise objects" : "Lower objects", () =>
        {
            foreach (var grouping in TopLevelSelection().GroupBy(e => e.Parent))
            {
                var parent = grouping.Key;
                if (parent is null)
                    continue;
                var selected = new HashSet<SvgElement>(grouping);
                var order = parent.Children.ToList();
                if (toEnd)
                {
                    var items = order.Where(selected.Contains).ToArray();
                    order.RemoveAll(selected.Contains);
                    order.InsertRange(forward ? order.Count : 0, items);
                }
                else if (forward)
                {
                    for (var i = order.Count - 2; i >= 0; i--)
                        if (selected.Contains(order[i]) && !selected.Contains(order[i + 1]))
                            (order[i], order[i + 1]) = (order[i + 1], order[i]);
                }
                else
                {
                    for (var i = 1; i < order.Count; i++)
                        if (selected.Contains(order[i]) && !selected.Contains(order[i - 1]))
                            (order[i], order[i - 1]) = (order[i - 1], order[i]);
                }
                foreach (var child in parent.Children.ToArray())
                    parent.Children.Remove(child);
                foreach (var child in order)
                    parent.Children.Add(child);
            }
        });
    }

    public void Align(AlignService.AlignType alignment)
    {
        EnsureSelectionEditable();
        var targets = GetSelectionGeometry();
        if (targets.Length < 2)
            return;
        var left = targets.Min(t => t.Bounds.Left);
        var right = targets.Max(t => t.Bounds.Right);
        var top = targets.Min(t => t.Bounds.Top);
        var bottom = targets.Max(t => t.Bounds.Bottom);
        Execute("Align objects", () =>
        {
            foreach (var item in targets)
            {
                var dx = alignment switch
                {
                    AlignService.AlignType.Left => left - item.Bounds.Left,
                    AlignService.AlignType.Right => right - item.Bounds.Right,
                    AlignService.AlignType.HCenter => (left + right) / 2 - item.Bounds.MidX,
                    _ => 0
                };
                var dy = alignment switch
                {
                    AlignService.AlignType.Top => top - item.Bounds.Top,
                    AlignService.AlignType.Bottom => bottom - item.Bounds.Bottom,
                    AlignService.AlignType.VCenter => (top + bottom) / 2 - item.Bounds.MidY,
                    _ => 0
                };
                ApplyWorldTranslation(item.Element, dx, dy);
            }
        });
    }

    public void Distribute(AlignService.DistributeType direction)
    {
        EnsureSelectionEditable();
        var horizontal = direction == AlignService.DistributeType.Horizontal;
        var targets = GetSelectionGeometry().OrderBy(t => horizontal ? t.Bounds.MidX : t.Bounds.MidY).ToArray();
        if (targets.Length < 3)
            return;
        var start = horizontal ? targets[0].Bounds.MidX : targets[0].Bounds.MidY;
        var end = horizontal ? targets[targets.Length - 1].Bounds.MidX : targets[targets.Length - 1].Bounds.MidY;
        Execute("Distribute objects", () =>
        {
            for (var i = 1; i < targets.Length - 1; i++)
            {
                var item = targets[i];
                var delta = start + (end - start) * i / (targets.Length - 1) - (horizontal ? item.Bounds.MidX : item.Bounds.MidY);
                ApplyWorldTranslation(item.Element, horizontal ? delta : 0, horizontal ? 0 : delta);
            }
        });
    }

    public void ApplyBoolean(SK.SKPathOp operation)
    {
        EnsureSelectionEditable();
        var elements = TopLevelSelection();
        if (elements.Length < 2)
            return;
        var first = elements[0];
        var parent = first.Parent!;
        var parentMatrix = GetSceneNode(first)?.Parent is { } parentNode ? ToMatrix(parentNode.TotalTransform) : SK.SKMatrix.Identity;
        if (!parentMatrix.TryInvert(out var inverse))
            throw new InvalidOperationException("The destination layer has a singular transform.");
        SK.SKPath? result = null;
        try
        {
            foreach (var element in elements)
            {
                using var geometry = CreateGeometryPath(element) ?? throw new InvalidOperationException("Convert the selected objects to paths before combining them.");
                var node = GetSceneNode(element) ?? throw new InvalidOperationException("The selected object has no editable geometry.");
                geometry.Transform(Multiply(inverse, ToMatrix(node.TotalTransform)));
                if (result is null)
                    result = new SK.SKPath(geometry);
                else
                {
                    var combined = result.Op(geometry, operation) ?? throw new InvalidOperationException("The path operation could not be completed.");
                    result.Dispose();
                    result = combined;
                }
            }
            if (result is null)
                return;
            var path = CloneAsPath(first, PathService.ToSvgPathData(result), false);
            Execute("Combine paths", () =>
            {
                var index = parent.Children.IndexOf(first);
                foreach (var element in elements)
                    element.Parent?.Children.Remove(element);
                parent.Children.Insert(Math.Min(index, parent.Children.Count), path);
                _selection.Clear();
                _selection.Add(path);
            });
        }
        finally
        {
            result?.Dispose();
        }
    }

    public void ConvertSelectionToPaths()
    {
        EnsureSelectionEditable();
        var replacements = TopLevelSelection().Select(element =>
        {
            using var geometry = CreateGeometryPath(element) ?? throw new InvalidOperationException("This object cannot be converted to a geometric path.");
            var path = CloneAsPath(element, PathService.ToSvgPathData(geometry), true);
            return (Original: element, Path: path);
        }).ToArray();
        Execute("Convert to paths", () =>
        {
            _selection.Clear();
            foreach (var item in replacements)
            {
                var parent = item.Original.Parent!;
                var index = parent.Children.IndexOf(item.Original);
                parent.Children.Remove(item.Original);
                parent.Children.Insert(index, item.Path);
                _selection.Add(item.Path);
            }
        });
    }

    public void SimplifySelection()
    {
        EnsureSelectionEditable();
        Execute("Simplify paths", () => { foreach (var path in _selection.OfType<SvgPath>()) Paths.SimplifyPath(path); });
    }

    /// <summary>Uses the second selected shape to clip the first, retaining its inherited SVG presentation.</summary>
    public void CreateClippingMask()
    {
        EnsureSelectionEditable();
        if (_selection.Count != 2)
            throw new InvalidOperationException("Select the artwork first and the clipping shape second.");
        var artwork = _selection[0];
        var shape = _selection[1];
        if (shape is not (SvgPath or SvgRectangle or SvgCircle or SvgEllipse or SvgLine or SvgPolyline or SvgPolygon or SvgText or SvgUse))
            throw new InvalidOperationException("Use a path, basic shape or text as the clipping shape.");
        var parent = artwork.Parent;
        if (parent is null || !ReferenceEquals(parent, shape.Parent))
            throw new InvalidOperationException("Place the artwork and clipping shape in the same group first.");
        var node = GetSceneNode(artwork)
            ?? throw new InvalidOperationException("The selected artwork has no editable geometry.");
        if (!ToMatrix(node.Transform).TryInvert(out var inverse))
            throw new InvalidOperationException("Cannot clip an object with a singular transform.");
        var clip = new SvgClipPath
        {
            ID = CreateId("clip"),
            ClipPathUnits = SvgCoordinateUnits.UserSpaceOnUse,
            // The reference is evaluated in the artwork's local coordinates. Keep the
            // compensating matrix on the resource so the shape's own CSS transform survives.
            Transforms = new SvgTransformCollection
            {
                new SvgMatrix(new List<float> { inverse.ScaleX, inverse.SkewY, inverse.SkewX, inverse.ScaleY, inverse.TransX, inverse.TransY })
            }
        };
        Execute("Create clipping mask", () =>
        {
            parent.Children.Remove(shape);
            clip.Children.Add(shape);
            // Keep the original style and xml:base ancestry for typography, clip-rule,
            // custom properties and linked resources instead of moving them to root defs.
            var definitions = parent.Children.OfType<SvgDefinitionList>().FirstOrDefault();
            if (definitions is null)
            {
                definitions = new SvgDefinitionList();
                parent.Children.Insert(0, definitions);
            }
            definitions.Children.Add(clip);
            artwork.ClipPath = new Uri("#" + clip.ID, UriKind.Relative);
            _selection.Clear();
            _selection.Add(artwork);
        });
    }

    /// <summary>Adds offset paths using a constant picture-space distance and preserves SVG appearance.</summary>
    public void OffsetSelection(float distance)
    {
        EnsureSelectionEditable();
        if (!IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (_selection.Count == 0 || distance == 0)
            return;
        var originals = TopLevelSelection();
        var fragment = CreateFragmentDocument();
        foreach (var element in originals)
        {
            using var source = CreateGeometryPath(element)
                ?? throw new InvalidOperationException("Convert the selected object to a path before offsetting it.");
            var node = GetSceneNode(element) ?? throw new InvalidOperationException("The selected object has no editable geometry.");
            var matrix = ToMatrix(node.TotalTransform);
            if (!matrix.TryInvert(out var inverse))
                throw new InvalidOperationException("An object with a singular transform cannot be offset.");
            source.Transform(matrix);
            using var paint = new SK.SKPaint { Style = SK.SKPaintStyle.Stroke, StrokeWidth = Math.Abs(distance) * 2, StrokeJoin = SK.SKStrokeJoin.Round };
            using var builder = new SK.SKPathBuilder();
            if (!paint.GetFillPath(source, builder))
                throw new InvalidOperationException("The path outline could not be calculated.");
            using var stroke = builder.Detach();
            using var result = source.Op(stroke, distance > 0 ? SK.SKPathOp.Union : SK.SKPathOp.Difference)
                ?? throw new InvalidOperationException("The path offset could not be completed.");
            result.Transform(inverse);
            fragment.Children.Add(CloneAsPath(element, PathService.ToSvgPathData(result), true));
        }
        var imported = ParseDocument(RemapCloneIds(fragment.GetXML()), Document!.BaseUri);
        var paths = imported.Children.OfType<SvgPath>().ToArray();
        Execute("Offset paths", () =>
        {
            _selection.Clear();
            for (var i = 0; i < originals.Length; i++)
            {
                var path = paths[i];
                imported.Children.Remove(path);
                if (string.IsNullOrEmpty(path.ID))
                    path.ID = CreateId("offset");
                var parent = originals[i].Parent!;
                parent.Children.Insert(parent.Children.IndexOf(originals[i]) + 1, path);
                RebindPaintServers(path);
                _selection.Add(path);
            }
        });
    }

    private (SvgVisualElement Element, SK.SKRect Bounds)[] GetSelectionGeometry()
        => TopLevelSelection().Select(e => (Element: e, Node: GetSceneNode(e)))
            .Where(t => t.Node is not null)
            .Select(t => (t.Element, new SK.SKRect(t.Node!.TransformedBounds.Left, t.Node.TransformedBounds.Top,
                t.Node.TransformedBounds.Right, t.Node.TransformedBounds.Bottom))).ToArray();

    private SvgVisualElement[] TopLevelSelection()
    {
        var selected = new HashSet<SvgElement>(_selection);
        return _selection.Where(element =>
        {
            for (var parent = element.Parent; parent is not null; parent = parent.Parent)
                if (selected.Contains(parent))
                    return false;
            return true;
        }).ToArray();
    }

    private void ApplyWorldTranslation(SvgVisualElement element, float dx, float dy)
    {
        var parent = GetSceneNode(element)?.Parent;
        var matrix = parent is null ? SK.SKMatrix.Identity : ToMatrix(parent.TotalTransform);
        if (!matrix.TryInvert(out var inverse))
            throw new InvalidOperationException("An object inside a singular transform cannot be moved.");
        PrependTransform(element, Multiply(Multiply(inverse, SK.SKMatrix.CreateTranslation(dx, dy)), matrix));
    }

    private string CreateId(string prefix)
    {
        string id;
        do
        { id = prefix + (++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture); } while (Document?.GetElementById(id) is not null || Document?.ID == id);
        return id;
    }

    private static SK.SKPath? CreateGeometryPath(SvgVisualElement element)
    {
        if (element is SvgRectangle rectangle && (rectangle.CornerRadiusX.Value > 0 || rectangle.CornerRadiusY.Value > 0))
        {
            var path = new SK.SKPath();
            var rx = rectangle.CornerRadiusX.Value;
            var ry = rectangle.CornerRadiusY.Value;
            if (rx == 0)
                rx = ry;
            if (ry == 0)
                ry = rx;
            path.AddRoundRect(SK.SKRect.Create(rectangle.X.Value, rectangle.Y.Value, rectangle.Width.Value, rectangle.Height.Value), rx, ry);
            return path;
        }
        return PathService.ElementToPath(element);
    }

    private SvgPath CloneAsPath(SvgVisualElement source, string data, bool preserveTransform)
    {
        var temporary = CreateFragmentDocument();
        var copy = (SvgVisualElement)source.DeepCopy();
        MaterializePresentation(source, copy);
        temporary.Children.Add(copy);
        var tree = System.Xml.Linq.XDocument.Parse(temporary.GetXML(), System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var element = tree.Root!.Elements().Single();
        element.Name = System.Xml.Linq.XName.Get("path", "http://www.w3.org/2000/svg");
        var geometryAttributes = new[] { "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry", "width", "height", "points" };
        foreach (var name in geometryAttributes)
            element.Attribute(name)?.Remove();
        element.SetAttributeValue("d", data);
        if (!preserveTransform)
        {
            element.Attribute("transform")?.Remove();
            element.Attribute("transform-origin")?.Remove();
            element.Attribute("transform-box")?.Remove();
            if (element.Attribute("style") is { } style)
                style.Value = System.Text.RegularExpressions.Regex.Replace(style.Value,
                    @"(^|;)\s*transform(?:-origin|-box)?\s*:[^;]*", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        var parsed = ParseDocument(tree.ToString(System.Xml.Linq.SaveOptions.DisableFormatting), Document?.BaseUri);
        var path = (SvgPath)parsed.Children[0];
        parsed.Children.Remove(path);
        RebindPaintServers(path);
        return path;
    }

    private static void MaterializePresentation(SvgVisualElement source, SvgVisualElement target)
    {
        target.Fill = source.Fill;
        target.FillOpacity = source.FillOpacity;
        target.FillRule = source.FillRule;
        target.Stroke = source.Stroke;
        target.StrokeWidth = source.StrokeWidth;
        target.StrokeOpacity = source.StrokeOpacity;
        target.StrokeLineCap = source.StrokeLineCap;
        target.StrokeLineJoin = source.StrokeLineJoin;
        target.StrokeMiterLimit = source.StrokeMiterLimit;
        target.StrokeDashArray = source.StrokeDashArray;
        target.StrokeDashOffset = source.StrokeDashOffset;
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.FontWeight = source.FontWeight;
        target.FontStyle = source.FontStyle;
        target.Visibility = source.Visibility;
        target.Color = source.Color;
    }

    private static void PrependTransform(SvgVisualElement element, SK.SKMatrix matrix)
    {
        if (matrix.IsIdentity)
            return;
        ValidateMatrix(matrix);
        var skip = 0;
        if (element.Transforms is { Count: > 0 } existing && existing[0] is SvgMatrix first && first.Points.Count == 6)
        {
            var p = first.Points;
            matrix = Multiply(matrix, new SK.SKMatrix(p[0], p[2], p[4], p[1], p[3], p[5], 0, 0, 1));
            ValidateMatrix(matrix);
            skip = 1;
        }
        var transforms = new SvgTransformCollection
        {
            new SvgMatrix(new List<float> { matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY })
        };
        if (element.Transforms is not null)
        {
            for (var i = skip; i < element.Transforms.Count; i++)
                transforms.Add(element.Transforms[i]);
        }
        element.Transforms = transforms;
    }

    private static SK.SKMatrix ToMatrix(ShimSkiaSharp.SKMatrix matrix)
        => new(matrix.ScaleX, matrix.SkewX, matrix.TransX, matrix.SkewY, matrix.ScaleY, matrix.TransY, matrix.Persp0, matrix.Persp1, matrix.Persp2);

    private static SK.SKMatrix Multiply(SK.SKMatrix left, SK.SKMatrix right)
        => new(
            left.ScaleX * right.ScaleX + left.SkewX * right.SkewY,
            left.ScaleX * right.SkewX + left.SkewX * right.ScaleY,
            left.ScaleX * right.TransX + left.SkewX * right.TransY + left.TransX,
            left.SkewY * right.ScaleX + left.ScaleY * right.SkewY,
            left.SkewY * right.SkewX + left.ScaleY * right.ScaleY,
            left.SkewY * right.TransX + left.ScaleY * right.TransY + left.TransY,
            0, 0, 1);

    private static void ValidateMatrix(SK.SKMatrix matrix)
    {
        if (!IsFinite(matrix.ScaleX) || !IsFinite(matrix.ScaleY) || !IsFinite(matrix.SkewX) || !IsFinite(matrix.SkewY) ||
            !IsFinite(matrix.TransX) || !IsFinite(matrix.TransY) || matrix.Persp0 != 0 || matrix.Persp1 != 0 || matrix.Persp2 != 1)
            throw new ArgumentException("SVG editing requires a finite affine transform.", nameof(matrix));
    }
}
