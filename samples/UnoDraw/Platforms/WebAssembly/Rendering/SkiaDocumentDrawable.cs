using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia;
using Shim = ShimSkiaSharp;

namespace UnoDraw.Rendering;

/// <summary>
/// Records the public Svg.Skia model painter unchanged, substituting live text
/// drawables for its text leaves. Shapes, layers, clips, shaders and their order
/// remain with the existing painter. No SVG parsing or text shaping is duplicated.
/// </summary>
internal sealed class SkiaDocumentDrawable : IDisposable
{
    private readonly List<SkiaTextRasterDrawable> _text = [];
    private readonly SkiaTextRasterFrame _frame = new();
    private readonly SkiaModel _converter;
    private readonly List<ReplayStep>? _directReplay;
    private SKDrawable? _drawable;

    internal SkiaDocumentDrawable(SkiaModel converter, Shim.SKPicture model, bool directReplay = false)
    {
        _converter = converter;
        if (directReplay)
            _directReplay = [];
        var validator = new ModelValidator(converter);
        validator.Validate(model);
        var bounds = converter.ToSKRect(model.CullRect);
        try
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(bounds);
            Replay(model);
            // EndRecording() snapshots drawables at recording scale. Retaining
            // the drawable instead defers text rasterization to the live device CTM.
            _drawable = recorder.EndRecordingAsDrawable();

            void Replay(Shim.SKPicture current)
            {
                if (current.Commands is not { } commands)
                    return;
                foreach (var command in commands)
                {
                    if (command is Shim.DrawPictureCanvasCommand { Picture: { } nested })
                    {
                        Replay(nested);
                    }
                    else if (IsText(command))
                    {
                        using var textRecorder = new SKPictureRecorder();
                        // Record in the original model coordinate space, so its
                        // cull rectangle remains meaningful even for transformed
                        // text whose local coordinates lie far outside that model.
                        var textCanvas = textRecorder.BeginRecording(bounds, useRTree: true);
                        var matrix = canvas.TotalMatrix;
                        textCanvas.Concat(in matrix);
                        converter.Draw(command, textCanvas);
                        var picture = textRecorder.EndRecording();
                        SkiaTextRasterDrawable text;
                        try
                        {
                            text = new SkiaTextRasterDrawable(picture, bounds, _frame);
                        }
                        catch
                        {
                            picture.Dispose();
                            throw;
                        }
                        _text.Add(text);
                        _directReplay?.Add(new ReplayStep(null, text));
                        var count = canvas.Save();
                        try
                        {
                            canvas.ResetMatrix();
                            canvas.DrawDrawable(text, 0, 0);
                        }
                        finally
                        {
                            canvas.RestoreToCount(count);
                        }
                    }
                    else
                    {
                        _directReplay?.Add(new ReplayStep(command, null));
                        converter.Draw(command, canvas);
                    }
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal int TextLeafCount => _text.Count;
    internal long RetainedTextBytes => _frame.RetainedBytes;
    internal int TextRasterizationCount
    {
        get
        {
            var count = 0;
            foreach (var text in _text)
                count += text.RasterizationCount;
            return count;
        }
    }

    internal void Draw(SKCanvas canvas)
    {
        var drawable = _drawable ?? throw new ObjectDisposedException(nameof(SkiaDocumentDrawable));
        _frame.Failure = null;
        if (_directReplay is { } commands)
            DrawCommands(canvas, commands);
        else
            canvas.DrawDrawable(drawable, 0, 0);
        if (_frame.Failure is { } error)
            throw new SkiaDocumentRasterizationException(error.Message, error);
    }

    private void DrawCommands(SKCanvas canvas, List<ReplayStep> commands)
    {
        // Skia d1717dc Graphite Device.h explicitly implements drawDrawable as
        // a no-op. Replay the already validated model commands with their existing
        // converter, and invoke live text leaves directly through SKDrawable.Draw.
        // The recorded Ganesh path remains unchanged.
        var entryMatrix = canvas.TotalMatrix;
        var saved = canvas.SaveCount;
        canvas.Save();
        try
        {
            foreach (var step in commands)
            {
                if (step.Text is { } text)
                {
                    var count = canvas.SaveCount;
                    canvas.Save();
                    try
                    {
                        // Each text picture already includes its model-space CTM.
                        // Keep the current clip and layer while restoring only the
                        // outer device/viewport/viewer matrix for the live tile.
                        canvas.SetMatrix(in entryMatrix);
                        text.Draw(canvas, 0, 0);
                    }
                    finally
                    {
                        canvas.RestoreToCount(count);
                    }
                }
                else
                {
                    _converter.Draw(step.Command!, canvas);
                }
            }
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    private readonly record struct ReplayStep(Shim.CanvasCommand? Command, SkiaTextRasterDrawable? Text);

    public void Dispose()
    {
        _drawable?.Dispose();
        _drawable = null;
        foreach (var text in _text)
            text.Dispose();
        _text.Clear();
        _directReplay?.Clear();
    }

    private static bool IsText(Shim.CanvasCommand command) => command is
        Shim.DrawTextCanvasCommand or Shim.DrawTextBlobCanvasCommand or
        Shim.DrawPositionedTextRunCanvasCommand or Shim.DrawTextOnPathCanvasCommand;

    /// <summary>
    /// Text with destination-dependent blending cannot be replaced by a transparent
    /// rectangle. Text embedded in native shader/filter pictures cannot retain a
    /// live drawable either. Reject such documents before any alternate recording.
    /// </summary>
    private sealed class ModelValidator(SkiaModel converter)
    {
        private readonly HashSet<Shim.SKPicture> _path = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> _effects = new(ReferenceEqualityComparer.Instance);

        internal void Validate(Shim.SKPicture picture, bool embedded = false)
        {
            if (_path.Count >= 128 || !_path.Add(picture))
                throw new SkiaDocumentRasterizationException("the model contains cyclic or excessively nested pictures");
            try
            {
                if (picture.Commands is not { } commands)
                    return;
                foreach (var command in commands)
                {
                    if (IsText(command) && embedded)
                        throw new SkiaDocumentRasterizationException("text is embedded in a shader or filter picture");
                    switch (command)
                    {
                        case Shim.DrawPictureCanvasCommand { Picture: { } nested }:
                            Validate(nested, embedded);
                            break;
                        case Shim.SetMatrixCanvasCommand matrix:
                            if (!SkiaTextRasterDrawable.IsFiniteAffine(converter.ToSKMatrix(matrix.DeltaMatrix)))
                                throw new SkiaDocumentRasterizationException("the model contains a non-finite or perspective transform");
                            break;
                        case Shim.DrawTextCanvasCommand text:
                            Paint(text.Paint, text: true);
                            break;
                        case Shim.DrawTextBlobCanvasCommand text:
                            Paint(text.Paint, text: true);
                            break;
                        case Shim.DrawPositionedTextRunCanvasCommand text:
                            Paint(text.Paint, text: true);
                            break;
                        case Shim.DrawTextOnPathCanvasCommand text:
                            Paint(text.Paint, text: true);
                            break;
                        case Shim.DrawPathCanvasCommand path:
                            Paint(path.Paint);
                            break;
                        case Shim.DrawImageCanvasCommand image:
                            Paint(image.Paint);
                            break;
                        case Shim.SaveLayerCanvasCommand layer:
                            Paint(layer.Paint);
                            break;
                        case Shim.ClipPathCanvasCommand or Shim.ClipRectCanvasCommand or
                            Shim.SaveCanvasCommand or Shim.RestoreCanvasCommand or Shim.DrawPictureCanvasCommand:
                            break;
                        default:
                            throw new SkiaDocumentRasterizationException("the model contains an unsupported canvas command");
                    }
                }
            }
            finally
            {
                _path.Remove(picture);
            }
        }

        private void Paint(Shim.SKPaint? paint, bool text = false)
        {
            if (paint is null)
                return;
            EnterEffect(paint);
            try
            {
                if (paint.IsStrokeNonScaling)
                    throw new SkiaDocumentRasterizationException("the document uses device-dependent non-scaling strokes");
                if (text && paint.BlendMode != Shim.SKBlendMode.SrcOver)
                    throw new SkiaDocumentRasterizationException("text uses destination-dependent blending");
                Shader(paint.Shader);
                Filter(paint.ImageFilter);
            }
            finally
            {
                _effects.Remove(paint);
            }
        }

        private void Shader(Shim.SKShader? shader)
        {
            if (shader is null)
                return;
            EnterEffect(shader);
            try
            {
                if (shader is Shim.PictureShader { Src: { } picture })
                    Validate(picture, embedded: true);
            }
            finally
            {
                _effects.Remove(shader);
            }
        }

        private void EnterEffect(object effect)
        {
            if (_effects.Count >= 128 || !_effects.Add(effect))
                throw new SkiaDocumentRasterizationException("the document contains cyclic or excessively nested paint effects");
        }

        private void Filter(Shim.SKImageFilter? filter)
        {
            if (filter is null)
                return;
            EnterEffect(filter);
            try
            {
                switch (filter)
                {
                    case Shim.ImageImageFilter:
                        break;
                    case Shim.ArithmeticImageFilter f:
                        Filter(f.Background);
                        Filter(f.Foreground);
                        break;
                    case Shim.BlendModeImageFilter f:
                        Filter(f.Background);
                        Filter(f.Foreground);
                        break;
                    case Shim.BlurImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.ColorFilterImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.DilateImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.DisplacementMapEffectImageFilter f:
                        Filter(f.Displacement);
                        Filter(f.Input);
                        break;
                    case Shim.DistantLitDiffuseImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.DistantLitSpecularImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.ErodeImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.MatrixConvolutionImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.MergeImageFilter f:
                        if (f.Filters is { } filters)
                            foreach (var input in filters)
                                Filter(input);
                        break;
                    case Shim.OffsetImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.PaintImageFilter f:
                        Paint(f.Paint);
                        break;
                    case Shim.ShaderImageFilter f:
                        Shader(f.Shader);
                        break;
                    case Shim.PictureImageFilter { Picture: { } picture }:
                        Validate(picture, embedded: true);
                        break;
                    case Shim.PictureImageFilter:
                        break;
                    case Shim.PointLitDiffuseImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.PointLitSpecularImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.SpotLitDiffuseImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.SpotLitSpecularImageFilter f:
                        Filter(f.Input);
                        break;
                    case Shim.TileImageFilter f:
                        Filter(f.Input);
                        break;
                    default:
                        throw new SkiaDocumentRasterizationException("the document contains an unsupported image filter");
                }
            }
            finally
            {
                _effects.Remove(filter);
            }
        }
    }
}
