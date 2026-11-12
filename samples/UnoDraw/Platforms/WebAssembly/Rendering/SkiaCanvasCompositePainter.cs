using SkiaSharp;
using Svg.Editor.Skia.Uno;

namespace UnoDraw.Rendering;

/// <summary>Shares exact document caching and feedback order between CPU transports.</summary>
internal static class SkiaCanvasCompositePainter
{
    internal static bool Render<TState>(SkiaCanvasRasterState.Paint paint, ISkiaCanvasLayers layers,
        CanvasPixelSize pixels, TState state, Func<TState, bool> isCurrent, ref bool documentDirty, out bool reused)
    {
        reused = !documentDirty && layers.IsDocumentCurrent(pixels.LogicalSize) && paint.TryRestoreDocument();
        if (!reused)
        {
            // Consume before painting so reentrant full invalidation remains
            // pending. A callback can unload or replace either layer owner.
            documentDirty = false;
            paint.Buffer.Canvas.Clear(SKColors.Transparent);
            if (!paint.TryRender((Layers: layers, Pixels: pixels),
                    static (value, canvas) => RenderLayer(canvas, value.Layers, value.Pixels, document: true))
                || !isCurrent(state) || !layers.IsDocumentCurrent(pixels.LogicalSize) || !paint.TryStoreDocument())
            {
                documentDirty = true;
                return false;
            }
        }

        return paint.TryRender((Layers: layers, Pixels: pixels),
                static (value, canvas) => RenderLayer(canvas, value.Layers, value.Pixels, document: false))
            && isCurrent(state);
    }

    private static void RenderLayer(SKCanvas canvas, ISkiaCanvasLayers layers, CanvasPixelSize pixels, bool document)
    {
        var saveCount = canvas.SaveCount;
        canvas.Save();
        try
        {
            canvas.Scale((float)pixels.Scale);
            canvas.ClipRect(new SKRect(0, 0, (float)pixels.LogicalSize.Width, (float)pixels.LogicalSize.Height));
            if (document)
                layers.RenderDocument(canvas, pixels.LogicalSize);
            else
                layers.RenderInteraction(canvas, pixels.LogicalSize);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }
}
