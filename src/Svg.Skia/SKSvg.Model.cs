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

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public static SKSvg CreateFromSvg(string svg, SvgParameters? parameters, Uri? baseUri = null)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvg(svg, parameters, baseUri);
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
    private SaveImageCacheEntry? _saveImageCache;

    private sealed class SaveImageCacheEntry
    {
        public SaveImageCacheEntry(
            SkiaSharp.SKPicture picture,
            SkiaSharp.SKColor background,
            SkiaSharp.SKEncodedImageFormat format,
            int quality,
            float scaleX,
            float scaleY,
            byte[] encodedBytes,
            int encodedByteCount)
        {
            Picture = picture;
            Background = background;
            Format = format;
            Quality = quality;
            ScaleX = scaleX;
            ScaleY = scaleY;
            EncodedBytes = encodedBytes;
            EncodedByteCount = encodedByteCount;
        }

        public SkiaSharp.SKPicture Picture { get; }
        public SkiaSharp.SKColor Background { get; }
        public SkiaSharp.SKEncodedImageFormat Format { get; }
        public int Quality { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
        public byte[] EncodedBytes { get; }
        public int EncodedByteCount { get; }

        public bool Matches(
            SkiaSharp.SKPicture picture,
            SkiaSharp.SKColor background,
            SkiaSharp.SKEncodedImageFormat format,
            int quality,
            float scaleX,
            float scaleY)
        {
            return ReferenceEquals(Picture, picture) &&
                   Background == background &&
                   Format == format &&
                   Quality == quality &&
                   ScaleX.Equals(scaleX) &&
                   ScaleY.Equals(scaleY);
        }
    }

    public object Sync { get; } = new();

    public SKSvgSettings Settings { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    private SKPicture? _model;
    public SKPicture? Model
    {
        get
        {
            SKPicture? model;
            lock (Sync)
            {
                model = _model;
                if (model is not null)
                {
                    return model;
                }
            }

            if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
            {
                return null;
            }

            var newModel = sceneDocument.CreateModel();
            if (newModel is null)
            {
                return null;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(_retainedSceneGraph, sceneDocument) || _retainedSceneGraphDirty)
                {
                    return _model;
                }

                if (_model is { } existing)
                {
                    return existing;
                }

                _model = newModel;
                return newModel;
            }
        }
        private set => _model = value;
    }

    private SvgDocument? _sourceDocument;
    private SvgDocument? _trackedSourceDocument;
    public SvgDocument? SourceDocument
    {
        get
        {
            var document = _sourceDocument;
            if (document is not null)
            {
                EnsureSourceDocumentMutationTracking(document);
            }

            return document;
        }
        private set
        {
            if (!ReferenceEquals(_sourceDocument, value))
            {
                _trackedSourceDocument = null;
            }

            _sourceDocument = value;
        }
    }

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
            SvgSceneDocument? sceneDocument;
            lock (Sync)
            {
                picture = _picture;
                if (picture is { })
                {
                    return picture;
                }

                model = _model;
                sceneDocument = !_retainedSceneGraphDirty
                    ? _retainedSceneGraph
                    : null;
                if (model is null && sceneDocument is null)
                {
                    sceneDocument = null;
                }
            }

            if (model is not null)
            {
                var newPicture = SkiaModel.ToSKPicture(model);
                if (newPicture is null)
                {
                    return null;
                }

                lock (Sync)
                {
                    if (!ReferenceEquals(_model, model))
                    {
                        newPicture.Dispose();
                        return _picture;
                    }

                    if (_picture is { } existing)
                    {
                        newPicture.Dispose();
                        return existing;
                    }

                    ClearSaveImageCacheLocked();
                    _picture = newPicture;
                    return newPicture;
                }
            }

            if (sceneDocument is null &&
                (!TryEnsureRetainedSceneGraph(out sceneDocument) || sceneDocument is null))
            {
                return null;
            }

            var directPicture = SkiaModel.ToSKPicture(sceneDocument);
            if (directPicture is null)
            {
                return null;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(_retainedSceneGraph, sceneDocument) || _retainedSceneGraphDirty)
                {
                    directPicture.Dispose();
                    return _picture;
                }

                if (_picture is { } existing)
                {
                    directPicture.Dispose();
                    return existing;
                }

                ClearSaveImageCacheLocked();
                _picture = directPicture;
                return directPicture;
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
                ClearSaveImageCacheLocked();
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

            ClearSaveImageCacheLocked();
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
        clone.Settings.EnableTextReferences = Settings.EnableTextReferences;
        clone.Settings.TypefaceProviders = Settings.ConfiguredTypefaceProviders is null
            ? null
            : new List<TypefaceProviders.ITypefaceProvider>(Settings.ConfiguredTypefaceProviders);

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

        if (_model is { } model)
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
        return LoadInternal(
            stream,
            parameters,
            baseUri,
            SourceFormat.Svg,
            baseUri is null
                ? SvgService.Open
                : (input, inputParameters) => SvgService.Open(input, inputParameters, baseUri),
            applyBaseUriAfterLoad: baseUri is null);
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
        Func<System.IO.Stream, SvgParameters?, SvgDocument?> loader,
        bool applyBaseUriAfterLoad = true)
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
            ClearOriginalLoadStateIfNeeded();
            svgDocument = loader(stream, parameters);
        }

        return LoadSvgDocument(svgDocument, applyBaseUriAfterLoad ? baseUri : null);
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

        if (CacheOriginalStream)
        {
            _originalPath = path;
            _originalParameters = parameters;
            _originalBaseUri = null;
            _originalSourceFormat = sourceFormat;
            _originalStream?.Dispose();
            _originalStream = null;
        }
        else
        {
            ClearOriginalLoadStateIfNeeded();
        }

        return LoadSvgDocument(loader(path, parameters));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SKSvg.LoadSvgDocument(SvgDocument, Uri)")]
    private SkiaSharp.SKPicture? LoadReader(
        XmlReader reader,
        SourceFormat sourceFormat,
        Func<XmlReader, SvgDocument?> loader)
    {
        if (CacheOriginalStream)
        {
            _originalPath = null;
            _originalParameters = null;
            _originalBaseUri = null;
            _originalSourceFormat = sourceFormat;
            _originalStream?.Dispose();
            _originalStream = null;
        }
        else
        {
            ClearOriginalLoadStateIfNeeded();
        }

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

        var sameStaticDocumentReload = CanReloadSameStaticDocumentInPlace(svgDocument);
        var requiresStateReset = !sameStaticDocumentReload && RequiresDocumentLoadStateReset();

        if (requiresStateReset)
        {
            ClearAnimationRenderState();
            InvalidateRetainedSceneGraph();
        }

        SourceDocument = svgDocument;

        if (ContainsAnimationElements(svgDocument))
        {
            var animationController = new SvgAnimationController(svgDocument);
            if (animationController.HasAnimations)
            {
                ReplaceAnimationController(animationController);
                _ = RenderAnimationFrame(animationController.EvaluateFrameState(TimeSpan.Zero), raiseInvalidation: false, bypassThrottle: true);
                return Picture;
            }

            animationController.Dispose();
        }

        if (AnimationController is not null)
        {
            ReplaceAnimationController(null);
        }

        if (sameStaticDocumentReload &&
            TryRenderPendingSameDocumentAttributeMutations(svgDocument, out var sameDocumentPicture))
        {
            return sameDocumentPicture;
        }

        return sameStaticDocumentReload
            ? RenderSvgDocument(svgDocument, invalidateRetainedSceneGraph: true)
            : RenderSvgDocument(
                svgDocument,
                allowFreshStaticLoadFastPath: !requiresStateReset);
    }

    private bool RequiresDocumentLoadStateReset()
    {
        lock (Sync)
        {
            return SourceDocument is not null ||
                   AnimationController is not null ||
                   _animatedDocument is not null ||
                   _lastRenderedAnimationFrameState is not null ||
                   _pendingAnimationFrameState is not null ||
                   _lastRenderedAnimationTime != TimeSpan.MinValue ||
                   LastAnimationDirtyTargetCount != 0 ||
                   _picture is not null ||
                   _model is not null ||
                   WireframePicture is not null ||
                   _retainedPicture is not null ||
                   _retainedNodePictures is not null ||
                   _retainedSceneGraph is not null ||
                   !_retainedSceneGraphDirty ||
                   HasAnimationLayerCachingStateLocked() ||
                   HasNativeCompositionState();
        }
    }

    private bool TryRenderPendingSameDocumentAttributeMutations(
        SvgDocument svgDocument,
        out SkiaSharp.SKPicture? picture)
    {
        picture = null;
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) ||
            sceneDocument is null ||
            !ReferenceEquals(sceneDocument.SourceDocument, svgDocument))
        {
            return false;
        }

        List<(SvgElement Element, IReadOnlyCollection<string> ChangedAttributes)>? pendingMutations = null;
        var stack = new Stack<SvgElement>();
        stack.Push(svgDocument);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.ConsumeSceneGraphPendingChangedAttributes() is { Count: > 0 } changedAttributes)
            {
                pendingMutations ??= new List<(SvgElement, IReadOnlyCollection<string>)>();
                pendingMutations.Add((current, changedAttributes));
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        if (pendingMutations is null || pendingMutations.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < pendingMutations.Count; i++)
        {
            var mutation = pendingMutations[i];
            var result = sceneDocument.ApplyMutation(mutation.Element, mutation.ChangedAttributes);
            if (!result.Succeeded)
            {
                return false;
            }
        }

        DisableAnimationLayerCaching();
        picture = RenderRetainedSceneDocument(sceneDocument);
        return picture is not null;
    }

    private void EnsureSourceDocumentMutationTracking(SvgDocument document)
    {
        lock (Sync)
        {
            if (ReferenceEquals(_trackedSourceDocument, document))
            {
                return;
            }
        }

        var stack = new Stack<SvgElement>();
        stack.Push(document);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            _ = current.GetSceneGraphCompileMetadataVersion();

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        lock (Sync)
        {
            if (ReferenceEquals(_sourceDocument, document))
            {
                _trackedSourceDocument = document;
            }
        }
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
            : LoadInternal(
                originalStream,
                parameters,
                originalBaseUri,
                SourceFormat.Svg,
                originalBaseUri is null
                    ? SvgService.Open
                    : (input, inputParameters) => SvgService.Open(input, inputParameters, originalBaseUri),
                applyBaseUriAfterLoad: originalBaseUri is null);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        var svgDocument = SvgService.FromSvg(svg);
        return LoadSvgDocument(svgDocument);
    }

    [RequiresUnreferencedCode("SVG document parsing may use trim-unsafe runtime discovery paths.")]
    public SkiaSharp.SKPicture? FromSvg(string svg, SvgParameters? parameters, Uri? baseUri = null)
    {
        var svgDocument = baseUri is null
            ? SvgService.FromSvg(svg, parameters)
            : SvgService.FromSvg(svg, parameters, baseUri);
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
        if (Picture is { } picture)
        {
            if (TryWriteCachedPictureSave(picture, stream, background, format, quality, scaleX, scaleY))
            {
                return true;
            }

            using var encodedStream = new MemoryStream();
            if (picture.ToImage(encodedStream, background, format, quality, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, Settings.Srgb))
            {
                byte[] encodedBytes;
                int encodedByteCount;
                if (encodedStream.TryGetBuffer(out var encodedBuffer))
                {
                    encodedBytes = encodedBuffer.Array!;
                    encodedByteCount = (int)encodedStream.Length;
                }
                else
                {
                    encodedBytes = encodedStream.ToArray();
                    encodedByteCount = encodedBytes.Length;
                }

                CachePictureSave(picture, background, format, quality, scaleX, scaleY, encodedBytes, encodedByteCount);
                stream.Write(encodedBytes, 0, encodedByteCount);
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
        return bitmap.Encode(stream, format, quality);
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
        lock (Sync)
        {
            if (CanResetSimpleStaticLoadLocked())
            {
                SourceDocument = null;
                ClearSaveImageCacheLocked();
                _picture?.Dispose();
                _picture = null;
                _retainedSceneGraph = null;
                _retainedSceneGraphDirty = true;
                return;
            }
        }

        ReplaceAnimationController(null);
        SourceDocument = null;
        ClearAnimationRenderState();

        lock (Sync)
        {
            if (HasRenderedPictureStateLocked())
            {
                WaitForDrawsLocked();
                Model = null;

                ClearSaveImageCacheLocked();
                ClearRetainedPictureLocked();
                _picture?.Dispose();
                _picture = null;

                WireframePicture?.Dispose();
                WireframePicture = null;
            }
            else
            {
                Model = null;
                ClearSaveImageCacheLocked();
                _picture = null;
                WireframePicture = null;
            }

            _retainedSceneGraph = null;
            _retainedSceneGraphDirty = true;
        }
    }

    private bool CanResetSimpleStaticLoadLocked()
    {
        return AnimationController is null &&
               _animatedDocument is null &&
               _lastRenderedAnimationFrameState is null &&
               _pendingAnimationFrameState is null &&
               _lastRenderedAnimationTime == TimeSpan.MinValue &&
               LastAnimationDirtyTargetCount == 0 &&
               !HasAnimationLayerCachingStateLocked() &&
               !HasNativeCompositionState() &&
               _activeDraws == 0 &&
               _model is null &&
               WireframePicture is null &&
               _retainedPicture is null &&
               _retainedNodePictures is null;
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

    private void ClearOriginalLoadStateIfNeeded()
    {
        if (_originalPath is null &&
            _originalStream is null &&
            _originalParameters is null &&
            _originalBaseUri is null)
        {
            return;
        }

        _originalPath = null;
        _originalParameters = null;
        _originalBaseUri = null;
        _originalSourceFormat = SourceFormat.Svg;
        _originalStream?.Dispose();
        _originalStream = null;
    }

    private void WaitForDrawsLocked()
    {
        while (_activeDraws > 0)
        {
            Monitor.Wait(Sync);
        }
    }

    private bool HasRenderedPictureStateLocked()
    {
        return _activeDraws > 0 ||
               _model is not null ||
               _picture is not null ||
               WireframePicture is not null ||
               _retainedPicture is not null ||
               _retainedNodePictures is not null;
    }

    private void ClearSaveImageCacheLocked()
    {
        _saveImageCache = null;
    }

    private bool TryWriteCachedPictureSave(
        SkiaSharp.SKPicture picture,
        Stream stream,
        SkiaSharp.SKColor background,
        SkiaSharp.SKEncodedImageFormat format,
        int quality,
        float scaleX,
        float scaleY)
    {
        byte[]? encodedBytes;
        var encodedByteCount = 0;
        lock (Sync)
        {
            if (_saveImageCache is { } cache &&
                cache.Matches(picture, background, format, quality, scaleX, scaleY))
            {
                encodedBytes = cache.EncodedBytes;
                encodedByteCount = cache.EncodedByteCount;
            }
            else
            {
                encodedBytes = null;
            }
        }

        if (encodedBytes is null)
        {
            return false;
        }

        stream.Write(encodedBytes, 0, encodedByteCount);
        return true;
    }

    private void CachePictureSave(
        SkiaSharp.SKPicture picture,
        SkiaSharp.SKColor background,
        SkiaSharp.SKEncodedImageFormat format,
        int quality,
        float scaleX,
        float scaleY,
        byte[] encodedBytes,
        int encodedByteCount)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_picture, picture))
            {
                return;
            }

            _saveImageCache = new SaveImageCacheEntry(
                picture,
                background,
                format,
                quality,
                scaleX,
                scaleY,
                encodedBytes,
                encodedByteCount);
        }
    }

    private static bool ContainsAnimationElements(SvgElement root)
    {
        var stack = new Stack<SvgElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is SvgAnimationElement)
            {
                return true;
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        return false;
    }

    private SkiaSharp.SKPicture? RenderSvgDocument(
        SvgDocument svgDocument,
        bool invalidateRetainedSceneGraph = true,
        bool allowFreshStaticLoadFastPath = false)
    {
        DisableAnimationLayerCaching();

        if (!SvgSceneRuntime.TryCompile(svgDocument, AssetLoader, _ignoreAttributes, GetStandaloneViewport(), out var sceneDocument) ||
            sceneDocument is null)
        {
            return null;
        }

        return RenderRetainedSceneDocument(
            sceneDocument,
            updateRetainedSceneGraph: invalidateRetainedSceneGraph,
            allowFreshStaticLoadFastPath: allowFreshStaticLoadFastPath);
    }

    private SkiaSharp.SKPicture? RenderRetainedSceneDocument(
        SvgSceneDocument sceneDocument,
        bool updateRetainedSceneGraph = false,
        bool allowFreshStaticLoadFastPath = false)
    {
        var picture = SkiaModel.ToSKPicture(sceneDocument);
        if (picture is null)
        {
            return null;
        }

        lock (Sync)
        {
            if (allowFreshStaticLoadFastPath &&
                CanPublishFirstStaticLoadLocked(sceneDocument.SourceDocument))
            {
                if (updateRetainedSceneGraph)
                {
                    _retainedSceneGraph = sceneDocument;
                    _retainedSceneGraphDirty = false;
                }

                _picture = picture;
                return picture;
            }

            if (updateRetainedSceneGraph)
            {
                _retainedSceneGraph = sceneDocument;
                _retainedSceneGraphDirty = false;
            }

            if (!HasRenderedPictureStateLocked())
            {
                ClearSaveImageCacheLocked();
                _picture = picture;
                return picture;
            }

            if (CanReplaceSimpleStaticPictureLocked())
            {
                ClearSaveImageCacheLocked();
                _picture?.Dispose();
                _picture = picture;
                return picture;
            }

            WaitForDrawsLocked();
            Model = null;

            ClearSaveImageCacheLocked();
            ClearRetainedPictureLocked();
            _picture?.Dispose();
            _picture = picture;

            WireframePicture?.Dispose();
            WireframePicture = null;
        }

        return picture;
    }

    private bool CanPublishFirstStaticLoadLocked(SvgDocument? sourceDocument)
    {
        return sourceDocument is not null &&
               ReferenceEquals(SourceDocument, sourceDocument) &&
               AnimationController is null &&
               _animatedDocument is null &&
               _lastRenderedAnimationFrameState is null &&
               _pendingAnimationFrameState is null &&
               _lastRenderedAnimationTime == TimeSpan.MinValue &&
               LastAnimationDirtyTargetCount == 0 &&
               _picture is null &&
               _model is null &&
               WireframePicture is null &&
               _retainedPicture is null &&
               _retainedNodePictures is null &&
               _retainedSceneGraph is null &&
               _retainedSceneGraphDirty &&
               !HasAnimationLayerCachingStateLocked() &&
               !HasNativeCompositionState() &&
               _activeDraws == 0;
    }

    private bool CanReplaceSimpleStaticPictureLocked()
    {
        return AnimationController is null &&
               _animatedDocument is null &&
               _lastRenderedAnimationFrameState is null &&
               _pendingAnimationFrameState is null &&
               _lastRenderedAnimationTime == TimeSpan.MinValue &&
               LastAnimationDirtyTargetCount == 0 &&
               !HasAnimationLayerCachingStateLocked() &&
               !HasNativeCompositionState() &&
               _activeDraws == 0 &&
               _picture is not null &&
               _model is null &&
               WireframePicture is null &&
               _retainedPicture is null &&
               _retainedNodePictures is null;
    }

    private bool CanReloadSameStaticDocumentInPlace(SvgDocument sourceDocument)
    {
        lock (Sync)
        {
            return ReferenceEquals(SourceDocument, sourceDocument) &&
                   AnimationController is null &&
                   _animatedDocument is null &&
                   _lastRenderedAnimationFrameState is null &&
                   _pendingAnimationFrameState is null &&
                   _lastRenderedAnimationTime == TimeSpan.MinValue &&
                   LastAnimationDirtyTargetCount == 0 &&
                   !HasAnimationLayerCachingStateLocked() &&
                   !HasNativeCompositionState();
        }
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

        return RenderRetainedSceneDocument(sceneDocument, updateRetainedSceneGraph: true) is not null;
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

    private bool HasNativeCompositionState()
    {
        return _nativeCompositionSourceDocument is not null ||
               _nativeCompositionSourceScene is not null ||
               _nativeCompositionAnimatedChildIndexes is not null ||
               _nativeCompositionAnimatedTargetKeys is not null ||
               !_nativeCompositionSourceBounds.IsEmpty ||
               _nativeCompositionIgnoreAttributes != DrawAttributes.None;
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
                       RenderRetainedSceneDocument(retainedSceneDocument) is not null;
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
