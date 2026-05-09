// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Xml;
using ShimSkiaSharp;
using Svg;
using Svg.JavaScript;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public partial class SKSvg : IDisposable
{
    private enum SourceFormat
    {
        Svg,
        VectorDrawable
    }

    public static bool CacheOriginalStream { get; set; }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromStream(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(stream, parameters);
        return skSvg;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromStream(System.IO.Stream stream) => CreateFromStream(stream, null);

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromFile(string path, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(path, parameters);
        return skSvg;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromFile(string path) => CreateFromFile(path, null);

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromVectorDrawable(string path, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(path, parameters);
        return skSvg;
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromVectorDrawable(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(stream, parameters);
        return skSvg;
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromVectorDrawable(XmlReader reader)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(reader);
        return skSvg;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromXmlReader(XmlReader reader)
    {
        var skSvg = new SKSvg();
        skSvg.Load(reader);
        return skSvg;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromSvg(string svg)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvg(svg);
        return skSvg;
    }

    [RequiresUnreferencedCode("Rendering from an SVG document may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromSvgDocument(SvgDocument svgDocument)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvgDocument(svgDocument);
        return skSvg;
    }

    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var picture = SvgSceneRuntime.CreateModel(
            svgFragment,
            assetLoader,
            DrawAttributes.None,
            GetStandaloneViewport(skiaModel.Settings));
        return skiaModel.ToSKPicture(picture);
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var picture = SvgSceneRuntime.CreateModel(
            svgFragment,
            assetLoader,
            DrawAttributes.None,
            GetStandaloneViewport(skiaModel.Settings));
        if (picture is { })
        {
            skiaModel.Draw(picture, skCanvas);
        }
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, string path, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var svgDocument = SvgService.Open(path);
        if (svgDocument is { })
        {
            Draw(skCanvas, svgDocument, skiaModel, assetLoader);
        }
    }

    private SvgParameters? _originalParameters;
    private string? _originalPath;
    private System.IO.Stream? _originalStream;
    private Uri? _originalBaseUri;
    private SourceFormat _originalSourceFormat;
    private int _activeDraws;
    private SvgDocument? _animatedDocument;
    private SvgAnimationFrameState? _lastRenderedAnimationFrameState;
    private SvgAnimationFrameState? _pendingAnimationFrameState;
    private TimeSpan _lastRenderedAnimationTime = TimeSpan.MinValue;
    private TimeSpan _animationMinimumRenderInterval;
    private SvgJavaScriptRuntime? _javaScriptRuntime;
    private bool _isDispatchingAnimationTimelineCallbacks;
    private bool _animationTimelineChangedDuringDispatch;
    private TimeSpan? _animationTimelineCallbackTime;

    public object Sync { get; } = new();

    public SKSvgSettings Settings { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    public SKPicture? Model { get; private set; }

    public SvgDocument? SourceDocument { get; private set; }

    public SvgAnimationController? AnimationController { get; private set; }

    public bool HasAnimations => AnimationController?.HasAnimations == true;

    public TimeSpan AnimationTime => AnimationController?.Clock.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan AnimationMinimumRenderInterval
    {
        get => _animationMinimumRenderInterval;
        set => _animationMinimumRenderInterval = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    public bool HasPendingAnimationFrame => _pendingAnimationFrameState is not null;

    public int LastAnimationDirtyTargetCount { get; private set; }

    private SkiaSharp.SKPicture? _picture;
    public virtual SkiaSharp.SKPicture? Picture
    {
        get
        {
            SkiaSharp.SKPicture? picture;
            SKPicture? model;
            lock (Sync)
            {
                picture = _picture;
                if (picture is { })
                {
                    return picture;
                }

                model = Model;
                if (model is null)
                {
                    return null;
                }
            }

            var newPicture = SkiaModel.ToSKPicture(model);
            if (newPicture is null)
            {
                return null;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(Model, model))
                {
                    newPicture.Dispose();
                    return _picture;
                }

                if (_picture is { } existing)
                {
                    newPicture.Dispose();
                    return existing;
                }

                _picture = newPicture;
                return newPicture;
            }
        }
        protected set => _picture = value;
    }

    public SkiaSharp.SKPicture? WireframePicture { get; protected set; }

    private bool _wireframe;
    public bool Wireframe
    {
        get => _wireframe;
        set
        {
            _wireframe = value;
            ClearWireframePicture();
        }
    }

    private DrawAttributes _ignoreAttributes;
    public DrawAttributes IgnoreAttributes
    {
        get => _ignoreAttributes;
        set => _ignoreAttributes = value;
    }

    public void ClearWireframePicture()
    {
        lock (Sync)
        {
            WaitForDrawsLocked();
            WireframePicture?.Dispose();
            WireframePicture = null;
        }
    }

    public event EventHandler<SKSvgDrawEventArgs>? OnDraw;

    public event EventHandler<SvgAnimationFrameChangedEventArgs>? AnimationInvalidated;

    protected virtual void RaiseOnDraw(SKSvgDrawEventArgs e)
    {
        OnDraw?.Invoke(this, e);
    }

    protected virtual void RaiseAnimationInvalidated(SvgAnimationFrameChangedEventArgs e)
    {
        AnimationInvalidated?.Invoke(this, e);
    }

    public SvgParameters? Parameters => _originalParameters;

    public SKSvg()
    {
        Settings = new SKSvgSettings();
        SkiaModel = new SkiaModel(Settings);
        AssetLoader = new SkiaSvgAssetLoader(SkiaModel);
    }

    /// <summary>
    /// Rebuilds the SkiaSharp picture from the current model.
    /// </summary>
    /// <returns>The rebuilt SkiaSharp picture, or null when no model exists.</returns>
    public SkiaSharp.SKPicture? RebuildFromModel()
    {
        var model = Model;
        if (model is null)
        {
            lock (Sync)
            {
                WaitForDrawsLocked();
                _picture?.Dispose();
                _picture = null;
                WireframePicture?.Dispose();
                WireframePicture = null;
            }
            return null;
        }

        var rebuilt = SkiaModel.ToSKPicture(model);
        lock (Sync)
        {
            WaitForDrawsLocked();
            if (!ReferenceEquals(Model, model))
            {
                rebuilt?.Dispose();
                return _picture;
            }

            _picture?.Dispose();
            _picture = rebuilt;
            WireframePicture?.Dispose();
            WireframePicture = null;
        }
        return rebuilt;
    }

    /// <summary>
    /// Re-renders the current <see cref="SourceDocument"/> into a fresh model and picture.
    /// Use this after mutating the source DOM rather than the compiled picture model.
    /// </summary>
    /// <returns>The refreshed picture, or <see langword="null"/> when no source document exists.</returns>
    public SkiaSharp.SKPicture? RefreshFromSourceDocument()
    {
        var sourceDocument = SourceDocument;
        if (sourceDocument is null)
        {
            return null;
        }

        InvalidateRetainedSceneGraph();
        if (AnimationController is { })
        {
            ClearAnimationRenderState();
            RefreshCurrentAnimationFrame(bypassThrottle: true);
            return Picture;
        }

        return RenderSvgDocument(sourceDocument);
    }

    /// <summary>
    /// Creates a deep clone of the current <see cref="SKSvg"/>, including model and reload data.
    /// </summary>
    /// <returns>A new <see cref="SKSvg"/> instance with independent state.</returns>
    [RequiresUnreferencedCode("Clone may recreate retained scene and animation state that use TypeDescriptor-based converters.")]
    public SKSvg Clone()
    {
        var clone = new SKSvg();

        Settings.CopyTo(clone.Settings);
        clone.Wireframe = Wireframe;
        clone.IgnoreAttributes = IgnoreAttributes;
        clone.AnimationMinimumRenderInterval = AnimationMinimumRenderInterval;

        clone._originalParameters = _originalParameters;
        clone._originalPath = _originalPath;
        clone._originalBaseUri = _originalBaseUri;
        clone._originalSourceFormat = _originalSourceFormat;

        if (_originalStream is { } originalStream)
        {
            clone._originalStream = new MemoryStream();
            var position = originalStream.Position;
            originalStream.Position = 0;
            originalStream.CopyTo(clone._originalStream);
            clone._originalStream.Position = 0;
            originalStream.Position = position;
        }

        if (Model is { } model)
        {
            clone.Model = model.DeepClone();
        }

        if (SourceDocument?.DeepCopy() is SvgDocument sourceDocumentClone)
        {
            clone.SourceDocument = sourceDocumentClone;

            clone.InitializeJavaScriptRuntime(sourceDocumentClone, executeDocumentScripts: true, dispatchLoadEvent: false);

            if (HasAnimations)
            {
                clone.ReplaceAnimationController(new SvgAnimationController(sourceDocumentClone));
                clone.SetAnimationTime(AnimationTime);
            }
            else if (clone._javaScriptRuntime?.MutationVersion > 0)
            {
                _ = clone.RenderSvgDocument(sourceDocumentClone);
            }
        }

        clone.InvalidateRetainedSceneGraph();
        return clone;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadSvgInternal(stream, parameters, null);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        return LoadSvgInternal(stream, parameters, baseUri);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(string? path, SvgParameters? parameters = null)
    {
        return LoadSvgPath(path, parameters);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(string path) => Load(path, null);

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(XmlReader reader)
    {
        return LoadSvgReader(reader);
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? LoadVectorDrawable(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadInternal(stream, parameters, null, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? LoadVectorDrawable(string? path, SvgParameters? parameters = null)
    {
        return LoadPath(path, parameters, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? LoadVectorDrawable(XmlReader reader)
    {
        return LoadReader(reader, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadInternal(
        System.IO.Stream stream,
        SvgParameters? parameters,
        Uri? baseUri,
        SourceFormat sourceFormat,
        Func<System.IO.Stream, SvgParameters?, SvgDocument?> loader)
    {
        SvgDocument? svgDocument;

        if (CacheOriginalStream)
        {
            if (_originalStream != stream)
            {
                _originalStream?.Dispose();
                _originalStream = new System.IO.MemoryStream();
                stream.CopyTo(_originalStream);
            }

            _originalPath = null;
            _originalParameters = parameters;
            _originalBaseUri = baseUri;
            _originalSourceFormat = sourceFormat;
            _originalStream.Position = 0;

            svgDocument = loader(_originalStream, parameters);
        }
        else
        {
            _originalPath = null;
            _originalParameters = parameters;
            _originalBaseUri = baseUri;
            _originalSourceFormat = sourceFormat;
            _originalStream?.Dispose();
            _originalStream = null;
            svgDocument = loader(stream, parameters);
        }

        return LoadSvgDocument(svgDocument, baseUri);
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadSvgInternal(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        SvgDocument? svgDocument;

        if (CacheOriginalStream)
        {
            if (_originalStream != stream)
            {
                _originalStream?.Dispose();
                _originalStream = new System.IO.MemoryStream();
                stream.CopyTo(_originalStream);
            }

            _originalPath = null;
            _originalParameters = parameters;
            _originalBaseUri = baseUri;
            _originalSourceFormat = SourceFormat.Svg;
            _originalStream.Position = 0;

            svgDocument = SvgService.Open(_originalStream, parameters, Settings.EnableJavaScript);
        }
        else
        {
            _originalPath = null;
            _originalParameters = parameters;
            _originalBaseUri = baseUri;
            _originalSourceFormat = SourceFormat.Svg;
            _originalStream?.Dispose();
            _originalStream = null;
            svgDocument = SvgService.Open(stream, parameters, Settings.EnableJavaScript);
        }

        return LoadSvgDocument(svgDocument, baseUri);
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadSvgPath(string? path, SvgParameters? parameters)
    {
        if (path is null)
        {
            return null;
        }

        _originalPath = path;
        _originalParameters = parameters;
        _originalBaseUri = null;
        _originalSourceFormat = SourceFormat.Svg;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(SvgService.Open(path, parameters, Settings.EnableJavaScript));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadSvgReader(XmlReader reader)
    {
        _originalPath = null;
        _originalParameters = null;
        _originalBaseUri = null;
        _originalSourceFormat = SourceFormat.Svg;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(SvgService.Open(reader, Settings.EnableJavaScript));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadPath(
        string? path,
        SvgParameters? parameters,
        SourceFormat sourceFormat,
        Func<string, SvgParameters?, SvgDocument?> loader)
    {
        if (path is null)
        {
            return null;
        }

        _originalPath = path;
        _originalParameters = parameters;
        _originalBaseUri = null;
        _originalSourceFormat = sourceFormat;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(loader(path, parameters));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadReader(
        XmlReader reader,
        SourceFormat sourceFormat,
        Func<XmlReader, SvgDocument?> loader)
    {
        _originalPath = null;
        _originalParameters = null;
        _originalBaseUri = null;
        _originalSourceFormat = sourceFormat;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(loader(reader));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.SvgAnimationController(SvgDocument)")]
    private SkiaSharp.SKPicture? LoadSvgDocument(SvgDocument? svgDocument, Uri? baseUri = null)
    {
        if (svgDocument is null)
        {
            return null;
        }

        if (baseUri is { })
        {
            svgDocument.BaseUri = baseUri;
        }

        SourceDocument = svgDocument;
        ClearAnimationRenderState();
        ReplaceAnimationController(null);
        InitializeJavaScriptRuntime(svgDocument);
        InvalidateRetainedSceneGraph();

        var animationController = new SvgAnimationController(svgDocument);
        if (animationController.HasAnimations)
        {
            ReplaceAnimationController(animationController);
            _ = RenderAnimationFrame(animationController.EvaluateFrameState(animationController.Clock.CurrentTime), raiseInvalidation: false, bypassThrottle: true);
            return Picture;
        }

        animationController.Dispose();
        ReplaceAnimationController(null);

        return RenderSvgDocument(svgDocument);
    }

    [RequiresUnreferencedCode("Reloading may reparse cached SVG or VectorDrawable content through trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? ReLoad(SvgParameters? parameters)
    {
        if (!CacheOriginalStream)
        {
            throw new ArgumentException($"Enable {nameof(CacheOriginalStream)} feature toggle to enable reload feature.");
        }

        string? originalPath;
        System.IO.Stream? originalStream;
        Uri? originalBaseUri;
        SourceFormat originalSourceFormat;

        lock (Sync)
        {
            _originalParameters = parameters;
            originalPath = _originalPath;
            originalStream = _originalStream;
            originalBaseUri = _originalBaseUri;
            originalSourceFormat = _originalSourceFormat;
        }

        Reset();

        if (originalStream == null)
        {
            return originalSourceFormat == SourceFormat.VectorDrawable
                ? LoadVectorDrawable(originalPath, parameters)
                : Load(originalPath, parameters);
        }

        originalStream.Position = 0;

        return originalSourceFormat == SourceFormat.VectorDrawable
            ? LoadInternal(originalStream, parameters, originalBaseUri, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable)
            : LoadSvgInternal(originalStream, parameters, originalBaseUri);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        var svgDocument = SvgService.FromSvg(svg, Settings.EnableJavaScript);
        return LoadSvgDocument(svgDocument);
    }

    [RequiresUnreferencedCode("VectorDrawable parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromVectorDrawable(string xml)
    {
        var svgDocument = SvgService.FromVectorDrawable(xml);
        return LoadSvgDocument(svgDocument);
    }

    [RequiresUnreferencedCode("Rendering from an SVG document may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromSvgDocument(SvgDocument? svgDocument)
    {
        return LoadSvgDocument(svgDocument);
    }

    public void SetAnimationTime(TimeSpan time)
    {
        AnimationController?.Clock.Seek(time);
    }

    public void AdvanceAnimation(TimeSpan delta)
    {
        AnimationController?.Clock.AdvanceBy(delta);
    }

    public void ResetAnimation()
    {
        AnimationController?.Reset();
    }

    public bool NotifyPointerEvent(SvgElement? element, SvgPointerEventType eventType)
    {
        if (!RecordAnimationPointerEvent(element, eventType))
        {
            return false;
        }

        RefreshCurrentAnimationFrame(bypassThrottle: true);
        return true;
    }

    public bool FlushPendingAnimationFrame()
    {
        if (_pendingAnimationFrameState is not { } pendingFrameState)
        {
            return false;
        }

        return RenderAnimationFrame(pendingFrameState, raiseInvalidation: true, bypassThrottle: true);
    }

    public bool Save(System.IO.Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
    {
        if (Picture is { })
        {
            if (Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, Settings.Srgb))
            {
                return true;
            }
        }

        return TrySaveBlankModelImage(stream, background, format, quality, scaleX, scaleY);
    }

    public bool Save(string path, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
    {
        using var stream = System.IO.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
        if (Save(stream, background, format, quality, scaleX, scaleY))
        {
            return true;
        }

        stream.SetLength(0);
        return false;
    }

    private bool TrySaveBlankModelImage(System.IO.Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format, int quality, float scaleX, float scaleY)
    {
        SKPicture? model;
        lock (Sync)
        {
            model = Model;
        }

        if (model is null)
        {
            return false;
        }

        var width = model.CullRect.Width * scaleX;
        var height = model.CullRect.Height * scaleY;
        if (!(width > 0) || !(height > 0))
        {
            return false;
        }

        var imageInfo = new SkiaSharp.SKImageInfo((int)width, (int)height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, Settings.Srgb);
        using var bitmap = new SkiaSharp.SKBitmap(imageInfo);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(background);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        if (data is null)
        {
            return false;
        }

        data.SaveTo(stream);
        return true;
    }

    public void Draw(SkiaSharp.SKCanvas canvas)
    {
        BeginDraw();
        try
        {
            canvas.Save();
            if (Wireframe && Model is { })
            {
                var wireframePicture = WireframePicture;
                if (wireframePicture is null && Model is { } model)
                {
                    var newWireframe = SkiaModel.ToWireframePicture(model);
                    lock (Sync)
                    {
                        if (WireframePicture is null)
                        {
                            WireframePicture = newWireframe;
                        }
                        else
                        {
                            newWireframe?.Dispose();
                        }
                        wireframePicture = WireframePicture;
                    }
                }

                if (wireframePicture is { })
                {
                    canvas.DrawPicture(wireframePicture);
                }
            }
            else if (!TryDrawAnimationLayers(canvas))
            {
                var picture = Picture;
                if (picture is null)
                {
                    canvas.Restore();
                    return;
                }

                canvas.DrawPicture(picture);
            }
            canvas.Restore();
        }
        finally
        {
            EndDraw();
        }

        RaiseOnDraw(new SKSvgDrawEventArgs(canvas));
    }

    private void Reset()
    {
        ReplaceAnimationController(null);
        SourceDocument = null;
        _javaScriptRuntime = null;
        ClearAnimationRenderState();

        lock (Sync)
        {
            WaitForDrawsLocked();
            Model = null;

            _picture?.Dispose();
            _picture = null;

            WireframePicture?.Dispose();
            WireframePicture = null;
        }

        InvalidateRetainedSceneGraph();
    }

    public void Dispose()
    {
        Reset();
        _originalStream?.Dispose();
    }

    private void BeginDraw()
    {
        lock (Sync)
        {
            _activeDraws++;
        }
    }

    private void EndDraw()
    {
        lock (Sync)
        {
            if (_activeDraws > 0 && --_activeDraws == 0)
            {
                Monitor.PulseAll(Sync);
            }
        }
    }

    private void WaitForDrawsLocked()
    {
        while (_activeDraws > 0)
        {
            Monitor.Wait(Sync);
        }
    }

    private SkiaSharp.SKPicture? RenderSvgDocument(SvgDocument svgDocument, bool invalidateRetainedSceneGraph = true)
    {
        DisableAnimationLayerCaching();

        if (!SvgSceneRuntime.TryCompile(svgDocument, AssetLoader, _ignoreAttributes, GetStandaloneViewport(), out var sceneDocument) ||
            sceneDocument is null)
        {
            return null;
        }

        if (invalidateRetainedSceneGraph)
        {
            lock (Sync)
            {
                _retainedSceneGraph = sceneDocument;
                _retainedSceneGraphDirty = false;
            }
        }

        return RenderRetainedSceneDocument(sceneDocument) ? Picture : null;
    }

    private bool RenderRetainedSceneDocument(SvgSceneDocument sceneDocument)
    {
        var model = sceneDocument.CreateModel();
        if (model is null)
        {
            return false;
        }

        var picture = SkiaModel.ToSKPicture(model);
        if (picture is null)
        {
            return false;
        }

        lock (Sync)
        {
            WaitForDrawsLocked();

            Model = model;

            _picture?.Dispose();
            _picture = picture;

            WireframePicture?.Dispose();
            WireframePicture = null;
        }

        return true;
    }

    private bool TryRenderCurrentAnimatedDocumentRetained()
    {
        SvgDocument? currentDocument;

        lock (Sync)
        {
            currentDocument = _animatedDocument ?? SourceDocument;
        }

        if (currentDocument is null)
        {
            return false;
        }

        if (!SvgSceneRuntime.TryCompile(currentDocument, AssetLoader, IgnoreAttributes, GetStandaloneViewport(), out var sceneDocument) ||
            sceneDocument is null)
        {
            return false;
        }

        lock (Sync)
        {
            _retainedSceneGraph = sceneDocument;
            _retainedSceneGraphDirty = false;
        }

        return RenderRetainedSceneDocument(sceneDocument);
    }

    private void ReplaceAnimationController(SvgAnimationController? controller)
    {
        if (AnimationController is { } existing)
        {
            existing.FrameChanged -= OnAnimationFrameChanged;
            existing.Dispose();
        }

        AnimationController = controller;
        if (_javaScriptRuntime is { } runtime)
        {
            runtime.AnimationHost = controller is null ? null : new SvgJavaScriptAnimationHostAdapter(this, controller);
        }

        if (controller is { })
        {
            controller.FrameChanged += OnAnimationFrameChanged;
        }
    }

    private void OnAnimationFrameChanged(object? sender, SvgAnimationFrameChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, AnimationController) || SourceDocument is null || AnimationController is null)
        {
            return;
        }

        _ = RenderAnimationFrame(e.Time, raiseInvalidation: true, bypassThrottle: false);
    }

    internal bool RecordAnimationPointerEvent(SvgElement? element, SvgPointerEventType eventType)
    {
        return AnimationController?.RecordPointerEvent(element, eventType) == true;
    }

    internal void RefreshCurrentAnimationFrame(bool bypassThrottle = false)
    {
        _ = RenderAnimationFrame(AnimationTime, raiseInvalidation: true, bypassThrottle: bypassThrottle);
    }

    private void NotifyAnimationTimelineMutation()
    {
        if (AnimationController is null)
        {
            return;
        }

        if (_isDispatchingAnimationTimelineCallbacks)
        {
            _animationTimelineChangedDuringDispatch = true;
            return;
        }

        RefreshCurrentAnimationFrame(bypassThrottle: true);
    }

    private void ClearAnimationRenderState()
    {
        DisableAnimationLayerCaching();
        InvalidateNativeCompositionState();
        _animatedDocument = null;
        _lastRenderedAnimationFrameState = null;
        _pendingAnimationFrameState = null;
        _lastRenderedAnimationTime = TimeSpan.MinValue;
        LastAnimationDirtyTargetCount = 0;
    }

    private void InitializeJavaScriptRuntime(
        SvgDocument svgDocument,
        bool executeDocumentScripts = true,
        bool dispatchLoadEvent = true)
    {
        _javaScriptRuntime = null;
        if (!Settings.EnableJavaScript)
        {
            return;
        }

        var runtime = new SvgJavaScriptRuntime(svgDocument, CreateJavaScriptSettings());
        _javaScriptRuntime = runtime;
        runtime.TextContentHost = new SvgJavaScriptTextContentHostAdapter(this);
        if (AnimationController is { } controller)
        {
            runtime.AnimationHost = new SvgJavaScriptAnimationHostAdapter(this, controller);
        }

        if (executeDocumentScripts)
        {
            runtime.ExecuteDocumentScripts(dispatchLoadEvent);
        }
    }

    internal SvgJavaScriptEventResult DispatchJavaScriptEvent(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgPointerInput input)
    {
        SvgJavaScriptEvent? eventFacade = null;
        return DispatchJavaScriptEvent(
            element,
            targetElement,
            relatedElement,
            eventType,
            attributeName,
            input,
            ref eventFacade);
    }

    internal SvgJavaScriptEventResult DispatchJavaScriptEvent(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgPointerInput input,
        ref SvgJavaScriptEvent? eventFacade)
    {
        var runtime = _javaScriptRuntime;
        if (runtime is null || SourceDocument is null)
        {
            return SvgJavaScriptEventResult.NotExecuted;
        }

        var handlerElement = NormalizeJavaScriptEventElement(element);
        var sourceTargetElement = NormalizeJavaScriptEventElement(targetElement);
        var sourceRelatedElement = relatedElement is null ? null : NormalizeJavaScriptEventElement(relatedElement);
        var resolvedTargetNode = ResolveJavaScriptEventTarget(runtime, sourceTargetElement, input.PicturePoint);
        var resolvedRelatedTargetNode = sourceRelatedElement is null ? null : runtime.GetElement(sourceRelatedElement);
        eventFacade ??= runtime.CreateEvent(
            eventType,
            resolvedTargetNode,
            resolvedRelatedTargetNode,
            CreateJavaScriptEventInput(input));

        var mutationVersion = runtime.MutationVersion;
        var result = runtime.ExecuteEventHandlerAndListeners(
            handlerElement,
            eventFacade,
            eventType,
            attributeName);
        if (runtime.MutationVersion == mutationVersion)
        {
            return result;
        }

        InvalidateRetainedSceneGraph();
        if (AnimationController is { })
        {
            ClearAnimationRenderState();
            RefreshCurrentAnimationFrame(bypassThrottle: true);
        }
        else
        {
            _ = RenderSvgDocument(SourceDocument);
        }

        return result;
    }

    private object ResolveJavaScriptEventTarget(SvgJavaScriptRuntime runtime, SvgElement targetElement, SKPoint picturePoint)
    {
        if (targetElement is SvgUse use &&
            HitTestTopmostSceneNode(picturePoint) is { } hitNode)
        {
            var hitTargetElement = hitNode.HitTestTargetElement is null
                ? null
                : NormalizeJavaScriptEventElement(hitNode.HitTestTargetElement);
            var correspondingElement = hitNode.Element is SvgElement element
                ? NormalizeJavaScriptEventElement(element)
                : null;

            if (ReferenceEquals(hitTargetElement, targetElement) &&
                correspondingElement is not null &&
                !ReferenceEquals(correspondingElement, targetElement))
            {
                var instance = runtime.FindUseInstance(use, correspondingElement);
                if (instance is not null)
                {
                    return instance;
                }
            }
        }

        return runtime.GetElement(targetElement);
    }

    private SvgElement NormalizeJavaScriptEventElement(SvgElement element)
    {
        if (SourceDocument is null ||
            ReferenceEquals(element.OwnerDocument, SourceDocument))
        {
            return element;
        }

        var resolved = SvgElementAddress.Create(element).Resolve(SourceDocument);
        return resolved ?? element;
    }

    private SvgJavaScriptSettings CreateJavaScriptSettings()
    {
        return new SvgJavaScriptSettings
        {
            EnableExternalJavaScript = Settings.EnableExternalJavaScript,
            TimeoutMilliseconds = Settings.JavaScriptTimeoutMilliseconds,
            MaxStatements = Settings.JavaScriptMaxStatements,
            ThrowOnError = Settings.ThrowOnJavaScriptError
        };
    }

    private static SvgJavaScriptEventInput CreateJavaScriptEventInput(SvgPointerInput input)
    {
        return new SvgJavaScriptEventInput(
            input.PicturePoint.X,
            input.PicturePoint.Y,
            ToJavaScriptMouseButton(input.Button),
            input.ClickCount,
            input.WheelDelta,
            input.AltKey,
            input.ShiftKey,
            input.CtrlKey);
    }

    private static SvgJavaScriptMouseButton ToJavaScriptMouseButton(SvgMouseButton button)
    {
        return button switch
        {
            SvgMouseButton.Left => SvgJavaScriptMouseButton.Left,
            SvgMouseButton.Middle => SvgJavaScriptMouseButton.Middle,
            SvgMouseButton.Right => SvgJavaScriptMouseButton.Right,
            SvgMouseButton.XButton1 => SvgJavaScriptMouseButton.XButton1,
            SvgMouseButton.XButton2 => SvgJavaScriptMouseButton.XButton2,
            _ => SvgJavaScriptMouseButton.None
        };
    }

    private sealed class SvgJavaScriptAnimationHostAdapter : ISvgJavaScriptAnimationHost
    {
        private readonly SKSvg _owner;
        private readonly SvgAnimationController _controller;

        public SvgJavaScriptAnimationHostAdapter(SKSvg owner, SvgAnimationController controller)
        {
            _owner = owner;
            _controller = controller;
        }

        public TimeSpan CurrentTime => _owner._animationTimelineCallbackTime ?? _controller.Clock.CurrentTime;

        public void Seek(TimeSpan time)
        {
            _controller.Clock.Seek(time);
        }

        public bool BeginElement(SvgAnimationElement animation, TimeSpan offset)
        {
            var scheduled = _controller.BeginElement(animation, TranslateOffset(offset));
            if (scheduled)
            {
                _owner.NotifyAnimationTimelineMutation();
            }

            return scheduled;
        }

        public bool EndElement(SvgAnimationElement animation, TimeSpan offset)
        {
            var scheduled = _controller.EndElement(animation, TranslateOffset(offset));
            if (scheduled)
            {
                _owner.NotifyAnimationTimelineMutation();
            }

            return scheduled;
        }

        public bool TryGetStartTime(SvgAnimationElement animation, out TimeSpan startTime)
        {
            return _controller.TryGetStartTime(animation, CurrentTime, out startTime);
        }

        public bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value)
        {
            return _controller.TryGetBaseAttributeValue(element, attributeName, out value);
        }

        private TimeSpan TranslateOffset(TimeSpan offset)
        {
            if (_owner._animationTimelineCallbackTime is not { } callbackTime)
            {
                return offset;
            }

            return callbackTime - _controller.Clock.CurrentTime + offset;
        }
    }

    private sealed class SvgJavaScriptTextContentHostAdapter : ISvgJavaScriptTextContentHost
    {
        private readonly SKSvg _owner;
        private readonly Dictionary<SvgTextBase, SvgSceneTextCompiler.SvgTextContentMetrics> _metricsByElement = new();
        private int _mutationVersion = -1;

        public SvgJavaScriptTextContentHostAdapter(SKSvg owner)
        {
            _owner = owner;
        }

        public double GetComputedTextLength(SvgTextBase textContentElement)
        {
            return GetMetrics(textContentElement).ComputedTextLength;
        }

        public int GetNumberOfChars(SvgTextBase textContentElement)
        {
            return GetMetrics(textContentElement).NumberOfChars;
        }

        public double GetSubStringLength(SvgTextBase textContentElement, int charnum, int nchars)
        {
            return GetMetrics(textContentElement).GetSubStringLength(charnum, nchars);
        }

        public SvgJavaScriptPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            var point = GetMetrics(textContentElement).GetStartPositionOfChar(charnum);
            return new SvgJavaScriptPoint(point.X, point.Y);
        }

        public SvgJavaScriptPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            var point = GetMetrics(textContentElement).GetEndPositionOfChar(charnum);
            return new SvgJavaScriptPoint(point.X, point.Y);
        }

        public SvgJavaScriptRect GetExtentOfChar(SvgTextBase textContentElement, int charnum)
        {
            var rect = GetMetrics(textContentElement).GetExtentOfChar(charnum);
            return new SvgJavaScriptRect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public double GetRotationOfChar(SvgTextBase textContentElement, int charnum)
        {
            return GetMetrics(textContentElement).GetRotationOfChar(charnum);
        }

        public int GetCharNumAtPosition(SvgTextBase textContentElement, SvgJavaScriptPoint point)
        {
            return GetMetrics(textContentElement).GetCharNumAtPosition(new SKPoint(point.x, point.y));
        }

        public void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars)
        {
            _ = GetMetrics(textContentElement).GetSubStringLength(charnum, nchars);
        }

        private SvgSceneTextCompiler.SvgTextContentMetrics GetMetrics(SvgTextBase textContentElement)
        {
            var mutationVersion = _owner._javaScriptRuntime?.MutationVersion ?? 0;
            if (_mutationVersion != mutationVersion)
            {
                _metricsByElement.Clear();
                _mutationVersion = mutationVersion;
            }

            if (_metricsByElement.TryGetValue(textContentElement, out var metrics))
            {
                return metrics;
            }

            if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, _owner.GetStandaloneViewport(), _owner.AssetLoader, out metrics))
            {
                metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
            }

            _metricsByElement[textContentElement] = metrics;
            return metrics;
        }

    }

    private bool DispatchAnimationTimelineCallbacks(ref SvgAnimationFrameState frameState)
    {
        if (SourceDocument is null ||
            AnimationController is null ||
            _javaScriptRuntime is not { } runtime)
        {
            return false;
        }

        var previousTime = _lastRenderedAnimationTime != TimeSpan.MinValue
            ? _lastRenderedAnimationTime
            : (TimeSpan?)null;
        var callbacks = AnimationController.GetTimelineCallbacks(frameState.Time, previousTime);
        if (callbacks.Count == 0)
        {
            return false;
        }

        var mutated = false;
        _animationTimelineChangedDuringDispatch = false;
        _isDispatchingAnimationTimelineCallbacks = true;
        try
        {
            for (var index = 0; index < callbacks.Count; index++)
            {
                var callback = callbacks[index];
                if (callback.AnimationAddress.Resolve(SourceDocument) is not SvgAnimationElement animation)
                {
                    continue;
                }

                _animationTimelineCallbackTime = callback.Time;
                try
                {
                    var result = runtime.ExecuteEventHandlerAndListeners(
                        animation,
                        runtime.GetElement(animation),
                        relatedTargetNode: null,
                        callback.EventType,
                        callback.AttributeName,
                        input: null);
                    mutated |= result.Mutated;
                }
                finally
                {
                    _animationTimelineCallbackTime = null;
                }
            }
        }
        finally
        {
            _isDispatchingAnimationTimelineCallbacks = false;
            _animationTimelineCallbackTime = null;
        }

        if (!mutated && !_animationTimelineChangedDuringDispatch)
        {
            return false;
        }

        frameState = AnimationController.EvaluateFrameState(frameState.Time);
        if (!mutated)
        {
            return false;
        }

        InvalidateRetainedSceneGraph();
        DisableAnimationLayerCaching();
        InvalidateNativeCompositionState();
        _animatedDocument = null;
        _lastRenderedAnimationFrameState = null;
        _pendingAnimationFrameState = null;
        return true;
    }

    private bool RenderAnimationFrame(TimeSpan time, bool raiseInvalidation, bool bypassThrottle)
    {
        if (SourceDocument is null || AnimationController is null)
        {
            return false;
        }

        return RenderAnimationFrame(AnimationController.EvaluateFrameState(time), raiseInvalidation, bypassThrottle);
    }

    private bool RenderAnimationFrame(SvgAnimationFrameState frameState, bool raiseInvalidation, bool bypassThrottle)
    {
        if (SourceDocument is null || AnimationController is null)
        {
            return false;
        }

        var forceRender = DispatchAnimationTimelineCallbacks(ref frameState);

        if (!forceRender &&
            _lastRenderedAnimationFrameState is { } lastRenderedFrameState &&
            frameState.IsEquivalentTo(lastRenderedFrameState))
        {
            _pendingAnimationFrameState = null;
            LastAnimationDirtyTargetCount = 0;
            return false;
        }

        if (!bypassThrottle &&
            AnimationMinimumRenderInterval > TimeSpan.Zero &&
            _lastRenderedAnimationFrameState is not null &&
            _lastRenderedAnimationTime != TimeSpan.MinValue &&
            (frameState.Time - _lastRenderedAnimationTime).Duration() < AnimationMinimumRenderInterval)
        {
            _pendingAnimationFrameState = frameState;
            LastAnimationDirtyTargetCount = frameState.GetDirtyTargetCount(_lastRenderedAnimationFrameState);
            return false;
        }

        if (_animatedDocument is null)
        {
            _animatedDocument = AnimationController.CreateAnimatedDocument(frameState);
            LastAnimationDirtyTargetCount = frameState.Count;
        }
        else
        {
            LastAnimationDirtyTargetCount = frameState.GetDirtyTargetCount(_lastRenderedAnimationFrameState);
            AnimationController.ApplyFrameState(_animatedDocument, frameState, _lastRenderedAnimationFrameState);
        }

        var retainedSceneReady = TryPrepareRetainedSceneGraphForAnimationFrame(frameState, _lastRenderedAnimationFrameState, out var retainedSceneDocument);
        var rendered = false;
        if (retainedSceneReady &&
            retainedSceneDocument is not null &&
            (UsesAnimationLayerCaching || TryInitializeAnimationLayerCaching(retainedSceneDocument)))
        {
            rendered = TryRenderAnimationLayerFrame(retainedSceneDocument, frameState, _lastRenderedAnimationFrameState);
            if (!rendered)
            {
                DisableAnimationLayerCaching();
            }
        }

        if (!rendered)
        {
            rendered = retainedSceneReady &&
                       retainedSceneDocument is not null &&
                       RenderRetainedSceneDocument(retainedSceneDocument);
        }

        if (!rendered)
        {
            rendered = TryRenderCurrentAnimatedDocumentRetained();
        }

        if (!rendered)
        {
            return false;
        }

        _lastRenderedAnimationFrameState = frameState;
        _lastRenderedAnimationTime = frameState.Time;
        _pendingAnimationFrameState = null;

        if (raiseInvalidation)
        {
            RaiseAnimationInvalidated(new SvgAnimationFrameChangedEventArgs(frameState.Time));
        }

        return true;
    }

    private SKRect GetStandaloneViewport()
    {
        return GetStandaloneViewport(Settings);
    }

    private static SKRect GetStandaloneViewport(SKSvgSettings settings)
    {
        var standaloneViewport = settings.StandaloneViewport;
        if (standaloneViewport is null || standaloneViewport.Value.Width <= 0f || standaloneViewport.Value.Height <= 0f)
        {
            return SKRect.Empty;
        }

        return SKRect.Create(
            standaloneViewport.Value.Left,
            standaloneViewport.Value.Top,
            standaloneViewport.Value.Width,
            standaloneViewport.Value.Height);
    }
}
