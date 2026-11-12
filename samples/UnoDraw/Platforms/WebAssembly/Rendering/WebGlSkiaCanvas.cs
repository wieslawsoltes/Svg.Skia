using System.Runtime.InteropServices.JavaScript;
using SkiaSharp;
using Svg.Editor.Skia.Uno;
using Svg.Skia;
using Uno.UI.Composition.WebGpu;
using Uno.WinUI.Graphics3DWebGpu;

namespace UnoDraw.Rendering;

/// <summary>
/// An app-owned Ganesh/WebGL2 document surface. Browser canvas transfer preserves
/// its premultiplied RGBA pixels in the existing WebGPU composition target. Some
/// browsers (including Chromium 143 on Linux) implement this transfer via CPU
/// readback; the document itself is still rasterized by Skia's GPU backend.
/// </summary>
internal sealed unsafe class WebGlSkiaCanvas : IDisposable
{
    private JSObject? _canvasState;
    private string _selector = string.Empty;
    private int _context;
    private GRGlInterface? _glInterface;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private int _width;
    private int _height;
    private int _stencil;
    private int _samples;
    private int _maximumWidth;
    private int _maximumHeight;
    private readonly Action<SKSvg, SKCanvas> _drawDocument;
    private readonly Func<SKCanvas, SkiaModel, ShimSkiaSharp.SKPicture, bool> _tryDrawModel;
    private SkiaDocumentDrawable? _document;
    private SkiaModel? _documentConverter;
    private ShimSkiaSharp.SKPicture? _documentModel;
    private SKPicture? _documentPicture;
    private SKPicture? _activePicture;
    private string? _documentFailure;
    private string? _reportedDocumentFailure;
    private bool _documentFailureCanRetry;
    private CanvasPixelSize _failedPixels;
    private SKMatrix _failedViewport;
    private ShimSkiaSharp.SKMatrix _failedViewerTransform;

    internal WebGlSkiaCanvas(Action<string> unavailable)
    {
        _drawDocument = DrawDocument;
        _tryDrawModel = TryDrawModel;
        var previous = WebGlSkiaCanvasInterop.GetCurrentContext();
        try
        {
            _canvasState = WebGlSkiaCanvasInterop.CreateCanvas(unavailable,
                () => WebGpuSkiaCanvasBridge.ReportWebGlInitialization());
            _selector = _canvasState.GetPropertyAsString("selector")
                ?? throw new InvalidOperationException("The WebGL canvas has no selector.");
            _context = WebGlSkiaCanvasInterop.CreateContext(_selector);
            if (_context <= 0)
                throw new NotSupportedException("An independent WebGL2 context could not be created.");
            MakeCurrent();
            WebGlSkiaCanvasInterop.PrepareCanvas(_canvasState);
            if (WebGlSkiaCanvasInterop.GetFramebuffer(out _stencil, out _samples,
                out _maximumWidth, out _maximumHeight) != 0 || _stencil < 8 || _samples != 0)
                throw new NotSupportedException("The WebGL2 canvas has incompatible framebuffer attachments.");

            _glInterface = GRGlInterface.CreateWebGl(WebGlSkiaCanvasInterop.GetProcAddress)
                ?? throw new NotSupportedException("Skia could not load the WebGL2 interface.");
            _grContext = GRContext.CreateGl(_glInterface)
                ?? throw new NotSupportedException("Skia could not create the WebGL2 rendering context.");
            // Match Uno's browser Skia cache budget; canvas-sized temporary
            // resources must survive between document paints.
            _grContext.SetResourceCacheLimit(256L * 1024 * 1024);
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            RestoreCurrent(previous);
        }
    }

