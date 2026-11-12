using System;
using SkiaSharp;
using Svg.Skia;

namespace UnoDraw.Rendering;

/// <summary>
/// Retains the original public model painter with native-resolution CPU text
/// leaves. Unsupported document semantics use the complete original CPU frame.
/// The cache owns no GPU context, recorder, texture or browser object.
/// </summary>
internal sealed class SkiaGpuDocumentPainter : IDisposable
{
    private readonly Func<SKCanvas, SkiaModel, ShimSkiaSharp.SKPicture, bool> _tryDrawModel;
    private readonly bool _directReplay;
    private SkiaDocumentDrawable? _document;
    private SkiaModel? _converter;
    private ShimSkiaSharp.SKPicture? _model;
    private SKPicture? _picture;
    private SKPicture? _activePicture;
    private string? _failure;
    private bool _failureCanRetry;
    private View _view;
    private View _failedView;
    private int _drawDepth;
    private bool _disposed;

    internal SkiaGpuDocumentPainter(bool directReplay = false)
    {
        _directReplay = directReplay;
        _tryDrawModel = TryDrawModel;
        DrawDocument = Draw;
    }

    internal Action<SKSvg, SKCanvas> DrawDocument { get; }

    internal bool Prepare(SKSvg? source, int width, int height, double scale,
        double logicalWidth, double logicalHeight, SKMatrix viewport, out string? reason)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_converter is not null && (source is null ||
            !ReferenceEquals(_converter, source.SkiaModel) || !ReferenceEquals(_model, source.Model) ||
            !ReferenceEquals(_picture, source.Picture)))
            ReleaseDocument();
        _view = new View(width, height, scale, logicalWidth, logicalHeight,
            viewport, source?.ViewerTransform ?? default);
        if (source is not null && RequiresOriginalPainter(source))
        {
            reason = "the document requires its original dynamic or wireframe painter";
            return false;
        }
        if (_failureCanRetry && _failedView != _view)
            ReleaseDocument();
        reason = _failure;
        return reason is null;
    }

    private static bool RequiresOriginalPainter(SKSvg source) =>
        source.Wireframe || source.HasAnimations || source.UsesAnimationLayerCaching || source.HasTextSelection;

    private void Draw(SKSvg source, SKCanvas canvas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (RequiresOriginalPainter(source))
            throw new SkiaDocumentRasterizationException("the document requires its original dynamic or wireframe painter");

        var previous = _activePicture;
        _activePicture = source.Picture;
        _drawDepth++;
        try
        {
            source.Draw(canvas, _tryDrawModel);
        }
        finally
        {
            _activePicture = previous;
            _drawDepth--;
            if (_disposed && _drawDepth == 0)
                ReleaseDocument();
        }
    }

    private bool TryDrawModel(SKCanvas canvas, SkiaModel converter, ShimSkiaSharp.SKPicture model)
    {
        if (!ReferenceEquals(_converter, converter) || !ReferenceEquals(_model, model) ||
            !ReferenceEquals(_picture, _activePicture))
        {
            ReleaseDocument();
            _converter = converter;
            _model = model;
            _picture = _activePicture;
            try
            {
                _document = new SkiaDocumentDrawable(converter, model, _directReplay);
            }
            catch (SkiaDocumentRasterizationException error)
            {
                _failure = error.Message;
            }
        }
        if (_failure is { } failure)
            throw new SkiaDocumentRasterizationException(failure);
        try
        {
            _document!.Draw(canvas);
        }
        catch (SkiaDocumentRasterizationException error)
        {
            _failure = error.Message;
            _failureCanRetry = true;
            _failedView = _view;
            _document?.Dispose();
            _document = null;
            throw;
        }
        return true;
    }

    private void ReleaseDocument()
    {
        _document?.Dispose();
        _document = null;
        _converter = null;
        _model = null;
        _picture = null;
        _failure = null;
        _failureCanRetry = false;
    }

    public void Dispose()
    {
        _disposed = true;
        // SKSvg.OnDraw can synchronously unload its owner. The native drawable
        // callback must return before its CPU pictures and text leaves are freed.
        if (_drawDepth == 0)
            ReleaseDocument();
    }

    private readonly record struct View(int Width, int Height, double Scale,
        double LogicalWidth, double LogicalHeight, SKMatrix Viewport, ShimSkiaSharp.SKMatrix Viewer);
}
