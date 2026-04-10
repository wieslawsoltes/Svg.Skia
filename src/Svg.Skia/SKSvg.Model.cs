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
    /// Creates a deep clone of the current <see cref="SKSvg"/>, including model and reload data.
    /// </summary>
    /// <returns>A new <see cref="SKSvg"/> instance with independent state.</returns>
    [RequiresUnreferencedCode("Clone may recreate retained scene and animation state that use TypeDescriptor-based converters.")]
    public SKSvg Clone()
    {
        var clone = new SKSvg();

        clone.Settings.AlphaType = Settings.AlphaType;
        clone.Settings.ColorType = Settings.ColorType;
        clone.Settings.SrgbLinear = Settings.SrgbLinear;
        clone.Settings.Srgb = Settings.Srgb;
        clone.Settings.StandaloneViewport = Settings.StandaloneViewport;
        clone.Settings.EnableSvgFonts = Settings.EnableSvgFonts;
        clone.Settings.TypefaceProviders = Settings.TypefaceProviders is null
            ? null
            : new List<TypefaceProviders.ITypefaceProvider>(Settings.TypefaceProviders);

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

            if (HasAnimations)
            {
                clone.ReplaceAnimationController(new SvgAnimationController(sourceDocumentClone));
                clone.SetAnimationTime(AnimationTime);
            }
        }

        clone.InvalidateRetainedSceneGraph();
        return clone;
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadInternal(stream, parameters, null, SourceFormat.Svg, SvgService.Open);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        return LoadInternal(stream, parameters, baseUri, SourceFormat.Svg, SvgService.Open);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(string? path, SvgParameters? parameters = null)
    {
        return LoadPath(path, parameters, SourceFormat.Svg, SvgService.Open);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(string path) => Load(path, null);

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? Load(XmlReader reader)
    {
        return LoadReader(reader, SourceFormat.Svg, SvgService.Open);
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
        InvalidateRetainedSceneGraph();

        var animationController = new SvgAnimationController(svgDocument);
        if (animationController.HasAnimations)
        {
            ReplaceAnimationController(animationController);
            _ = RenderAnimationFrame(animationController.EvaluateFrameState(TimeSpan.Zero), raiseInvalidation: false, bypassThrottle: true);
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
            : LoadInternal(originalStream, parameters, originalBaseUri, SourceFormat.Svg, SvgService.Open);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        var svgDocument = SvgService.FromSvg(svg);
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

        if (_lastRenderedAnimationFrameState is { } lastRenderedFrameState &&
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
