using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;

namespace Svg.Skia;

public partial class SkiaModel
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawCommands(IList<CanvasCommand> commands, SkiaSharp.SKCanvas skCanvas)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i])
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                    if (clipPathCanvasCommand.ClipPath is { })
                    {
                        var path = ToSKPath(clipPathCanvasCommand.ClipPath);
                        var operation = ToSKClipOperation(clipPathCanvasCommand.Operation);
                        skCanvas.ClipPath(path, operation, clipPathCanvasCommand.Antialias);
                    }
                    break;
                case ClipRectCanvasCommand clipRectCanvasCommand:
                    {
                        var rect = ToSKRect(clipRectCanvasCommand.Rect);
                        var operation = ToSKClipOperation(clipRectCanvasCommand.Operation);
                        skCanvas.ClipRect(rect, operation, clipRectCanvasCommand.Antialias);
                        break;
                    }
                case SaveCanvasCommand _:
                    skCanvas.Save();
                    break;
                case RestoreCanvasCommand restoreCanvasCommand:
                    i = ReplayConsecutiveRestoreCommands(commands, i, restoreCanvasCommand, skCanvas);
                    break;
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    {
                        var matrix = ToSKMatrix(setMatrixCanvasCommand.DeltaMatrix);
                        skCanvas.Concat(ref matrix);
                        break;
                    }
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                    SaveLayer(skCanvas, saveLayerCanvasCommand);
                    break;
                case DrawImageCanvasCommand drawImageCanvasCommand:
                    if (drawImageCanvasCommand.Image is { })
                    {
                        var image = GetRenderImage(drawImageCanvasCommand.Image);
                        var source = ToSKRect(drawImageCanvasCommand.Source);
                        var dest = ToSKRect(drawImageCanvasCommand.Dest);
                        var paint = GetRenderPaint(drawImageCanvasCommand.Paint);
                        skCanvas.DrawImage(image, source, dest, paint);
                    }
                    break;
                case DrawPictureCanvasCommand drawPictureCanvasCommand:
                    if (drawPictureCanvasCommand.Picture is { } picture)
                    {
                        if (TryGetCachedPicture(picture, out var cachedPicture))
                        {
                            skCanvas.DrawPicture(cachedPicture);
                        }
                        else if (picture.Commands is { } nestedCommands)
                        {
                            DrawCommands(nestedCommands, skCanvas);
                        }
                    }
                    break;
                case DrawPathCanvasCommand drawPathCanvasCommand:
                    if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                    {
                        var path = GetRenderPath(drawPathCanvasCommand.Path);
                        var paint = GetRenderPaint(drawPathCanvasCommand.Paint);
                        skCanvas.DrawPath(path, paint);
                    }
                    break;
                case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                    if (drawTextBlobCanvasCommand.TextBlob?.Points is { } && drawTextBlobCanvasCommand.Paint is { } sourcePaint)
                    {
                        var paint = GetRenderPaint(sourcePaint);
                        if (paint is null)
                        {
                            break;
                        }

                        var textBlob = GetCachedPositionedTextBlob(drawTextBlobCanvasCommand, paint);
                        if (textBlob is not null)
                        {
                            skCanvas.DrawText(textBlob, 0, 0, paint);
                        }
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    if (drawTextCanvasCommand.Paint is { })
                    {
                        var paint = GetRenderPaint(drawTextCanvasCommand.Paint);
                        if (paint is null)
                        {
                            break;
                        }

                        if (!TryDrawShapedText(skCanvas, drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint))
                        {
                            skCanvas.DrawText(drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint);
                        }
                    }
                    break;
                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                    if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                    {
                        var path = GetRenderPath(drawTextOnPathCanvasCommand.Path);
                        var paint = GetRenderPaint(drawTextOnPathCanvasCommand.Paint);
                        skCanvas.DrawTextOnPath(
                            drawTextOnPathCanvasCommand.Text,
                            path,
                            drawTextOnPathCanvasCommand.HOffset,
                            drawTextOnPathCanvasCommand.VOffset,
                            paint);
                    }
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawCommandsWireframe(IList<CanvasCommand> commands, SkiaSharp.SKCanvas skCanvas)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i])
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                    if (clipPathCanvasCommand.ClipPath is { })
                    {
                        var path = ToSKPath(clipPathCanvasCommand.ClipPath);
                        var operation = ToSKClipOperation(clipPathCanvasCommand.Operation);
                        skCanvas.ClipPath(path, operation, clipPathCanvasCommand.Antialias);
                    }
                    break;
                case ClipRectCanvasCommand clipRectCanvasCommand:
                    {
                        var rect = ToSKRect(clipRectCanvasCommand.Rect);
                        var operation = ToSKClipOperation(clipRectCanvasCommand.Operation);
                        skCanvas.ClipRect(rect, operation, clipRectCanvasCommand.Antialias);
                        break;
                    }
                case SaveCanvasCommand _:
                    skCanvas.Save();
                    break;
                case RestoreCanvasCommand restoreCanvasCommand:
                    i = ReplayConsecutiveRestoreCommands(commands, i, restoreCanvasCommand, skCanvas);
                    break;
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    {
                        var matrix = ToSKMatrix(setMatrixCanvasCommand.DeltaMatrix);
                        skCanvas.Concat(ref matrix);
                        break;
                    }
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                    SaveWireframeLayer(skCanvas, saveLayerCanvasCommand);
                    break;
                case DrawImageCanvasCommand drawImageCanvasCommand:
                    if (drawImageCanvasCommand.Image is { })
                    {
                        using var rectPath = new SkiaSharp.SKPath();
                        rectPath.AddRect(ToSKRect(drawImageCanvasCommand.Dest));
                        skCanvas.DrawPath(rectPath, ToWireframePaint(null));
                    }
                    break;
                case DrawPictureCanvasCommand drawPictureCanvasCommand:
                    if (drawPictureCanvasCommand.Picture?.Commands is { } nestedCommands)
                    {
                        DrawCommandsWireframe(nestedCommands, skCanvas);
                    }
                    break;
                case DrawPathCanvasCommand drawPathCanvasCommand:
                    if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                    {
                        var path = GetRenderPath(drawPathCanvasCommand.Path);
                        var paint = ToWireframePaint(drawPathCanvasCommand.Paint);
                        skCanvas.DrawPath(path, paint);
                    }
                    break;
                case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                    if (drawTextBlobCanvasCommand.TextBlob?.Points is { } && drawTextBlobCanvasCommand.Paint is { } sourcePaint)
                    {
                        var paint = ToWireframePaint(sourcePaint);
                        if (paint is null)
                        {
                            break;
                        }

                        var textBlob = GetCachedPositionedTextBlob(drawTextBlobCanvasCommand, paint);
                        if (textBlob is not null)
                        {
                            skCanvas.DrawText(textBlob, 0, 0, paint);
                        }
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    if (drawTextCanvasCommand.Paint is { })
                    {
                        var paint = ToWireframePaint(drawTextCanvasCommand.Paint);
                        if (paint is null)
                        {
                            break;
                        }

                        if (!TryDrawShapedText(skCanvas, drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint))
                        {
                            skCanvas.DrawText(drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint);
                        }
                    }
                    break;
                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                    if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                    {
                        var path = GetRenderPath(drawTextOnPathCanvasCommand.Path);
                        var paint = ToWireframePaint(drawTextOnPathCanvasCommand.Paint);
                        skCanvas.DrawTextOnPath(
                            drawTextOnPathCanvasCommand.Text,
                            path,
                            drawTextOnPathCanvasCommand.HOffset,
                            drawTextOnPathCanvasCommand.VOffset,
                            paint);
                    }
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReplayConsecutiveRestoreCommands(
        IList<CanvasCommand> commands,
        int index,
        RestoreCanvasCommand restoreCanvasCommand,
        SkiaSharp.SKCanvas skCanvas)
    {
        var targetCount = restoreCanvasCommand.Count;

        while (index + 1 < commands.Count && commands[index + 1] is RestoreCanvasCommand nextRestore)
        {
            index++;
            targetCount = nextRestore.Count;
        }

        skCanvas.RestoreToCount(targetCount + 1);
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveLayer(SkiaSharp.SKCanvas skCanvas, SaveLayerCanvasCommand saveLayerCanvasCommand)
    {
        var paint = saveLayerCanvasCommand.Paint is { } sourcePaint
            ? GetRenderPaint(sourcePaint)
            : null;

        SaveLayer(skCanvas, saveLayerCanvasCommand.Bounds, paint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveWireframeLayer(SkiaSharp.SKCanvas skCanvas, SaveLayerCanvasCommand saveLayerCanvasCommand)
    {
        var paint = saveLayerCanvasCommand.Paint is { } sourcePaint
            ? ToWireframePaint(sourcePaint)
            : null;

        SaveLayer(skCanvas, saveLayerCanvasCommand.Bounds, paint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SaveLayer(SkiaSharp.SKCanvas skCanvas, SKRect? bounds, SkiaSharp.SKPaint? paint)
    {
        if (bounds is { } layerBounds)
        {
            skCanvas.SaveLayer(
                new SkiaSharp.SKRect(layerBounds.Left, layerBounds.Top, layerBounds.Right, layerBounds.Bottom),
                paint);
            return;
        }

        if (paint is not null)
        {
            skCanvas.SaveLayer(paint);
        }
        else
        {
            skCanvas.SaveLayer();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawCanvasCommand(CanvasCommand canvasCommand, SkiaSharp.SKCanvas skCanvas)
    {
        switch (canvasCommand)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
                if (clipPathCanvasCommand.ClipPath is { })
                {
                    var path = ToSKPath(clipPathCanvasCommand.ClipPath);
                    var operation = ToSKClipOperation(clipPathCanvasCommand.Operation);
                    skCanvas.ClipPath(path, operation, clipPathCanvasCommand.Antialias);
                }
                break;
            case ClipRectCanvasCommand clipRectCanvasCommand:
                {
                    var rect = ToSKRect(clipRectCanvasCommand.Rect);
                    var operation = ToSKClipOperation(clipRectCanvasCommand.Operation);
                    skCanvas.ClipRect(rect, operation, clipRectCanvasCommand.Antialias);
                    break;
                }
            case SaveCanvasCommand _:
                skCanvas.Save();
                break;
            case RestoreCanvasCommand restoreCanvasCommand:
                skCanvas.RestoreToCount(restoreCanvasCommand.Count + 1);
                break;
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    var matrix = ToSKMatrix(setMatrixCanvasCommand.DeltaMatrix);
                    skCanvas.Concat(ref matrix);
                    break;
                }
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
                SaveLayer(skCanvas, saveLayerCanvasCommand);
                break;
            case DrawImageCanvasCommand drawImageCanvasCommand:
                if (drawImageCanvasCommand.Image is { })
                {
                    var image = GetRenderImage(drawImageCanvasCommand.Image);
                    var source = ToSKRect(drawImageCanvasCommand.Source);
                    var dest = ToSKRect(drawImageCanvasCommand.Dest);
                    var paint = GetRenderPaint(drawImageCanvasCommand.Paint);
                    skCanvas.DrawImage(image, source, dest, paint);
                }
                break;
            case DrawPictureCanvasCommand drawPictureCanvasCommand:
                if (drawPictureCanvasCommand.Picture is { } picture)
                {
                    if (TryGetCachedPicture(picture, out var cachedPicture))
                    {
                        skCanvas.DrawPicture(cachedPicture);
                    }
                    else if (picture.Commands is { } nestedCommands)
                    {
                        DrawCommands(nestedCommands, skCanvas);
                    }
                }
                break;
            case DrawPathCanvasCommand drawPathCanvasCommand:
                if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                {
                    var path = GetRenderPath(drawPathCanvasCommand.Path);
                    var paint = GetRenderPaint(drawPathCanvasCommand.Paint);
                    skCanvas.DrawPath(path, paint);
                }
                break;
            case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                if (drawTextBlobCanvasCommand.TextBlob?.Points is { } && drawTextBlobCanvasCommand.Paint is { } sourcePaint)
                {
                    var paint = GetRenderPaint(sourcePaint);
                    if (paint is null)
                    {
                        break;
                    }

                    var textBlob = GetCachedPositionedTextBlob(drawTextBlobCanvasCommand, paint);
                    if (textBlob is not null)
                    {
                        skCanvas.DrawText(textBlob, 0, 0, paint);
                    }
                }
                break;
            case DrawTextCanvasCommand drawTextCanvasCommand:
                if (drawTextCanvasCommand.Paint is { })
                {
                    var paint = GetRenderPaint(drawTextCanvasCommand.Paint);
                    if (paint is null)
                    {
                        break;
                    }

                    if (!TryDrawShapedText(skCanvas, drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint))
                    {
                        skCanvas.DrawText(drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint);
                    }
                }
                break;
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                {
                    var path = GetRenderPath(drawTextOnPathCanvasCommand.Path);
                    var paint = GetRenderPaint(drawTextOnPathCanvasCommand.Paint);
                    skCanvas.DrawTextOnPath(
                        drawTextOnPathCanvasCommand.Text,
                        path,
                        drawTextOnPathCanvasCommand.HOffset,
                        drawTextOnPathCanvasCommand.VOffset,
                        paint);
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawCanvasCommandWireframe(CanvasCommand canvasCommand, SkiaSharp.SKCanvas skCanvas)
    {
        switch (canvasCommand)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
                if (clipPathCanvasCommand.ClipPath is { })
                {
                    var path = ToSKPath(clipPathCanvasCommand.ClipPath);
                    var operation = ToSKClipOperation(clipPathCanvasCommand.Operation);
                    skCanvas.ClipPath(path, operation, clipPathCanvasCommand.Antialias);
                }
                break;
            case ClipRectCanvasCommand clipRectCanvasCommand:
                {
                    var rect = ToSKRect(clipRectCanvasCommand.Rect);
                    var operation = ToSKClipOperation(clipRectCanvasCommand.Operation);
                    skCanvas.ClipRect(rect, operation, clipRectCanvasCommand.Antialias);
                    break;
                }
            case SaveCanvasCommand _:
                skCanvas.Save();
                break;
            case RestoreCanvasCommand restoreCanvasCommand:
                skCanvas.RestoreToCount(restoreCanvasCommand.Count + 1);
                break;
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    var matrix = ToSKMatrix(setMatrixCanvasCommand.DeltaMatrix);
                    skCanvas.Concat(ref matrix);
                    break;
                }
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
                SaveWireframeLayer(skCanvas, saveLayerCanvasCommand);
                break;
            case DrawImageCanvasCommand drawImageCanvasCommand:
                if (drawImageCanvasCommand.Image is { })
                {
                    using var rectPath = new SkiaSharp.SKPath();
                    rectPath.AddRect(ToSKRect(drawImageCanvasCommand.Dest));
                    skCanvas.DrawPath(rectPath, ToWireframePaint(null));
                }
                break;
            case DrawPictureCanvasCommand drawPictureCanvasCommand:
                if (drawPictureCanvasCommand.Picture?.Commands is { } nestedCommands)
                {
                    DrawCommandsWireframe(nestedCommands, skCanvas);
                }
                break;
            case DrawPathCanvasCommand drawPathCanvasCommand:
                if (drawPathCanvasCommand.Path is { } && drawPathCanvasCommand.Paint is { })
                {
                    var path = GetRenderPath(drawPathCanvasCommand.Path);
                    var paint = ToWireframePaint(drawPathCanvasCommand.Paint);
                    skCanvas.DrawPath(path, paint);
                }
                break;
            case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                if (drawTextBlobCanvasCommand.TextBlob?.Points is { } && drawTextBlobCanvasCommand.Paint is { } sourcePaint)
                {
                    var paint = ToWireframePaint(sourcePaint);
                    if (paint is null)
                    {
                        break;
                    }

                    var textBlob = GetCachedPositionedTextBlob(drawTextBlobCanvasCommand, paint);
                    if (textBlob is not null)
                    {
                        skCanvas.DrawText(textBlob, 0, 0, paint);
                    }
                }
                break;
            case DrawTextCanvasCommand drawTextCanvasCommand:
                if (drawTextCanvasCommand.Paint is { })
                {
                    var paint = ToWireframePaint(drawTextCanvasCommand.Paint);
                    if (paint is null)
                    {
                        break;
                    }

                    if (!TryDrawShapedText(skCanvas, drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint))
                    {
                        skCanvas.DrawText(drawTextCanvasCommand.Text, drawTextCanvasCommand.X, drawTextCanvasCommand.Y, paint);
                    }
                }
                break;
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                if (drawTextOnPathCanvasCommand.Path is { } && drawTextOnPathCanvasCommand.Paint is { })
                {
                    var path = GetRenderPath(drawTextOnPathCanvasCommand.Path);
                    var paint = ToWireframePaint(drawTextOnPathCanvasCommand.Paint);
                    skCanvas.DrawTextOnPath(
                        drawTextOnPathCanvasCommand.Text,
                        path,
                        drawTextOnPathCanvasCommand.HOffset,
                        drawTextOnPathCanvasCommand.VOffset,
                        paint);
                }
                break;
        }
    }
}
