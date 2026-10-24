using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Divides sibling shapes by all selected filled outlines in paint order. Each cell
    /// inherits the frontmost covering source's presentation and remains in that source's
    /// local coordinate system. This is a geometric operation, not appearance flattening:
    /// strokes are applied to new edges and object-bounding-box paints use each new path's
    /// bounds. Disconnected portions of a cell remain a compound path.
    /// </summary>
    public void DivideSelection(int maximumPieces = SvgPathDivisionService.DefaultMaximumPieces,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (maximumPieces < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumPieces));
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSelectionEditable();
        var elements = TopLevelSelection();
        if (elements.Length < 2)
            return;
        var parent = elements[0].Parent;
        if (parent is null || elements.Any(element => !ReferenceEquals(element.Parent, parent)))
            throw new InvalidOperationException("Move the selected objects into the same layer or group before dividing them.");
        if (elements.Length > SvgPathDivisionService.MaximumOperands)
            throw new InvalidOperationException($"Divide at most {SvgPathDivisionService.MaximumOperands} objects at a time.");
        elements = elements.OrderBy(element => parent.Children.IndexOf(element)).ToArray();
        var geometry = new List<SK.SKPath>(elements.Length);
        var inverses = new SK.SKMatrix[elements.Length];
        try
        {
            for (var index = 0; index < elements.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = elements[index];
                ValidateDivisionOperand(element);
                var node = GetSceneNode(element)
                    ?? throw new InvalidOperationException("A selected object has no editable geometry.");
                var world = ToMatrix(node.TotalTransform);
                ValidateMatrix(world);
                if (!world.TryInvert(out inverses[index]))
                    throw new InvalidOperationException("A selected object has a singular transform.");
                var path = CreateGeometryPath(element)
                    ?? throw new InvalidOperationException("Convert the selected objects to paths before dividing them.");
                geometry.Add(path);
                path.FillType = element.FillRule == SvgFillRule.EvenOdd
                    ? SK.SKPathFillType.EvenOdd : SK.SKPathFillType.Winding;
                path.Transform(world);
            }
            using var result = SvgPathDivisionService.Divide(geometry, maximumPieces, cancellationToken);
            var replacements = new List<SvgPath>[elements.Length];
            for (var index = 0; index < replacements.Length; index++)
                replacements[index] = new List<SvgPath>();
            foreach (var piece in result.Pieces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Canonical winding contours also work under an inherited even-odd rule.
                // This avoids copying a source fill-rule onto arbitrary path-op contours.
                using var local = piece.Geometry.ToWinding()
                    ?? throw new InvalidOperationException("A divided region could not be normalized for SVG.");
                local.Transform(inverses[piece.SourceIndex]);
                var path = CloneAsPath(elements[piece.SourceIndex], PathService.ToSvgPathData(local), true);
                var siblings = replacements[piece.SourceIndex];
                if (siblings.Count != 0 || string.IsNullOrEmpty(path.ID))
                    path.ID = CreateId("divide");
                siblings.Add(path);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // All geometry, serialization and validation finish before touching the live
            // DOM. A limit, cancellation or unsupported operand leaves history unchanged.
            Execute("Divide paths", () =>
            {
                _selection.Clear();
                for (var index = 0; index < elements.Length; index++)
                {
                    var insertionIndex = parent.Children.IndexOf(elements[index]);
                    parent.Children.Remove(elements[index]);
                    foreach (var path in replacements[index])
                    {
                        parent.Children.Insert(insertionIndex++, path);
                        RebindPaintServers(path);
                        _selection.Add(path);
                    }
                }
            });
        }
        finally
        {
            foreach (var path in geometry)
                path.Dispose();
        }
    }

    private static void ValidateDivisionOperand(SvgVisualElement element)
    {
        if (element is not (SvgPath or SvgRectangle or SvgCircle or SvgEllipse or SvgPolygon or SvgPolyline))
            throw new InvalidOperationException("Divide supports paths and basic shapes. Convert other objects to paths first.");
        if (element.Children.Count != 0)
            throw new InvalidOperationException("Divide requires static shapes without child elements. Bake animations or edit a plain path copy first.");
        if (element.Filter is not null || element.ClipPath is not null ||
            (element.TryGetAttribute("mask", out var mask) && !string.IsNullOrWhiteSpace(mask) && mask != "none"))
            throw new InvalidOperationException("Release the selected objects' clips, masks and filters before dividing their geometry.");
        switch (element)
        {
            case SvgRectangle rectangle:
                ValidateDivisionUnits(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height,
                    rectangle.CornerRadiusX, rectangle.CornerRadiusY);
                break;
            case SvgCircle circle:
                ValidateDivisionUnits(circle.CenterX, circle.CenterY, circle.Radius);
                break;
            case SvgEllipse ellipse:
                ValidateDivisionUnits(ellipse.CenterX, ellipse.CenterY, ellipse.RadiusX, ellipse.RadiusY);
                break;
            case SvgPolyline polyline:
                foreach (var unit in polyline.Points)
                    ValidateDivisionUnits(unit);
                break;
            case SvgPolygon polygon:
                foreach (var unit in polygon.Points)
                    ValidateDivisionUnits(unit);
                break;
        }
    }

    private static void ValidateDivisionUnits(params SvgUnit[] units)
    {
        foreach (var unit in units)
        {
            if (!IsFinite(unit.Value))
                throw new InvalidOperationException("Divide requires finite shape coordinates.");
            if (unit.Value != 0 && unit.Type is not (SvgUnitType.User or SvgUnitType.Pixel))
                throw new InvalidOperationException("Resolve percentage, font-relative and physical geometry units to SVG user units before dividing.");
        }
    }
}
