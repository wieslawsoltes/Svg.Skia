using System.Collections.Generic;
using System.Diagnostics;

namespace ShimSkiaSharp;

public abstract record CanvasCommand;

public record ClipPathCanvasCommand(ClipPath? ClipPath, SKClipOperation Operation, bool Antialias) : CanvasCommand;

public record ClipRectCanvasCommand(SKRect Rect, SKClipOperation Operation, bool Antialias) : CanvasCommand;

public record DrawImageCanvasCommand(SKImage? Image, SKRect Source, SKRect Dest, SKPaint? Paint = null) : CanvasCommand;

public record DrawPathCanvasCommand(SKPath? Path, SKPaint? Paint) : CanvasCommand;

public record DrawTextBlobCanvasCommand(SKTextBlob? TextBlob, float X, float Y, SKPaint? Paint) : CanvasCommand;

public record DrawTextCanvasCommand(string Text, float X, float Y, SKPaint? Paint) : CanvasCommand;

public record DrawTextOnPathCanvasCommand(string Text, SKPath? Path, float HOffset, float VOffset, SKPaint? Paint) : CanvasCommand;

public record RestoreCanvasCommand(int Count) : CanvasCommand;

public record SaveCanvasCommand(int Count) : CanvasCommand;

public record SaveLayerCanvasCommand(int Count, SKPaint? Paint = null) : CanvasCommand;

public record SetMatrixCanvasCommand(SKMatrix Matrix) : CanvasCommand;

public class SKCanvas
{
    private int _saveCount;
    private readonly Stack<SKMatrix> _totalMatrices = new();

    public IList<CanvasCommand>? Commands { get; }

    public SKMatrix TotalMatrix { get; private set; }

    internal SKCanvas(IList<CanvasCommand> commands, SKMatrix totalMatrix)
    {
        Commands = commands;
        TotalMatrix = totalMatrix;
    }

    public void ClipPath(ClipPath clipPath, SKClipOperation operation = SKClipOperation.Intersect, bool antialias = false)
    {
        Commands?.Add(new ClipPathCanvasCommand(clipPath, operation, antialias));
    }

    public void ClipRect(SKRect rect, SKClipOperation operation = SKClipOperation.Intersect, bool antialias = false)
    {
        Commands?.Add(new ClipRectCanvasCommand(rect, operation, antialias));
    }

    public void DrawImage(SKImage image, SKRect source, SKRect dest, SKPaint? paint = null)
    {
        Commands?.Add(new DrawImageCanvasCommand(image, source, dest, paint));
    }

    public void DrawPath(SKPath path, SKPaint paint)
    {
        Commands?.Add(new DrawPathCanvasCommand(path, paint));
    }

    public void DrawText(SKTextBlob textBlob, float x, float y, SKPaint paint)
    {
        Commands?.Add(new DrawTextBlobCanvasCommand(textBlob, x, y, paint));
    }

    public void DrawText(string text, float x, float y, SKPaint paint)
    {
        Commands?.Add(new DrawTextCanvasCommand(text, x, y, paint));
    }

    public void DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKPaint paint)
    {
        Commands?.Add(new DrawTextOnPathCanvasCommand(text, path, hOffset, vOffset, paint));
    }

    public void SetMatrix(SKMatrix matrix)
    {
        TotalMatrix = matrix;
        Commands?.Add(new SetMatrixCanvasCommand(matrix));
    }

    public int Save()
    {
        _totalMatrices.Push(TotalMatrix);
        Commands?.Add(new SaveCanvasCommand(_saveCount));
        _saveCount++;
        return _saveCount;
    }

    public int SaveLayer(SKPaint paint)
    {
        _totalMatrices.Push(TotalMatrix);
        Commands?.Add(new SaveLayerCanvasCommand(_saveCount, paint));
        _saveCount++;
        return _saveCount;
    }

    public void Restore()
    {
        if (_totalMatrices.Count == 0)
        {
            Debug.WriteLine($"Invalid Save and Restore balance.");
        }
        else
        {
            TotalMatrix = _totalMatrices.Pop();
            _saveCount--;
        }

        Commands?.Add(new RestoreCanvasCommand(_saveCount));
    }
}
