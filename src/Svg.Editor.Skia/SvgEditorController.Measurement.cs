using System;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A read-only measurement in the SVG document's root user coordinates.</summary>
public readonly struct SvgEditorMeasurement
{
    internal SvgEditorMeasurement(SKPoint start, SKPoint end)
    {
        Start = start;
        End = end;
        DeltaX = (double)end.X - start.X;
        DeltaY = (double)end.Y - start.Y;
        Distance = Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY);
        AngleDegrees = Math.Atan2(DeltaY, DeltaX) * 180 / Math.PI;
    }
    public SKPoint Start { get; }
    public SKPoint End { get; }
    public double DeltaX { get; }
    public double DeltaY { get; }
    public double Distance { get; }
    public double AngleDegrees { get; }
}

public sealed partial class SvgEditorController
{
    /// <summary>Measures a canvas segment through the imported root/viewBox transform without a document edit.</summary>
    public SvgEditorMeasurement MeasureInDocumentCoordinates(SKPoint pictureStart, SKPoint pictureEnd)
    {
        ThrowIfDisposed();
        if (!IsFinite(pictureStart.X) || !IsFinite(pictureStart.Y) || !IsFinite(pictureEnd.X) || !IsFinite(pictureEnd.Y))
            throw new ArgumentOutOfRangeException(nameof(pictureEnd));
        var root = Document is null ? null : GetSceneNode(Document) ?? Renderer.RetainedSceneGraph?.Root;
        var matrix = root is null ? SKMatrix.Identity : ToMatrix(root.TotalTransform);
        if (!matrix.TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        var start = inverse.MapPoint(pictureStart);
        var end = inverse.MapPoint(pictureEnd);
        if (!IsFinite(start.X) || !IsFinite(start.Y) || !IsFinite(end.X) || !IsFinite(end.Y))
            throw new InvalidOperationException("The measured coordinates are outside the supported range.");
        return new SvgEditorMeasurement(start, end);
    }
}