    internal bool Render(WebGpuCanvasContext target, CanvasPixelSize pixels, ISkiaCanvasRenderer renderer)
    {
        if (renderer is SvgEditorCanvas editorSource)
        {
            if (editorSource.Controller is { } controller)
            {
                var source = controller.Renderer;
                // Release a previous document even when its replacement requires
                // the CPU painter and therefore never reaches TryDrawModel.
                if (_documentConverter is not null &&
                    (!ReferenceEquals(_documentConverter, source.SkiaModel) ||
                     !ReferenceEquals(_documentModel, source.Model) || !ReferenceEquals(_documentPicture, source.Picture)))
                    ReleaseDocument();
                if (RequiresOriginalPainter(source))
                    return UseCpuDocument("the document requires its original dynamic or wireframe painter");
                if (_documentFailureCanRetry &&
                    (_failedPixels != pixels || _failedViewport != editorSource.Viewport.Matrix ||
                     !_failedViewerTransform.Equals(source.ViewerTransform)))
                    ReleaseDocument();
                if (_documentFailure is { } failure)
                    return UseCpuDocument(failure);
            }
            else if (_documentConverter is not null)
            {
                ReleaseDocument();
            }
        }

        var previous = WebGlSkiaCanvasInterop.GetCurrentContext();
        try
        {
            MakeCurrent();
            EnsureSurface(pixels.Width, pixels.Height);
            var canvas = _surface!.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            try
            {
                canvas.Scale((float)pixels.Scale);
                canvas.ClipRect(new SKRect(0, 0, (float)pixels.LogicalSize.Width, (float)pixels.LogicalSize.Height));
                if (renderer is SvgEditorCanvas editor)
                    editor.RenderWithDocument(canvas, pixels.LogicalSize, _drawDocument);
                else
                    renderer.Render(canvas, pixels.LogicalSize);
            }
            finally
            {
                canvas.Restore();
            }

            _grContext!.Flush(submit: true, synchronous: false);
            var error = WebGlSkiaCanvasInterop.GetError();
            if (error != 0 || WebGlSkiaCanvasInterop.IsContextLost(_context) != 0)
                throw new InvalidOperationException($"The WebGL2 document render failed (GL 0x{error:X}).");

            // These proxies belong to Uno/emdawn and must not be disposed here.
            // Resolve the live target every paint: its native handle may be reused
            // after a resize, and caching by that number could retain a stale texture.
            var device = WebGpuJsInterop.GetJsObject(unchecked((int)(nint)target.Device));
            var queue = WebGpuJsInterop.GetJsObject(unchecked((int)(nint)target.Queue));
            var texture = WebGpuJsInterop.GetJsObject(unchecked((int)(nint)target.Target));
            WebGlSkiaCanvasInterop.CopyCanvas(_canvasState!, device, queue, texture, _width, _height);
            return true;
        }
        catch (SkiaDocumentRasterizationException error)
        {
            // Unsupported document semantics use the original complete CPU frame;
            // the healthy GL context remains available for later documents.
            if (_documentFailureCanRetry && renderer is SvgEditorCanvas { Controller: { } failedController } failedEditor)
            {
                _failedPixels = pixels;
                _failedViewport = failedEditor.Viewport.Matrix;
                _failedViewerTransform = failedController.Renderer.ViewerTransform;
            }
            return UseCpuDocument(error.Message);
        }
        finally
        {
            RestoreCurrent(previous);
        }
    }

    private bool UseCpuDocument(string reason)
    {
        if (_reportedDocumentFailure != reason)
        {
            _reportedDocumentFailure = reason;
            Console.WriteLine($"UnoDraw WebGL canvas fallback: {reason}.");
        }
        return false;
    }

    private static bool RequiresOriginalPainter(SKSvg source) =>
        source.Wireframe || source.HasAnimations || source.UsesAnimationLayerCaching || source.HasTextSelection;

    private void DrawDocument(SKSvg source, SKCanvas canvas)
    {
        if (RequiresOriginalPainter(source))
            throw new SkiaDocumentRasterizationException("the document requires its original dynamic or wireframe painter");

        var previous = _activePicture;
        _activePicture = source.Picture;
        try
        {
            source.Draw(canvas, _tryDrawModel);
        }
        finally
        {
            _activePicture = previous;
        }
    }

