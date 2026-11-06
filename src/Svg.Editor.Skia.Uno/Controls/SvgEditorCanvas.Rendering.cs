using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Svg.Pathing;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_disposed)
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var resources = _resources ??= new CanvasResources();
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        resources.Fill.Color = IsDarkTheme ? new SKColor(31, 34, 42) : new SKColor(233, 236, 242);
        canvas.DrawRect(SKRect.Create((float)area.Width, (float)area.Height), resources.Fill);

        if (Controller?.Renderer.Picture is { } picture)
        {
            DrawArtboard(canvas, picture.CullRect, resources);
            using (var documentRestore = new SKAutoCanvasRestore(canvas))
            {
                canvas.Concat(Viewport.Matrix);
                if (ShowGrid)
                {
                    DrawGrid(canvas, picture.CullRect, resources);
                }

                // Replays the existing Svg.Skia display list into the host surface. No bitmap
                // readback, reparsing, or document mutation is performed by a camera redraw.
                Controller.Renderer.Draw(canvas);
                DrawSelectionGhost(canvas, resources);
            }

            DrawSelection(canvas, resources);
            DrawGesture(canvas, resources);
            DrawPathNodes(canvas, resources);
        }

        LastRenderMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    }

    private SKPoint ViewCenter => new((float)(Viewport.Width / 2), (float)(Viewport.Height / 2));

    private void DrawArtboard(SKCanvas canvas, SKRect world, CanvasResources resources)
    {
        var topLeft = Viewport.WorldToView(new SKPoint(world.Left, world.Top));
        var bottomRight = Viewport.WorldToView(new SKPoint(world.Right, world.Bottom));
        var rect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        for (var spread = 12; spread >= 2; spread -= 2)
        {
            resources.Fill.Color = new SKColor(10, 17, 35, (byte)(IsDarkTheme ? 8 : 4));
            canvas.DrawRoundRect(new SKRect(rect.Left - spread, rect.Top - spread + 3,
                rect.Right + spread, rect.Bottom + spread + 3), spread, spread, resources.Fill);
        }

        resources.Fill.Color = SKColors.White;
        canvas.DrawRect(rect, resources.Fill);
        if (ShowTransparency)
        {
            // One repeating shader draw instead of thousands of individual checker tiles.
            canvas.DrawRect(rect, resources.CheckerPaint);
        }

        resources.Stroke.Color = new SKColor(150, 159, 177, 90);
        resources.Stroke.StrokeWidth = 1;
        canvas.DrawRect(rect, resources.Stroke);
        resources.Fill.Color = IsDarkTheme ? new SKColor(168, 179, 200) : new SKColor(98, 111, 135);
        canvas.DrawText("Artboard", rect.Left, rect.Top - 10, SKTextAlign.Left, resources.LabelFont, resources.Fill);
    }

    private void DrawGrid(SKCanvas canvas, SKRect artboard, CanvasResources resources)
    {
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(artboard);
        var visible = SKRect.Intersect(artboard, Viewport.VisibleWorldBounds);
        if (visible.IsEmpty)
        {
            return;
        }

        var step = Controller?.Selection.GridSize ?? 10;
        if (!double.IsFinite(step) || step <= 0)
        {
            step = 10;
        }

        while (step * Zoom < 14)
        {
            step *= 2;
        }

        resources.Stroke.Color = new SKColor(158, 170, 192, 54);
        resources.Stroke.StrokeWidth = (float)(1 / Zoom);
        var firstX = Math.Floor(visible.Left / step) * step;
        var firstY = Math.Floor(visible.Top / step) * step;
        var columns = Math.Min(4096, (int)Math.Ceiling(visible.Width / step) + 2);
        var rows = Math.Min(4096, (int)Math.Ceiling(visible.Height / step) + 2);
        for (var i = 0; i < columns; i++)
        {
            var x = (float)(firstX + i * step);
            canvas.DrawLine(x, visible.Top, x, visible.Bottom, resources.Stroke);
        }

        for (var i = 0; i < rows; i++)
        {
            var y = (float)(firstY + i * step);
            canvas.DrawLine(visible.Left, y, visible.Right, y, resources.Stroke);
        }
    }

    private void DrawSelection(SKCanvas canvas, CanvasResources resources)
    {
        if (Controller?.SelectionBounds is not { } bounds)
        {
            return;
        }

        resources.Stroke.Color = resources.Accent;
        resources.Stroke.StrokeWidth = 1;
        foreach (var selected in Controller.SelectedElements)
        {
            if (Controller.GetSceneNode(selected) is { } node)
            {
                var info = Controller.Selection.GetBoundsInfo(node, () => (float)Zoom);
                DrawQuad(canvas, info.TL, info.TR, info.BR, info.BL, SKMatrix.Identity, resources.Stroke);
            }
        }

        var transform = _gesture is Gesture.Move or Gesture.Resize or Gesture.Rotate
            ? _previewTransform : SKMatrix.Identity;
        DrawQuad(canvas, new(bounds.Left, bounds.Top), new(bounds.Right, bounds.Top),
            new(bounds.Right, bounds.Bottom), new(bounds.Left, bounds.Bottom), transform, resources.Stroke);

        if (IsNodeTool(Tool) || !CanEditSelection)
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            var center = Viewport.WorldToView(transform.MapPoint(GetHandle(bounds, i)));
            resources.Fill.Color = SKColors.White;
            var handle = SKRect.Create(center.X - 3.5f, center.Y - 3.5f, 7, 7);
            canvas.DrawRect(handle, resources.Fill);
            canvas.DrawRect(handle, resources.Stroke);
        }

        var top = Viewport.WorldToView(transform.MapPoint(GetHandle(bounds, 1)));
        var rotation = new SKPoint(top.X, top.Y - 24);
        canvas.DrawLine(top, rotation, resources.Stroke);
        canvas.DrawCircle(rotation, 4, resources.Fill);
        canvas.DrawCircle(rotation, 4, resources.Stroke);
    }

    private void DrawQuad(SKCanvas canvas, SKPoint tl, SKPoint tr, SKPoint br, SKPoint bl,
        SKMatrix transform, SKPaint paint)
    {
        tl = Viewport.WorldToView(transform.MapPoint(tl));
        tr = Viewport.WorldToView(transform.MapPoint(tr));
        br = Viewport.WorldToView(transform.MapPoint(br));
        bl = Viewport.WorldToView(transform.MapPoint(bl));
        canvas.DrawLine(tl, tr, paint);
        canvas.DrawLine(tr, br, paint);
        canvas.DrawLine(br, bl, paint);
        canvas.DrawLine(bl, tl, paint);
    }

    private void DrawGesture(SKCanvas canvas, CanvasResources resources)
    {
        resources.Stroke.Color = resources.Accent;
        resources.Stroke.StrokeWidth = 1.5f;
        resources.Fill.Color = resources.Accent.WithAlpha(22);

        if (_gesture == Gesture.Marquee)
        {
            var rect = RectBetween(_startView, _lastView);
            canvas.DrawRect(rect, resources.Fill);
            canvas.DrawRect(rect, resources.Stroke);
            return;
        }

        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(Viewport.Matrix);
        resources.Stroke.StrokeWidth = (float)(1.5 / Zoom);
        if (_gesture == Gesture.Create && !IsPenTool(Tool))
        {
            var rect = RectBetween(_startWorld, _currentWorld);
            switch (Tool)
            {
                case SvgEditorToolKind.Rect:
                case SvgEditorToolKind.Image:
                    canvas.DrawRect(rect, resources.Fill);
                    canvas.DrawRect(rect, resources.Stroke);
                    break;
                case SvgEditorToolKind.Ellipse:
                    canvas.DrawOval(rect, resources.Fill);
                    canvas.DrawOval(rect, resources.Stroke);
                    break;
                case SvgEditorToolKind.Circle:
                    var radius = Math.Max(rect.Width, rect.Height) / 2;
                    canvas.DrawCircle(rect.MidX, rect.MidY, radius, resources.Fill);
                    canvas.DrawCircle(rect.MidX, rect.MidY, radius, resources.Stroke);
                    break;
                case SvgEditorToolKind.Freehand when _freehandPreview is { } path:
                    using (var snapshot = path.Snapshot())
                    {
                        canvas.DrawPath(snapshot, resources.Stroke);
                    }

                    break;
                default:
                    canvas.DrawLine(_startWorld, _currentWorld, resources.Stroke);
                    break;
            }
        }

        if (IsPenTool(Tool) && (_penNodes.Count > 0 || _gesture == Gesture.Create))
        {
            using var preview = CreatePenPreview();
            canvas.DrawPath(preview, resources.Stroke);
            foreach (var node in _penNodes)
            {
                resources.Fill.Color = SKColors.White;
                canvas.DrawCircle(node.Position, (float)(3.5 / Zoom), resources.Fill);
                canvas.DrawCircle(node.Position, (float)(3.5 / Zoom), resources.Stroke);
            }

            if (_gesture == Gesture.Create && Tool == SvgEditorToolKind.PathCubic)
            {
                var tangent = Subtract(_currentWorld, _startWorld);
                canvas.DrawLine(Subtract(_startWorld, tangent), _currentWorld, resources.Stroke);
                canvas.DrawCircle(_currentWorld, (float)(3 / Zoom), resources.Stroke);
            }
        }
    }

    private void DrawPathNodes(SKCanvas canvas, CanvasResources resources)
    {
        if (_vertexOriginal is not null && IsNodeTool(Tool))
        {
            DrawVertices(canvas, resources);
            return;
        }

        if (!_nodePreview.IsEditing || Tool != SvgEditorToolKind.PathSelect)
        {
            return;
        }

        resources.Stroke.Color = resources.Accent;
        resources.Stroke.StrokeWidth = 1;
        if (_nodeOutline is not null)
        {
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.Concat(Viewport.Matrix);
            canvas.Concat(ToSkia(_nodePreview.PathMatrix));
            resources.Stroke.StrokeWidth = 0;
            canvas.DrawPath(_nodeOutline, resources.Stroke);
            resources.Stroke.StrokeWidth = 1;
        }

        var points = _nodePreview.PathPoints;
        for (var i = 0; i < points.Count; i++)
        {
            var node = points[i];
            var center = NodeViewPoint(node.Point);
            if (node.Type != 0)
            {
                var anchorIndex = node.Type == 2 ? i + 1 : i - 1;
                if (node.Segment is SvgQuadraticCurveSegment)
                {
                    anchorIndex = i + 1;
                }

                if (anchorIndex >= 0 && anchorIndex < points.Count)
                {
                    canvas.DrawLine(center, NodeViewPoint(points[anchorIndex].Point), resources.Stroke);
                }

                resources.Fill.Color = i == _nodePreview.ActivePoint ? resources.Accent : SKColors.White;
                canvas.DrawCircle(center, 3, resources.Fill);
                canvas.DrawCircle(center, 3, resources.Stroke);
            }
            else
            {
                resources.Fill.Color = i == _nodePreview.ActivePoint ? resources.Accent : SKColors.White;
                var rect = SKRect.Create(center.X - 3.5f, center.Y - 3.5f, 7, 7);
                canvas.DrawRect(rect, resources.Fill);
                canvas.DrawRect(rect, resources.Stroke);
            }
        }
    }

    private SKPoint NodeViewPoint(Shim.SKPoint point)
    {
        var world = _nodePreview.PathMatrix.MapPoint(point);
        return Viewport.WorldToView(new SKPoint(world.X, world.Y));
    }

    private sealed class CanvasResources : IDisposable
    {
        private readonly SKShader _checkerShader;

        public CanvasResources()
        {
            using var bitmap = new SKBitmap(16, 16);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = new SKColor(239, 241, 245) })
            {
                canvas.Clear(SKColors.White);
                canvas.DrawRect(0, 0, 8, 8, paint);
                canvas.DrawRect(8, 8, 8, 8, paint);
            }

            _checkerShader = bitmap.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            CheckerPaint = new SKPaint { Shader = _checkerShader };
        }

        public SKColor Accent { get; } = new(63, 111, 242);
        public SKPaint Fill { get; } = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        public SKPaint Stroke { get; } = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        public SKFont LabelFont { get; } = new(SKTypeface.Default, 11);
        public SKPaint CheckerPaint { get; }
        public SKPaint GhostPaint { get; } = new() { Color = SKColors.White.WithAlpha(165) };

        public void Dispose()
        {
            Fill.Dispose();
            Stroke.Dispose();
            LabelFont.Dispose();
            CheckerPaint.Dispose();
            GhostPaint.Dispose();
            _checkerShader.Dispose();
        }
    }
}
