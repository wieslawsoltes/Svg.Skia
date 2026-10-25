using System;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Centers the selection as one arrangement, preserving relative positions and one-step undo.</summary>
    public void CenterSelectionOnArtboard()
    {
        ThrowIfDisposed();
        var targets = CaptureArrangement();
        if (targets.Length == 0) return;
        var artboard = Renderer.Picture?.CullRect ?? throw new InvalidOperationException("The document has no rendered artboard.");
        var union = SvgArrangementService.Union(targets.Select(item => item.Bounds).ToArray());
        // Reuse the planner's finite-coordinate validation for both axes before mutation.
        var x = SvgArrangementService.Align(new[] { union }, artboard, SvgAlignment.HorizontalCenter)[0].X;
        var y = SvgArrangementService.Align(new[] { union }, artboard, SvgAlignment.VerticalCenter)[0].Y;
        ApplyArrangement("Center selection on artboard", targets.Select(item => item.Element).ToArray(),
            Enumerable.Repeat(new SKPoint(x, y), targets.Length).ToArray());
    }
}
