using System.Runtime.InteropServices.JavaScript;
using Silk.NET.WebGPU;
using SkiaSharp;
using Uno.UI.Composition.WebGpu;
using Uno.WinUI.Graphics3DWebGpu;

namespace UnoDraw.Rendering;

/// <summary>
/// Draws into Uno's own WebGPU target on its existing queue. No browser canvas,
/// document readback, intermediate pixel buffer or full-frame upload is involved.
/// </summary>
internal sealed unsafe class WebGpuGraphiteCanvas(Action<string> unavailable) : IDisposable
{
    private JSObject? _state;
    private WebGpuGraphiteContext.Lease? _lease;
    private SKGraphiteBackendTexture? _texture;
    private SKSurface? _surface;
    private nint _target;
    private nint _device;
    private int _width;
    private int _height;
    private bool _rendering;
    private bool _disposed;

    internal bool Render(WebGpuCanvasContext target, CanvasPixelSize pixels, Action<SKCanvas> draw)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (target.TargetFormat != TextureFormat.Rgba8Unorm)
            throw new NotSupportedException("Graphite requires an RGBA8 WebGPU target.");
        if (_rendering)
            throw new InvalidOperationException("The Graphite document paint cannot be reentered.");
        _rendering = true;
        var opened = false;
        var submitted = false;
        try
        {
            if (_state is null)
            {
                // These two proxies are borrowed from Uno. The helper state keeps
                // the real JS objects alive without taking ownership of proxies.
                var device = WebGpuJsInterop.GetJsObject(unchecked((int)(nint)target.Device));
                var queue = WebGpuJsInterop.GetJsObject(unchecked((int)(nint)target.Queue));
                _state = WebGpuGraphiteInterop.CreateState(device, queue, OnAsyncUnavailable,
                    WebGpuSkiaCanvasBridge.ReportGraphiteInitialization);
                _device = (nint)target.Device;
            }
            if (_device != (nint)target.Device)
                throw new InvalidOperationException("The Graphite document device changed without retirement.");
            WebGpuGraphiteInterop.BeginFrame(_state);
            opened = true;
            _lease ??= WebGpuGraphiteContext.Acquire(target);
            var context = _lease.Context;
            context.CheckAsyncWorkCompletion();
            EnsureSurface(target);
            var canvas = _surface!.Canvas;
            canvas.RestoreToCount(1);
            canvas.ResetMatrix();
            canvas.Clear(SKColors.Transparent);
            var saved = canvas.SaveCount;
            canvas.Save();
            try
            {
                canvas.Scale((float)pixels.Scale);
                canvas.ClipRect(new SKRect(0, 0, (float)pixels.LogicalSize.Width, (float)pixels.LogicalSize.Height));
                draw(canvas);
            }
            finally
            {
                canvas.RestoreToCount(saved);
            }
            // An OnDraw callback may synchronously unload this canvas. Do not
            // submit after a retirement request or capture its queue barrier early.
            if (_disposed)
                return false;
            _lease.ThrowIfImageUploadFailed();
            using var recording = _lease.Recorder.Snap()
                ?? throw new InvalidOperationException("Graphite produced no document recording.");
            _lease.ThrowIfImageUploadFailed();
            try
            {
                if (context.InsertRecording(recording) != SKGraphiteInsertStatus.Success)
                    throw new InvalidOperationException("Graphite rejected the document recording.");
                _lease.ThrowIfImageUploadFailed();
                if (!context.Submit())
                    throw new InvalidOperationException("Graphite could not submit the document recording.");
                _lease.ThrowIfImageUploadFailed();
            }
            catch
            {
                _lease.RejectSubmission();
                throw;
            }
            submitted = true;
            return true;
        }
        finally
        {
            try
            {
                if (!submitted)
                    _lease?.DiscardRecording();
            }
            finally
            {
                try
                {
                    if (opened)
                        WebGpuGraphiteInterop.EndFrame(_state!, submitted);
                }
                finally
                {
                    _rendering = false;
                    if (_disposed)
                        RetireResources();
                }
            }
        }
    }

    private void OnAsyncUnavailable(string reason)
    {
        _lease?.RejectSubmission();
        unavailable(reason);
    }

    private void EnsureSurface(WebGpuCanvasContext target)
    {
        if (_surface is not null && _target == (nint)target.Target &&
            _width == target.PixelWidth && _height == target.PixelHeight)
            return;
        _surface?.Dispose();
        _surface = null;
        _texture?.Dispose();
        _texture = null;
        _target = 0;
        _texture = SKGraphiteBackendTexture.CreateDawn((nint)target.Target)
            ?? throw new NotSupportedException("Graphite could not wrap Uno's WebGPU target.");
        _surface = SKSurface.Create(_lease!.Recorder, _texture, SKColorType.Rgba8888)
            ?? throw new NotSupportedException("Graphite could not create a surface on Uno's WebGPU target.");
        // The surface retains the native texture. Its handle cannot be recycled
        // while this wrapper is cached, even if Uno replaces its own target.
        _target = (nint)target.Target;
        _width = target.PixelWidth;
        _height = target.PixelHeight;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (!_rendering)
            RetireResources();
    }

    private void RetireResources()
    {
        var state = _state;
        _state = null;
        var lease = _lease;
        _lease = null;
        var surface = _surface;
        _surface = null;
        var texture = _texture;
        _texture = null;
        _target = 0;
        if (state is null)
            return;
        try
        {
            WebGpuGraphiteInterop.Retire(state);
        }
        catch (Exception error)
        {
            Console.WriteLine($"UnoDraw Graphite canvas fallback cleanup: {error.Message}.");
        }
        if (lease is not null)
            lease.Retire(state, surface, texture);
        else
        {
            // No native context was acquired, so no GPU work can be pending.
            surface?.Dispose();
            texture?.Dispose();
            WebGpuGraphiteInterop.ReleaseState(state);
        }
    }
}
