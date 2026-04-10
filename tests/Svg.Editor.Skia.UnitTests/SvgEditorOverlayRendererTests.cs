using System;
using Svg.Editor.Skia;
using Svg.Editor.Svg.Models;
using Xunit;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorOverlayRendererTests
{
    [Fact]
    public void Draw_DoesNotThrow_WithEmptySelection()
    {
        var renderer = new SvgEditorOverlayRenderer();
        using var bitmap = new SK.SKBitmap(16, 16);
        using var canvas = new SK.SKCanvas(bitmap);

        renderer.Draw(
            canvas,
            picture: null,
            rootBounds: null,
            artboardBounds: null,
            scale: 1f,
            snapToGrid: false,
            showGrid: false,
            gridSize: 10d,
            layers: Array.Empty<LayerEntry>(),
            selectedLayer: null,
            selectedVisuals: Array.Empty<SelectionVisualInfo>(),
            getBounds: _ => default,
            polyEditing: false,
            editPolyVisual: null,
            editPolyline: false,
            polyPoints: Array.Empty<ShimSkiaSharp.SKPoint>(),
            polyMatrix: ShimSkiaSharp.SKMatrix.CreateIdentity());
    }
}