    private bool TryDrawModel(SKCanvas canvas, SkiaModel converter, ShimSkiaSharp.SKPicture model)
    {
        // The native picture is the existing renderer's content-generation key.
        // Camera changes leave it stable; document rebuilds replace it even when
        // callers retain and edit the public model object.
        if (!ReferenceEquals(_documentConverter, converter) || !ReferenceEquals(_documentModel, model) ||
            !ReferenceEquals(_documentPicture, _activePicture))
        {
            ReleaseDocument();
            _documentConverter = converter;
            _documentModel = model;
            _documentPicture = _activePicture;
            try
            {
                _document = new SkiaDocumentDrawable(converter, model);
            }
            catch (SkiaDocumentRasterizationException error)
            {
                _documentFailure = error.Message;
            }
        }
        if (_documentFailure is { } failure)
            throw new SkiaDocumentRasterizationException(failure);
        try
        {
            _document!.Draw(canvas);
        }
        catch (SkiaDocumentRasterizationException error)
        {
            _documentFailure = error.Message;
            _documentFailureCanRetry = true;
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
        _documentConverter = null;
        _documentModel = null;
        _documentPicture = null;
        _documentFailure = null;
        _documentFailureCanRetry = false;
        _reportedDocumentFailure = null;
    }

    private void EnsureSurface(int width, int height)
    {
        if (_surface is not null && _width == width && _height == height)
            return;
        if (width <= 0 || height <= 0 || width > _maximumWidth || height > _maximumHeight)
            throw new NotSupportedException("The document exceeds the WebGL2 drawing-buffer limits.");

        _surface?.Dispose();
        _surface = null;
        _renderTarget?.Dispose();
        _renderTarget = null;
        _width = _height = 0;
        if (WebGlSkiaCanvasInterop.Resize(_selector, width, height) != 0 ||
            WebGlSkiaCanvasInterop.GetBufferSize(_context, out var actualWidth, out var actualHeight) != 0 ||
            actualWidth != width || actualHeight != height)
            throw new NotSupportedException("The browser could not allocate the full-resolution WebGL2 drawing buffer.");

        _grContext!.ResetContext();
        _renderTarget = new GRBackendRenderTarget(width, height, _samples, _stencil,
            new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat()));
        // BottomLeft is Skia's normal browser framebuffer origin. The browser's
        // canvas image is already top-left oriented; copyExternalImage needs no flip.
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new NotSupportedException("Skia could not wrap the WebGL2 drawing buffer.");
        _width = width;
        _height = height;
    }

    private void MakeCurrent()
    {
        if (_context <= 0 || WebGlSkiaCanvasInterop.IsContextLost(_context) != 0 ||
            WebGlSkiaCanvasInterop.MakeCurrent(_context) != 0)
            throw new InvalidOperationException("The WebGL2 document context is unavailable.");
    }

    private static void RestoreCurrent(int previous)
    {
        // A previously lost context cannot be restored. Keep Emscripten's current
        // context empty in that case; never leave this private context active.
        if (WebGlSkiaCanvasInterop.MakeCurrent(previous) != 0)
            WebGlSkiaCanvasInterop.MakeCurrent(0);
    }

    public void Dispose()
    {
        var context = _context;
        var previous = WebGlSkiaCanvasInterop.GetCurrentContext();
        try
        {
            var usable = context > 0 && WebGlSkiaCanvasInterop.IsContextLost(context) == 0 &&
                WebGlSkiaCanvasInterop.MakeCurrent(context) == 0;
            if (!usable)
                _grContext?.AbandonContext(releaseResources: false);
            ReleaseDocument();
            _activePicture = null;
            _surface?.Dispose();
            _surface = null;
            _renderTarget?.Dispose();
            _renderTarget = null;
            _grContext?.Dispose();
            _grContext = null;
            _glInterface?.Dispose();
            _glInterface = null;
            if (context > 0)
                WebGlSkiaCanvasInterop.DestroyContext(context);
            _context = 0;
            _width = _height = 0;
        }
        finally
        {
            RestoreCurrent(previous == context ? 0 : previous);
            if (_canvasState is { } state)
            {
                _canvasState = null;
                try
                {
                    WebGlSkiaCanvasInterop.DisposeCanvas(state);
                }
                finally
                {
                    state.Dispose(); // This app-owned proxy is not a borrowed GPU object.
                }
            }
        }
    }
}
