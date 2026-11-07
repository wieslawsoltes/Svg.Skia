using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Pathing;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Swaps selected paints and the creation defaults in a single undoable document edit.</summary>
    public void SwapFillAndStroke()
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        var fill = CurrentStroke;
        var stroke = CurrentFill;
        if (_selection.Count > 0)
        {
            Execute("Swap fill and stroke", () =>
            {
                foreach (var element in _selection)
                {
                    var oldFill = element.Fill;
                    element.Fill = element.Stroke;
                    element.Stroke = oldFill;
                }
            });
            fill = PaintKey(_selection[0].Fill);
            stroke = PaintKey(_selection[0].Stroke);
        }
        CurrentFill = fill;
        CurrentStroke = stroke;
        NotifyChanged();
    }

    /// <summary>Restores white fill, black one-unit stroke; also works with no selection.</summary>
    public void ResetPaints()
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (_selection.Count > 0)
        {
            Execute("Default fill and stroke", () =>
            {
                foreach (var element in _selection)
                {
                    element.Fill = ParsePaint("#FFFFFF");
                    element.Stroke = ParsePaint("#000000");
                    element.StrokeWidth = 1;
                }
            });
        }
        CurrentFill = "#FFFFFF";
        CurrentStroke = "#000000";
        CurrentStrokeWidth = Tools.CurrentStrokeWidth = 1;
        NotifyChanged();
    }

    /// <summary>
    /// Samples the topmost painted SVG object without changing the target selection.
    /// Locked source artwork can be read, while locked targets remain protected.
    /// With no selection the sampled appearance becomes the next drawing's defaults.
    /// </summary>
    public bool SampleAppearanceAt(SKPoint point, bool paintOnly = false, bool strokeTarget = false)
    {
        ThrowIfDisposed();
        if (!IsFinite(point.X) || !IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        if (Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(point.X, point.Y)) is not SvgVisualElement source)
            return false;
        EnsureSelectionEditable();
        var fill = PaintKey(source.Fill);
        var stroke = PaintKey(source.Stroke);
        var sampledPaint = strokeTarget ? stroke : fill;
        if (_selection.Count > 0)
        {
            Execute(paintOnly ? "Sample paint" : "Sample appearance", () =>
            {
                foreach (var target in _selection)
                {
                    if (ReferenceEquals(source, target))
                        continue;
                    if (paintOnly)
                    {
                        if (strokeTarget)
                            target.Stroke = source.Stroke;
                        else
                            target.Fill = source.Fill;
                    }
                    else
                    {
                        MaterializePresentation(source, target);
                        // Collections must not remain shared between unrelated artwork.
                        target.StrokeDashArray = source.StrokeDashArray is null ? null
                            : (SvgUnitCollection)source.StrokeDashArray.Clone();
                        target.Opacity = source.Opacity;
                    }
                }
            });
        }
        if (paintOnly)
        {
            if (strokeTarget)
                CurrentStroke = sampledPaint;
            else
                CurrentFill = sampledPaint;
        }
        else
        {
            CurrentFill = fill;
            CurrentStroke = stroke;
            CurrentStrokeWidth = Tools.CurrentStrokeWidth = source.StrokeWidth.Value;
        }
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Places a linear gradient along a drag in picture coordinates. Each differently
    /// transformed target receives an independent inverse-transform definition, keeping
    /// the visible gradient line fixed under nested transforms and nonuniform scales.
    /// Existing definitions and their unselected consumers are left intact.
    /// </summary>
    public void ApplyGradientLine(SKPoint start, SKPoint end, bool stroke = false)
    {
        ThrowIfDisposed();
        if (!IsFinite(start.X) || !IsFinite(start.Y) || !IsFinite(end.X) || !IsFinite(end.Y))
            throw new ArgumentOutOfRangeException(nameof(end));
        if (SKPoint.Distance(start, end) < 0.001f)
            return;
        var draft = BeginGradientEdit(stroke);
        var inverses = _selection.Select(element =>
        {
            var scene = GetSceneNode(element) ?? throw new InvalidOperationException("The gradient target has no rendered geometry.");
            if (!ToMatrix(scene.TotalTransform).TryInvert(out var inverse))
                throw new InvalidOperationException("A gradient cannot be positioned inside a singular transform.");
            return inverse;
        }).ToArray();
        var tree = ReadEffectDocument(draft.Source);
        var targets = draft.Selection.Select(key => ResolveEffectElement(tree, key)
            ?? throw new InvalidOperationException("The gradient target no longer exists.")).ToArray();
        var definitions = tree.Root!.Elements(GradientNamespace + "defs").FirstOrDefault();
        // Append after resolving anonymous targets; inserting before them would alter
        // the structural addresses captured by the history/selection machinery.
        if (definitions is null)
        { definitions = new XElement(GradientNamespace + "defs"); tree.Root.Add(definitions); }
        for (var index = 0; index < targets.Length; index++)
        {
            var id = UniqueEffectId(tree, "gradient");
            var matrix = inverses[index];
            var gradient = new XElement(GradientNamespace + "linearGradient",
                new XAttribute("id", id), new XAttribute("gradientUnits", "userSpaceOnUse"),
                new XAttribute("x1", start.X.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("y1", start.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("x2", end.X.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("y2", end.Y.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("spreadMethod", draft.Spread.ToString().ToLowerInvariant()),
                new XAttribute("gradientTransform", string.Format(CultureInfo.InvariantCulture,
                    "matrix({0:R} {1:R} {2:R} {3:R} {4:R} {5:R})",
                    matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY)));
            foreach (var stop in draft.Ramp.Stops)
                gradient.Add(new XElement(GradientNamespace + "stop",
                    new XAttribute("offset", stop.Offset.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("stop-color", SvgSolidPaintDraft.FormatColor(stop.Color)),
                    new XAttribute("stop-opacity", stop.Opacity.ToString("R", CultureInfo.InvariantCulture))));
            definitions.Add(gradient);
            SetEffectReference(targets[index], stroke ? "stroke" : "fill", "url(#" + id + ")");
        }
        if (!draft.IsCurrent(this))
            throw new InvalidOperationException("The gradient target changed during the drag.");
        var time = Renderer.AnimationTime;
        ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), stroke ? "Draw stroke gradient" : "Draw fill gradient");
        Renderer.SetAnimationTime(time);
    }

    /// <summary>Crops or expands the one SVG artboard without transforming its artwork.</summary>
    public void SetArtboardBounds(SKRect pictureBounds)
    {
        ThrowIfDisposed();
        if (Document is not { } document || Renderer.Picture is not { } picture)
            return;
        ValidatePositive(pictureBounds.Width, nameof(pictureBounds));
        ValidatePositive(pictureBounds.Height, nameof(pictureBounds));
        if (!IsFinite(pictureBounds.Left) || !IsFinite(pictureBounds.Top)
            || !IsFinite(pictureBounds.Right) || !IsFinite(pictureBounds.Bottom))
            throw new ArgumentOutOfRangeException(nameof(pictureBounds));
        if (IsLocked(document))
            throw new InvalidOperationException("Unlock the document before changing its artboard.");
        var root = GetSceneNode(document) ?? Renderer.RetainedSceneGraph?.Root;
        var transform = root is not null ? ToMatrix(root.TotalTransform) : SKMatrix.Identity;
        if (Math.Abs(transform.SkewX) > 0.0001 || Math.Abs(transform.SkewY) > 0.0001 || !transform.TryInvert(out var inverse))
            throw new InvalidOperationException("Reset the root SVG rotation or skew before cropping its artboard.");
        var area = inverse.MapRect(pictureBounds);
        ValidatePositive(area.Width, nameof(pictureBounds));
        ValidatePositive(area.Height, nameof(pictureBounds));
        var old = picture.CullRect;
        ValidatePositive(old.Width, nameof(pictureBounds));
        ValidatePositive(old.Height, nameof(pictureBounds));
        var width = document.Width.Value * pictureBounds.Width / old.Width;
        var height = document.Height.Value * pictureBounds.Height / old.Height;
        ValidatePositive(width, nameof(pictureBounds));
        ValidatePositive(height, nameof(pictureBounds));
        Execute("Change artboard bounds", () =>
        {
            document.Width = new SvgUnit(document.Width.Type, width);
            document.Height = new SvgUnit(document.Height.Type, height);
            document.ViewBox = new SvgViewBox(area.Left, area.Top, area.Width, area.Height);
        });
    }

    /// <summary>
    /// Inserts canvas geometry without applying the imported document's viewport twice.
    /// The inverse viewport is retained as a native SVG transform, preserving editable
    /// primitives, pen curves, symbol references, and their picture-space stroke width.
    /// </summary>
    public void AddElementInPictureCoordinates(SvgVisualElement element, string label = "Draw object")
    {
        ThrowIfDisposed();
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        if (element.Parent is not null)
            throw new ArgumentException("The object already belongs to a document.", nameof(element));
        if (Document is not { } document)
            throw new InvalidOperationException("Open a document before drawing.");
        if (IsLocked(document))
            throw new InvalidOperationException("Unlock the document before drawing.");
        var root = GetSceneNode(document) ?? Renderer.RetainedSceneGraph?.Root;
        var transform = root is null ? SKMatrix.Identity : ToMatrix(root.TotalTransform);
        if (!transform.TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        PrependTransform(element, inverse);
        AddElement(element, label);
    }

    public SvgPath AddEditorShape(SvgEditorInteractionMode mode, SKPoint start, SKPoint end, int divisions = 6)
    {
        using var path = SvgEditorToolGeometry.CreateShape(mode, start, end, divisions);
        var element = new SvgPath { PathData = SvgPathBuilder.Parse(path.ToSvgPathData()) };
        if (mode is SvgEditorInteractionMode.RectangularGrid or SvgEditorInteractionMode.PolarGrid or SvgEditorInteractionMode.Spiral)
            element.Fill = SvgPaintServer.None;
        AddElementInPictureCoordinates(element, "Draw " + mode);
        return element;
    }

    /// <summary>Commits a round basic brush or a document brush as a single undoable path.</summary>
    public void AddBrushStroke(SvgPath path, SvgBrushDefinition? brush = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        Execute("Draw paintbrush stroke", () =>
        {
            AddElementInPictureCoordinates(path, "Draw paintbrush stroke");
            path.StrokeLineCap = SvgStrokeLineCap.Round;
            path.StrokeLineJoin = SvgStrokeLineJoin.Round;
            if (brush is not null)
                SvgBrushLibraryService.Apply(path, brush);
        });
    }

    /// <summary>Creates native textPath markup next to its selected path in the same coordinate system.</summary>
    public SvgText AddTextOnSelectedPath(string value)
    {
        ThrowIfDisposed();
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        if (_selection.Count != 1 || _selection[0] is not SvgPath path || path.Parent is not { } parent)
            throw new InvalidOperationException("Select one path before placing type on it.");
        EnsureSelectionEditable();
        var text = new SvgText { FontFamily = Tools.CurrentFontFamily, FontSize = 32 };
        Execute("Type on a path", () =>
        {
            if (string.IsNullOrEmpty(path.ID))
                path.ID = CreateId("text-path");
            text.Children.Add(new SvgTextPath { ReferencedPath = new Uri("#" + path.ID, UriKind.Relative), Text = value });
            AddElement(text, "Type on a path");
            if (!ReferenceEquals(parent, Document))
            {
                text.Parent.Children.Remove(text);
                parent.Children.Add(text);
            }
        });
        return text;
    }

    /// <summary>Creates a reusable paragraph story from a canvas drag, in root SVG user units.</summary>
    public SvgGroup AddTextArea(SKRect pictureBounds, string value)
    {
        ThrowIfDisposed();
        if (Document is not { } document)
            throw new InvalidOperationException("Open a document before drawing a text frame.");
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        ValidatePositive(pictureBounds.Width, nameof(pictureBounds));
        ValidatePositive(pictureBounds.Height, nameof(pictureBounds));
        if (!IsFinite(pictureBounds.Left) || !IsFinite(pictureBounds.Top) || !IsFinite(pictureBounds.Right) || !IsFinite(pictureBounds.Bottom))
            throw new ArgumentOutOfRangeException(nameof(pictureBounds));
        var root = GetSceneNode(document) ?? Renderer.RetainedSceneGraph?.Root;
        var matrix = root is null ? SKMatrix.Identity : ToMatrix(root.TotalTransform);
        if (!matrix.TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        var bounds = inverse.MapRect(pictureBounds);
        var story = new SvgTextStory
        {
            Text = value,
            DefaultStyle = new SvgStoryTextStyle { FontFamily = Tools.CurrentFontFamily, FontSize = 24, Fill = CurrentFill }
        };
        story.Frames.Add(new SvgTextFrameDefinition
        {
            X = bounds.Left,
            Y = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Padding = Math.Min(8, Math.Min(bounds.Width, bounds.Height) / 8)
        });
        return SvgTextStoryService.Apply(this, story);
    }

    private static string PaintKey(SvgPaintServer? paint)
        => paint is SvgGradientServer gradient && !string.IsNullOrEmpty(gradient.ID) ? "url(#" + gradient.ID + ")"
            : paint?.ToString() ?? "none";
}
