// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
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

    public static SKSvg CreateFromStream(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(stream, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromStream(System.IO.Stream stream) => CreateFromStream(stream, null);

    public static SKSvg CreateFromFile(string path, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(path, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromFile(string path) => CreateFromFile(path, null);

    public static SKSvg CreateFromVectorDrawable(string path, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(path, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromVectorDrawable(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(stream, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromVectorDrawable(XmlReader reader)
    {
        var skSvg = new SKSvg();
        skSvg.LoadVectorDrawable(reader);
        return skSvg;
    }

    public static SKSvg CreateFromXmlReader(XmlReader reader)
    {
        var skSvg = new SKSvg();
        skSvg.Load(reader);
        return skSvg;
    }

    public static SKSvg CreateFromSvg(string svg)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvg(svg);
        return skSvg;
    }

    public static SKSvg CreateFromSvgDocument(SvgDocument svgDocument)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvgDocument(svgDocument);
        return skSvg;
    }

    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        using var documentFontScope = PushDocumentFonts(svgFragment as SvgDocument ?? svgFragment.OwnerDocument, assetLoader);
        var picture = SvgSceneRuntime.CreateModel(
            svgFragment,
            assetLoader,
            DrawAttributes.None,
            GetStandaloneViewport(skiaModel.Settings));
        return skiaModel.ToSKPicture(picture);
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        using var documentFontScope = PushDocumentFonts(svgFragment as SvgDocument ?? svgFragment.OwnerDocument, assetLoader);
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
    private ISKSvgJavaScriptRuntime? _javaScriptRuntime;
    private bool _isDispatchingAnimationTimelineCallbacks;
    private bool _animationTimelineChangedDuringDispatch;
    private TimeSpan? _animationTimelineCallbackTime;
    private readonly List<SvgTextSelectionRange> _textSelections = new();
    private bool _suspendTextSelectionRefresh;

    public object Sync { get; } = new();

    public SKSvgSettings Settings { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    public SKPicture? Model { get; private set; }

    public SvgDocument? SourceDocument { get; private set; }

    public SvgAnimationController? AnimationController { get; private set; }

    public SvgTextSelectionRange[] TextSelections
    {
        get
        {
            lock (Sync)
            {
                return _textSelections.ToArray();
            }
        }
    }

    public bool HasTextSelection
    {
        get
        {
            lock (Sync)
            {
                return _textSelections.Count > 0;
            }
        }
    }

    public bool HasAnimations => AnimationController?.HasAnimations == true;

    public TimeSpan AnimationTime => AnimationController?.Clock.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan AnimationMinimumRenderInterval
    {
        get => _animationMinimumRenderInterval;
        set => _animationMinimumRenderInterval = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    public bool HasPendingAnimationFrame => _pendingAnimationFrameState is not null;

    public int LastAnimationDirtyTargetCount { get; private set; }

    public enum SvgTextSelectionDirection
    {
        None,
        Forward,
        Backward
    }

    public readonly struct SvgTextSelectionRange
    {
        public SvgTextSelectionRange(string? elementId, int charnum, int nchars, IReadOnlyList<SKRect> extents)
            : this(
                elementId,
                elementAddress: null,
                textContentElement: null,
                charnum,
                nchars,
                startCharnum: charnum,
                endCharnum: GetRequestedEndCharnum(charnum, nchars),
                anchorCharnum: charnum,
                focusCharnum: GetRequestedFocusCharnum(charnum, nchars),
                direction: SvgTextSelectionDirection.Forward,
                hasCaret: false,
                caretPosition: default,
                caretExtent: default,
                extents)
        {
        }

        internal SvgTextSelectionRange(string? elementId, string? elementAddress, int charnum, int nchars, IReadOnlyList<SKRect> extents)
            : this(
                elementId,
                elementAddress,
                textContentElement: null,
                charnum,
                nchars,
                startCharnum: charnum,
                endCharnum: GetRequestedEndCharnum(charnum, nchars),
                anchorCharnum: charnum,
                focusCharnum: GetRequestedFocusCharnum(charnum, nchars),
                direction: SvgTextSelectionDirection.Forward,
                hasCaret: false,
                caretPosition: default,
                caretExtent: default,
                extents)
        {
        }

        private SvgTextSelectionRange(
            string? elementId,
            string? elementAddress,
            SvgTextBase? textContentElement,
            int charnum,
            int nchars,
            int startCharnum,
            int endCharnum,
            int anchorCharnum,
            int focusCharnum,
            SvgTextSelectionDirection direction,
            bool hasCaret,
            SKPoint caretPosition,
            SKRect caretExtent,
            IReadOnlyList<SKRect> extents)
        {
            ElementId = elementId;
            ElementAddress = elementAddress;
            TextContentElement = textContentElement;
            Charnum = charnum;
            NChars = nchars;
            StartCharnum = startCharnum;
            EndCharnum = endCharnum;
            AnchorCharnum = anchorCharnum;
            FocusCharnum = focusCharnum;
            Direction = direction;
            HasCaret = hasCaret;
            CaretPosition = caretPosition;
            CaretExtent = caretExtent;
            Extents = CopySelectionExtents(extents);
            VisualExtents = CopySelectionExtentsInVisualOrder(extents);
        }

        public string? ElementId { get; }

        public string? ElementAddress { get; }

        internal SvgTextBase? TextContentElement { get; }

        public int Charnum { get; }

        public int NChars { get; }

        public int StartCharnum { get; }

        public int EndCharnum { get; }

        public int SelectedNChars => Math.Max(0, EndCharnum - StartCharnum);

        public bool IsCollapsed => SelectedNChars == 0 && HasCaret;

        public int AnchorCharnum { get; }

        public int FocusCharnum { get; }

        public SvgTextSelectionDirection Direction { get; }

        public bool HasCaret { get; }

        public SKPoint CaretPosition { get; }

        public SKRect CaretExtent { get; }

        public IReadOnlyList<SKRect> Extents { get; }

        public IReadOnlyList<SKRect> VisualExtents { get; }

        internal static SvgTextSelectionRange Create(
            SvgTextBase textContentElement,
            int charnum,
            int nchars,
            int anchorCharnum,
            int focusCharnum,
            SvgTextSelectionDirection direction,
            SvgSceneTextCompiler.SvgTextContentMetrics metrics,
            IReadOnlyList<SKRect> extents)
        {
            var startCharnum = charnum;
            var endCharnum = GetBoundedEndCharnum(charnum, nchars, metrics.NumberOfChars);
            var hasCaret = TryGetCaretMetadata(metrics, startCharnum, endCharnum, direction, out var caretPosition, out var caretExtent);

            return new SvgTextSelectionRange(
                textContentElement.ID,
                SvgSceneCompiler.TryGetElementAddressKey(textContentElement),
                textContentElement,
                charnum,
                nchars,
                startCharnum,
                endCharnum,
                anchorCharnum,
                focusCharnum,
                direction,
                hasCaret,
                caretPosition,
                caretExtent,
                extents);
        }

        private static IReadOnlyList<SKRect> CopySelectionExtents(IReadOnlyList<SKRect> extents)
        {
            if (extents is null)
            {
                throw new ArgumentNullException(nameof(extents));
            }

            if (extents.Count == 0)
            {
                return Array.Empty<SKRect>();
            }

            var copy = new SKRect[extents.Count];
            for (var i = 0; i < extents.Count; i++)
            {
                copy[i] = extents[i];
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<SKRect> CopySelectionExtentsInVisualOrder(IReadOnlyList<SKRect> extents)
        {
            if (extents is null)
            {
                throw new ArgumentNullException(nameof(extents));
            }

            if (extents.Count == 0)
            {
                return Array.Empty<SKRect>();
            }

            var copy = new SKRect[extents.Count];
            for (var i = 0; i < extents.Count; i++)
            {
                copy[i] = extents[i];
            }

            Array.Sort(copy, CompareSelectionExtentsInVisualOrder);
            return Array.AsReadOnly(copy);
        }

        private static int CompareSelectionExtentsInVisualOrder(SKRect left, SKRect right)
        {
            if (AreSameVisualSelectionLine(left, right))
            {
                return left.Left.CompareTo(right.Left);
            }

            var topComparison = left.Top.CompareTo(right.Top);
            return topComparison != 0 ? topComparison : left.Left.CompareTo(right.Left);
        }

        private static bool AreSameVisualSelectionLine(SKRect left, SKRect right)
        {
            var verticalOverlap = Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top);
            var minHeight = Math.Min(Math.Abs(left.Height), Math.Abs(right.Height));
            return minHeight > 0f && verticalOverlap >= minHeight * 0.5f;
        }

        private static int GetRequestedEndCharnum(int charnum, int nchars)
        {
            if (nchars <= 0)
            {
                return charnum;
            }

            var requestedEnd = (long)charnum + nchars;
            return requestedEnd >= int.MaxValue ? int.MaxValue : (int)requestedEnd;
        }

        private static int GetBoundedEndCharnum(int charnum, int nchars, int numberOfChars)
        {
            if (nchars <= 0)
            {
                return charnum;
            }

            var requestedEnd = (long)charnum + nchars;
            return requestedEnd >= numberOfChars ? numberOfChars : (int)requestedEnd;
        }

        private static int GetRequestedFocusCharnum(int charnum, int nchars)
        {
            return nchars <= 0 ? charnum : GetRequestedEndCharnum(charnum, nchars) - 1;
        }

        private static bool TryGetCaretMetadata(
            SvgSceneTextCompiler.SvgTextContentMetrics metrics,
            int startCharnum,
            int endCharnum,
            SvgTextSelectionDirection direction,
            out SKPoint caretPosition,
            out SKRect caretExtent)
        {
            caretPosition = default;
            caretExtent = default;

            if (metrics.NumberOfChars == 0 ||
                startCharnum < 0 ||
                endCharnum < startCharnum ||
                endCharnum > metrics.NumberOfChars)
            {
                return false;
            }

            if (startCharnum == endCharnum)
            {
                return metrics.TryGetCaretMetadata(startCharnum, out caretPosition, out caretExtent);
            }

            if (direction == SvgTextSelectionDirection.Backward)
            {
                caretPosition = metrics.GetStartPositionOfChar(startCharnum);
                caretExtent = metrics.GetExtentOfChar(startCharnum);
                return true;
            }

            var focusCharnum = endCharnum - 1;
            caretPosition = metrics.GetEndPositionOfChar(focusCharnum);
            caretExtent = metrics.GetExtentOfChar(focusCharnum);
            return true;
        }
    }

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

            using var documentFontScope = PushDocumentFonts(SourceDocument, AssetLoader);
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

        using var documentFontScope = PushDocumentFonts(SourceDocument, AssetLoader);
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
                clone.ReplaceAnimationController(new SvgAnimationController(sourceDocumentClone, AnimationController?.WallclockTimeOrigin));
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

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadSvgInternal(stream, parameters, null);
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        return LoadSvgInternal(stream, parameters, baseUri);
    }

    public SkiaSharp.SKPicture? Load(string? path, SvgParameters? parameters = null)
    {
        return LoadSvgPath(path, parameters);
    }

    public SkiaSharp.SKPicture? Load(string path) => Load(path, null);

    public SkiaSharp.SKPicture? Load(XmlReader reader)
    {
        return LoadSvgReader(reader);
    }

    public SkiaSharp.SKPicture? LoadVectorDrawable(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadInternal(stream, parameters, null, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    public SkiaSharp.SKPicture? LoadVectorDrawable(string? path, SvgParameters? parameters = null)
    {
        return LoadPath(path, parameters, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    public SkiaSharp.SKPicture? LoadVectorDrawable(XmlReader reader)
    {
        return LoadReader(reader, SourceFormat.VectorDrawable, SvgService.OpenVectorDrawable);
    }

    private SkiaSharp.SKPicture? LoadInternal(
        System.IO.Stream stream,
        SvgParameters? parameters,
        Uri? baseUri,
        SourceFormat sourceFormat,
        Func<System.IO.Stream, SvgParameters?, SvgDocument?> loader)
    {
        SvgDocument? svgDocument;
        using var systemColorScope = CreateSystemColorProviderScope();

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

    private SkiaSharp.SKPicture? LoadSvgInternal(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        SvgDocument? svgDocument;
        using var systemColorScope = CreateSystemColorProviderScope();

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

    private SkiaSharp.SKPicture? LoadSvgPath(string? path, SvgParameters? parameters)
    {
        if (path is null)
        {
            return null;
        }

        using var systemColorScope = CreateSystemColorProviderScope();
        _originalPath = path;
        _originalParameters = parameters;
        _originalBaseUri = null;
        _originalSourceFormat = SourceFormat.Svg;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(SvgService.Open(path, parameters, Settings.EnableJavaScript));
    }

    private SkiaSharp.SKPicture? LoadSvgReader(XmlReader reader)
    {
        using var systemColorScope = CreateSystemColorProviderScope();
        _originalPath = null;
        _originalParameters = null;
        _originalBaseUri = null;
        _originalSourceFormat = SourceFormat.Svg;
        _originalStream?.Dispose();
        _originalStream = null;

        return LoadSvgDocument(SvgService.Open(reader, Settings.EnableJavaScript));
    }

    private IDisposable? CreateSystemColorProviderScope()
    {
        return Settings.SystemColorProvider is { } provider
            ? SvgSystemColorResolver.PushProvider(provider)
            : null;
    }

    private static IDisposable? PushDocumentFonts(SvgDocument? document, ISvgAssetLoader assetLoader)
    {
        if (document is not null &&
            assetLoader is ISvgDocumentFontLoader fontLoader)
        {
            return fontLoader.PushDocumentFonts(document);
        }

        return null;
    }

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

    private SkiaSharp.SKPicture? LoadSvgDocument(SvgDocument? svgDocument, Uri? baseUri = null)
    {
        if (svgDocument is null)
        {
            return null;
        }

        if (AssetLoader is ISvgDocumentFontLoader fontLoader)
        {
            fontLoader.ClearDocumentFonts();
        }

        if (baseUri is { })
        {
            svgDocument.BaseUri = baseUri;
        }

        SourceDocument = svgDocument;
        ClearTextSelectionCore();
        ClearAnimationRenderState();
        ReplaceAnimationController(null);
        InvalidateRetainedSceneGraph();
        _suspendTextSelectionRefresh = true;
        try
        {
            InitializeJavaScriptRuntime(svgDocument);
        }
        finally
        {
            _suspendTextSelectionRefresh = false;
        }

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

    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        using var systemColorScope = CreateSystemColorProviderScope();
        var svgDocument = SvgService.FromSvg(svg, Settings.EnableJavaScript);
        return LoadSvgDocument(svgDocument);
    }

    public SkiaSharp.SKPicture? FromVectorDrawable(string xml)
    {
        var svgDocument = SvgService.FromVectorDrawable(xml);
        return LoadSvgDocument(svgDocument);
    }

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

    public bool BeginAnimationElement(string animationElementId)
    {
        return BeginAnimationElement(animationElementId, TimeSpan.Zero);
    }

    public bool BeginAnimationElement(string animationElementId, TimeSpan offset)
    {
        return TryGetAnimationElement(animationElementId, out var animation) &&
               BeginAnimationElement(animation, offset);
    }

    public bool BeginAnimationElement(SvgAnimationElement animation)
    {
        return BeginAnimationElement(animation, TimeSpan.Zero);
    }

    public bool BeginAnimationElement(SvgAnimationElement? animation, TimeSpan offset)
    {
        return ScheduleAnimationElement(animation, offset, begin: true);
    }

    public bool EndAnimationElement(string animationElementId)
    {
        return EndAnimationElement(animationElementId, TimeSpan.Zero);
    }

    public bool EndAnimationElement(string animationElementId, TimeSpan offset)
    {
        return TryGetAnimationElement(animationElementId, out var animation) &&
               EndAnimationElement(animation, offset);
    }

    public bool EndAnimationElement(SvgAnimationElement animation)
    {
        return EndAnimationElement(animation, TimeSpan.Zero);
    }

    public bool EndAnimationElement(SvgAnimationElement? animation, TimeSpan offset)
    {
        return ScheduleAnimationElement(animation, offset, begin: false);
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

    public bool NotifyPointerEvent(SvgElement? element, SvgPointerEventType eventType, TimeSpan presentationTime)
    {
        if (!RecordAnimationPointerEvent(element, eventType, presentationTime))
        {
            return false;
        }

        RefreshCurrentAnimationFrame(bypassThrottle: true);
        return true;
    }

    public bool NotifyAccessKey(string? accessKey)
    {
        if (!RecordAnimationAccessKey(accessKey))
        {
            return false;
        }

        RefreshCurrentAnimationFrame(bypassThrottle: true);
        return true;
    }

    public bool NotifyAccessKey(string? accessKey, TimeSpan presentationTime)
    {
        if (!RecordAnimationAccessKey(accessKey, presentationTime))
        {
            return false;
        }

        RefreshCurrentAnimationFrame(bypassThrottle: true);
        return true;
    }

    private bool TryGetAnimationElement(string animationElementId, out SvgAnimationElement animation)
    {
        animation = null!;
        if (string.IsNullOrWhiteSpace(animationElementId) ||
            SourceDocument?.GetElementById(animationElementId) is not SvgAnimationElement resolvedAnimation)
        {
            return false;
        }

        animation = resolvedAnimation;
        return true;
    }

    private bool ScheduleAnimationElement(SvgAnimationElement? animation, TimeSpan offset, bool begin)
    {
        if (animation is null || AnimationController is null)
        {
            return false;
        }

        var scheduled = begin
            ? AnimationController.BeginElement(animation, offset)
            : AnimationController.EndElement(animation, offset);
        if (scheduled)
        {
            NotifyAnimationTimelineMutation();
        }

        return scheduled;
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
        SKPicture? model;
        lock (Sync)
        {
            model = Model;
        }

        if (ContainsNonScalingStroke(model) &&
            TrySaveModelImage(model, stream, background, format, quality, scaleX, scaleY))
        {
            return true;
        }

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

    private bool TrySaveModelImage(SKPicture? model, System.IO.Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format, int quality, float scaleX, float scaleY)
    {
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
        canvas.Save();
        canvas.Scale(scaleX, scaleY);
        SkiaModel.Draw(model, canvas);
        canvas.Restore();

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        if (data is null)
        {
            return false;
        }

        data.SaveTo(stream);
        return true;
    }

    private static bool ContainsNonScalingStroke(SKPicture? model)
    {
        if (model?.Commands is not { } commands)
        {
            return false;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i])
            {
                case DrawPathCanvasCommand { Paint: { IsStrokeNonScaling: true, Style: SKPaintStyle.Stroke } }:
                    return true;

                case DrawPictureCanvasCommand { Picture: { } picture }
                    when ContainsNonScalingStroke(picture):
                    return true;
            }
        }

        return false;
    }

    public void Draw(SkiaSharp.SKCanvas canvas)
    {
        BeginDraw();
        try
        {
            canvas.Save();
            ApplyViewerTransform(canvas);
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
                SKPicture? model;
                lock (Sync)
                {
                    model = Model;
                }

                if (ContainsNonScalingStroke(model))
                {
                    if (model is not null)
                    {
                        SkiaModel.Draw(model, canvas);
                    }
                }
                else
                {
                    var picture = Picture;
                    if (picture is null)
                    {
                        canvas.Restore();
                        return;
                    }

                    canvas.DrawPicture(picture);
                }
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
        if (AssetLoader is ISvgDocumentFontLoader fontLoader)
        {
            fontLoader.ClearDocumentFonts();
        }

        SourceDocument = null;
        _javaScriptRuntime = null;
        ClearAnimationRenderState();
        ClearTextSelectionCore();

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
        using var documentFontScope = PushDocumentFonts(sceneDocument.SourceDocument, sceneDocument.AssetLoader);
        var model = sceneDocument.CreateModel();
        if (model is null)
        {
            return false;
        }

        ApplyTextSelectionRendering(model);

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

    private void ApplyTextSelectionRendering(SKPicture model)
    {
        if (!Settings.EnableTextSelectionRendering)
        {
            return;
        }

        RefreshTextSelectionMetrics();

        SvgTextSelectionRange[] selections;
        lock (Sync)
        {
            if (_textSelections.Count == 0)
            {
                return;
            }

            selections = _textSelections.ToArray();
        }

        for (var i = 0; i < selections.Length; i++)
        {
            var selection = selections[i];
            if (!HasTextSelectionTarget(selection) ||
                !TryCreateTextSelectionCommand(selection, out var command))
            {
                continue;
            }

            _ = InsertTextSelectionCommand(model, selection, command);
        }
    }

    private static bool HasTextSelectionTarget(SvgTextSelectionRange selection)
    {
        return !string.IsNullOrWhiteSpace(selection.ElementAddress) ||
               !string.IsNullOrWhiteSpace(selection.ElementId);
    }

    private bool TryCreateTextSelectionCommand(SvgTextSelectionRange selection, out DrawPathCanvasCommand command)
    {
        command = default!;
        if (selection.VisualExtents.Count == 0)
        {
            return false;
        }

        var path = new SKPath();
        for (var i = 0; i < selection.VisualExtents.Count; i++)
        {
            var extent = selection.VisualExtents[i];
            if (!IsRenderableSelectionExtent(extent))
            {
                continue;
            }

            path.AddRect(extent);
        }

        if (path.IsEmpty)
        {
            return false;
        }

        var color = Settings.TextSelectionColor;
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
            Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha)
        };

        command = new DrawPathCanvasCommand(path, paint)
        {
            SourceElementId = selection.ElementId,
            SourceElementAddress = selection.ElementAddress,
            SourceElementTypeName = "SvgTextSelection"
        };
        return true;
    }

    private static bool IsRenderableSelectionExtent(SKRect extent)
    {
        return IsFinite(extent.Left) &&
               IsFinite(extent.Top) &&
               IsFinite(extent.Right) &&
               IsFinite(extent.Bottom) &&
               extent.Width > 0f &&
               extent.Height > 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool InsertTextSelectionCommand(SKPicture picture, SvgTextSelectionRange selection, DrawPathCanvasCommand command)
    {
        var commands = picture.Commands;
        if (commands is null)
        {
            return false;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            var current = commands[i];
            if (current is DrawPictureCanvasCommand { Picture: { } nestedPicture } drawPictureCommand &&
                PictureContainsSelectionTarget(nestedPicture, selection))
            {
                var nestedClone = nestedPicture.DeepClone();
                if (InsertTextSelectionCommand(nestedClone, selection, command))
                {
                    commands[i] = CloneDrawPictureCommandWithPicture(drawPictureCommand, nestedClone);
                    return true;
                }
            }

            if (!IsTextDrawingCommand(current) ||
                !IsSelectionTargetCommand(current, selection))
            {
                continue;
            }

            commands.Insert(i, command);
            return true;
        }

        return false;
    }

    private static bool PictureContainsSelectionTarget(SKPicture picture, SvgTextSelectionRange selection)
    {
        var commands = picture.Commands;
        if (commands is null)
        {
            return false;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            var current = commands[i];
            if (IsTextDrawingCommand(current) &&
                IsSelectionTargetCommand(current, selection))
            {
                return true;
            }

            if (current is DrawPictureCanvasCommand { Picture: { } nestedPicture } &&
                PictureContainsSelectionTarget(nestedPicture, selection))
            {
                return true;
            }
        }

        return false;
    }

    private static DrawPictureCanvasCommand CloneDrawPictureCommandWithPicture(DrawPictureCanvasCommand source, SKPicture picture)
    {
        return new DrawPictureCanvasCommand(picture)
        {
            SourceElementId = source.SourceElementId,
            SourceElementAddress = source.SourceElementAddress,
            SourceElementTypeName = source.SourceElementTypeName
        };
    }

    private static bool IsTextDrawingCommand(CanvasCommand command)
    {
        return command is DrawPathCanvasCommand or
            DrawTextCanvasCommand or
            DrawTextBlobCanvasCommand or
            DrawTextOnPathCanvasCommand;
    }

    private static bool IsSelectionTargetCommand(CanvasCommand command, SvgTextSelectionRange selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.ElementAddress) &&
            IsSameOrDescendantSelectionAddress(command.SourceElementAddress, selection.ElementAddress!))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(selection.ElementId) &&
               string.Equals(command.SourceElementId, selection.ElementId, StringComparison.Ordinal);
    }

    private static bool IsSameOrDescendantSelectionAddress(string? candidate, string ancestor)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return string.Equals(candidate, ancestor, StringComparison.Ordinal) ||
               candidate!.StartsWith(ancestor + "/", StringComparison.Ordinal);
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
            runtime.SetAnimationHost(controller is null ? null : new SvgJavaScriptAnimationHostAdapter(this, controller));
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
        return RecordAnimationPointerEvent(element, hitNode: null, eventType, presentationTime: null);
    }

    internal bool RecordAnimationPointerEvent(SvgElement? element, SvgPointerEventType eventType, TimeSpan presentationTime)
    {
        return RecordAnimationPointerEvent(element, hitNode: null, eventType, presentationTime);
    }

    internal bool RecordAnimationPointerEvent(SvgElement? element, SvgSceneNode? hitNode, SvgPointerEventType eventType)
    {
        return RecordAnimationPointerEvent(element, hitNode, eventType, presentationTime: null);
    }

    private bool RecordAnimationPointerEvent(SvgElement? element, SvgSceneNode? hitNode, SvgPointerEventType eventType, TimeSpan? presentationTime)
    {
        if (AnimationController is null)
        {
            return false;
        }

        var recorded = false;
        var recordedAddressKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var eventElement in EnumerateAnimationEventElements(element, hitNode))
        {
            var normalizedElement = NormalizeAnimationEventElement(eventElement);
            if (normalizedElement is null ||
                !TryMarkAnimationEventElement(recordedAddressKeys, normalizedElement))
            {
                continue;
            }

            recorded |= presentationTime.HasValue
                ? AnimationController.RecordPointerEvent(normalizedElement, eventType, presentationTime.Value)
                : AnimationController.RecordPointerEvent(normalizedElement, eventType);
        }

        return recorded;
    }

    private static bool TryMarkAnimationEventElement(HashSet<string> recordedAddressKeys, SvgElement element)
    {
        var key = SvgElementAddress.Create(element).Key;
        return recordedAddressKeys.Add(key);
    }

    private static IEnumerable<SvgElement> EnumerateAnimationEventElements(SvgElement? targetElement, SvgSceneNode? hitNode)
    {
        if (targetElement is not null)
        {
            foreach (var instanceElement in EnumerateUseInstanceAnimationEventElements(targetElement, hitNode))
            {
                yield return instanceElement;
            }

            yield return targetElement;
        }
    }

    private static IEnumerable<SvgElement> EnumerateUseInstanceAnimationEventElements(SvgElement targetElement, SvgSceneNode? hitNode)
    {
        if (targetElement is not SvgUse ||
            hitNode is null ||
            !ReferenceEquals(hitNode.HitTestTargetElement, targetElement))
        {
            yield break;
        }

        for (var current = hitNode; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current.Element, targetElement))
            {
                yield break;
            }

            if (!ReferenceEquals(current.HitTestTargetElement, targetElement))
            {
                yield break;
            }

            if (current.Element is SvgElement instanceElement)
            {
                yield return instanceElement;
            }
        }
    }

    internal bool RecordAnimationAccessKey(string? accessKey)
    {
        return AnimationController?.RecordAccessKey(accessKey) == true;
    }

    internal bool RecordAnimationAccessKey(string? accessKey, TimeSpan presentationTime)
    {
        return AnimationController?.RecordAccessKey(accessKey, presentationTime) == true;
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

        var factory = Settings.JavaScriptRuntimeFactory ?? SKSvgSettings.DefaultJavaScriptRuntimeFactory;
        if (factory is null)
        {
            throw new InvalidOperationException(
                "SVG JavaScript support is not registered. Reference the Svg.Skia.JavaScript package and call SKSvgJavaScriptRuntime.Register() before loading JavaScript-enabled SVG content.");
        }

        var runtime = factory.Create(svgDocument, CreateJavaScriptSettings());
        _javaScriptRuntime = runtime;
        if (runtime is ISKSvgJavaScriptViewerRuntime viewerRuntime)
        {
            viewerRuntime.SetViewerHost(this);
        }

        runtime.SetTextContentHost(new SvgJavaScriptTextContentHostAdapter(this));
        if (AnimationController is { } controller)
        {
            runtime.SetAnimationHost(new SvgJavaScriptAnimationHostAdapter(this, controller));
        }

        if (executeDocumentScripts)
        {
            runtime.ExecuteDocumentScripts(dispatchLoadEvent);
        }
    }

    internal SKSvgJavaScriptEventResult DispatchJavaScriptEvent(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgPointerInput input)
    {
        object? eventFacade = null;
        return DispatchJavaScriptEvent(
            element,
            targetElement,
            relatedElement,
            eventType,
            attributeName,
            input,
            ref eventFacade);
    }

    internal SKSvgJavaScriptEventResult DispatchJavaScriptEvent(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgPointerInput input,
        ref object? eventFacade)
    {
        var runtime = _javaScriptRuntime;
        if (runtime is null || SourceDocument is null)
        {
            return SKSvgJavaScriptEventResult.NotExecuted;
        }

        var handlerElement = NormalizeJavaScriptEventElement(element);
        var sourceTargetElement = NormalizeJavaScriptEventElement(targetElement);
        var sourceRelatedElement = relatedElement is null ? null : NormalizeJavaScriptEventElement(relatedElement);
        var hasUseInstanceTarget = TryResolveJavaScriptUseInstanceTarget(
            runtime,
            sourceTargetElement,
            input.PicturePoint,
            out var resolvedTargetNode,
            out var correspondingElement);
        var resolvedRelatedTargetNode = sourceRelatedElement is null ? null : runtime.GetElement(sourceRelatedElement);
        eventFacade ??= runtime.CreateEvent(
            eventType,
            resolvedTargetNode,
            resolvedRelatedTargetNode,
            CreateJavaScriptEventInput(input));

        var mutationVersion = runtime.MutationVersion;
        var result = ExecuteJavaScriptEventHandlerAndListeners(
            runtime,
            handlerElement,
            sourceTargetElement,
            correspondingElement,
            hasUseInstanceTarget,
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

    private static SKSvgJavaScriptEventResult ExecuteJavaScriptEventHandlerAndListeners(
        ISKSvgJavaScriptRuntime runtime,
        SvgElement handlerElement,
        SvgElement targetElement,
        SvgElement? correspondingElement,
        bool hasUseInstanceTarget,
        object eventFacade,
        string eventType,
        string attributeName)
    {
        if (!hasUseInstanceTarget ||
            correspondingElement is null ||
            !ReferenceEquals(handlerElement, targetElement))
        {
            return runtime.ExecuteEventHandlerAndListeners(
                handlerElement,
                eventFacade,
                eventType,
                attributeName);
        }

        var correspondingResult = runtime.ExecuteEventHandlerAndListeners(
            correspondingElement,
            eventFacade,
            eventType,
            attributeName);

        if (correspondingResult.CancelBubble)
        {
            return correspondingResult;
        }

        var useResult = runtime.ExecuteEventHandlerAndListeners(
            handlerElement,
            eventFacade,
            eventType,
            attributeName);

        return CombineJavaScriptEventResults(correspondingResult, useResult);
    }

    private static SKSvgJavaScriptEventResult CombineJavaScriptEventResults(
        SKSvgJavaScriptEventResult first,
        SKSvgJavaScriptEventResult second)
    {
        if (!first.Executed)
        {
            return second;
        }

        if (!second.Executed)
        {
            return first;
        }

        return new SKSvgJavaScriptEventResult(
            executed: true,
            mutated: first.Mutated || second.Mutated,
            cancelBubble: first.CancelBubble || second.CancelBubble,
            defaultPrevented: first.DefaultPrevented || second.DefaultPrevented);
    }

    private bool TryResolveJavaScriptUseInstanceTarget(
        ISKSvgJavaScriptRuntime runtime,
        SvgElement targetElement,
        SKPoint picturePoint,
        out object targetNode,
        out SvgElement? correspondingElement)
    {
        if (targetElement is SvgUse use &&
            HitTestTopmostSceneNode(picturePoint) is { } hitNode)
        {
            var hitTargetElement = hitNode.HitTestTargetElement is null
                ? null
                : NormalizeJavaScriptEventElement(hitNode.HitTestTargetElement);
            var hitElement = hitNode.Element is SvgElement element
                ? NormalizeJavaScriptEventElement(element)
                : null;

            if (ReferenceEquals(hitTargetElement, targetElement) &&
                hitElement is not null &&
                !ReferenceEquals(hitElement, targetElement))
            {
                var instance = runtime.FindUseInstance(use, hitElement);
                if (instance is not null)
                {
                    correspondingElement = hitElement;
                    targetNode = instance;
                    return true;
                }
            }
        }

        correspondingElement = null;
        targetNode = runtime.GetElement(targetElement);
        return false;
    }

    private SvgElement NormalizeJavaScriptEventElement(SvgElement element)
    {
        return NormalizeSourceDocumentElement(element);
    }

    private SvgElement? NormalizeAnimationEventElement(SvgElement? element)
    {
        return element is null ? null : NormalizeSourceDocumentElement(element);
    }

    private SvgElement NormalizeSourceDocumentElement(SvgElement element)
    {
        if (SourceDocument is null ||
            ReferenceEquals(element.OwnerDocument, SourceDocument))
        {
            return element;
        }

        var resolved = SvgElementAddress.Create(element).Resolve(SourceDocument);
        return resolved ?? element;
    }

    private SKSvgJavaScriptRuntimeSettings CreateJavaScriptSettings()
    {
        return new SKSvgJavaScriptRuntimeSettings
        {
            EnableExternalJavaScript = Settings.EnableExternalJavaScript,
            TimeoutMilliseconds = Settings.JavaScriptTimeoutMilliseconds,
            MaxStatements = Settings.JavaScriptMaxStatements,
            ThrowOnError = Settings.ThrowOnJavaScriptError
        };
    }

    private static SKSvgJavaScriptEventInput CreateJavaScriptEventInput(SvgPointerInput input)
    {
        return new SKSvgJavaScriptEventInput(
            input.PicturePoint.X,
            input.PicturePoint.Y,
            ToJavaScriptMouseButton(input.Button),
            input.ClickCount,
            input.WheelDelta,
            input.AltKey,
            input.ShiftKey,
            input.CtrlKey);
    }

    private static SKSvgJavaScriptMouseButton ToJavaScriptMouseButton(SvgMouseButton button)
    {
        return button switch
        {
            SvgMouseButton.Left => SKSvgJavaScriptMouseButton.Left,
            SvgMouseButton.Middle => SKSvgJavaScriptMouseButton.Middle,
            SvgMouseButton.Right => SKSvgJavaScriptMouseButton.Right,
            SvgMouseButton.XButton1 => SKSvgJavaScriptMouseButton.XButton1,
            SvgMouseButton.XButton2 => SKSvgJavaScriptMouseButton.XButton2,
            _ => SKSvgJavaScriptMouseButton.None
        };
    }

    private sealed class SvgJavaScriptAnimationHostAdapter : ISKSvgJavaScriptAnimationHost
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

    private sealed class SvgJavaScriptTextContentHostAdapter : ISKSvgJavaScriptTextContentHost, ISKSvgJavaScriptTextSelectionHost
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

        public SKPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            return GetMetrics(textContentElement).GetStartPositionOfChar(charnum);
        }

        public SKPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            return GetMetrics(textContentElement).GetEndPositionOfChar(charnum);
        }

        public SKRect GetExtentOfChar(SvgTextBase textContentElement, int charnum)
        {
            return GetMetrics(textContentElement).GetExtentOfChar(charnum);
        }

        public double GetRotationOfChar(SvgTextBase textContentElement, int charnum)
        {
            return GetMetrics(textContentElement).GetRotationOfChar(charnum);
        }

        public int GetCharNumAtPosition(SvgTextBase textContentElement, SKPoint point)
        {
            return GetMetrics(textContentElement).GetCharNumAtPosition(point);
        }

        public void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars)
        {
            var metrics = GetMetrics(textContentElement);
            _ = metrics.GetSubStringLength(charnum, nchars);
            _owner.SetTextSelection(textContentElement, charnum, nchars, metrics);
        }

        public bool TryBeginTextSelection(SvgTextBase textContentElement, int anchorCharnum)
        {
            return _owner.TryBeginTextSelection(textContentElement, anchorCharnum);
        }

        public bool TryExtendTextSelection(SvgTextBase textContentElement, int focusCharnum)
        {
            return _owner.TryExtendTextSelection(textContentElement, focusCharnum);
        }

        public bool TrySelectTextRange(SvgTextBase textContentElement, int anchorCharnum, int focusCharnum)
        {
            return _owner.TrySelectTextRange(textContentElement, anchorCharnum, focusCharnum);
        }

        public void ClearTextSelection()
        {
            _owner.ClearTextSelection();
        }

        public bool TryGetTextSelection(SvgTextBase? textContentElement, out SvgTextSelectionRange selection)
        {
            return textContentElement is null
                ? _owner.TryGetTextSelection(out selection)
                : _owner.TryGetTextSelection(textContentElement, out selection);
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

    private void SetTextSelection(
        SvgTextBase textContentElement,
        int charnum,
        int nchars,
        SvgSceneTextCompiler.SvgTextContentMetrics metrics)
    {
        SetTextSelection(
            textContentElement,
            charnum,
            nchars,
            anchorCharnum: charnum,
            focusCharnum: nchars <= 0 ? charnum : GetBoundedFocusCharnum(charnum, nchars, metrics.NumberOfChars),
            direction: SvgTextSelectionDirection.Forward,
            metrics);
    }

    private void SetTextSelection(
        SvgTextBase textContentElement,
        int charnum,
        int nchars,
        int anchorCharnum,
        int focusCharnum,
        SvgTextSelectionDirection direction,
        SvgSceneTextCompiler.SvgTextContentMetrics metrics)
    {
        var shouldRefresh = false;
        var extents = nchars > 0 ? metrics.GetSelectionExtents(charnum, nchars) : Array.Empty<SKRect>();
        lock (Sync)
        {
            _textSelections.Clear();
            if (nchars > 0 || direction == SvgTextSelectionDirection.None)
            {
                _textSelections.Add(SvgTextSelectionRange.Create(
                    textContentElement,
                    charnum,
                    nchars,
                    anchorCharnum,
                    focusCharnum,
                    direction,
                    metrics,
                    extents));
            }

            shouldRefresh = !_suspendTextSelectionRefresh && Model is not null;
        }

        if (shouldRefresh)
        {
            RefreshTextSelectionRendering();
        }
    }

    public bool TryGetTextSelection(out SvgTextSelectionRange selection)
    {
        lock (Sync)
        {
            if (_textSelections.Count == 0)
            {
                selection = default;
                return false;
            }

            selection = _textSelections[0];
            return true;
        }
    }

    public bool TryGetTextSelection(SvgTextBase textContentElement, out SvgTextSelectionRange selection)
    {
        if (textContentElement is null)
        {
            selection = default;
            return false;
        }

        lock (Sync)
        {
            for (var i = 0; i < _textSelections.Count; i++)
            {
                if (IsTextSelectionForElement(_textSelections[i], textContentElement))
                {
                    selection = _textSelections[i];
                    return true;
                }
            }
        }

        selection = default;
        return false;
    }

    /// <summary>
    /// Selects a logical character range in a text content element and refreshes retained static selection rendering.
    /// </summary>
    /// <param name="textContentElement">The text content element to select from.</param>
    /// <param name="charnum">The zero-based character index where the range starts.</param>
    /// <param name="nchars">The requested number of characters in the range. Zero clears the active selection.</param>
    public void SelectTextSubString(SvgTextBase textContentElement, int charnum, int nchars)
    {
        if (textContentElement is null)
        {
            throw new ArgumentNullException(nameof(textContentElement));
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        _ = metrics.GetSubStringLength(charnum, nchars);
        SetTextSelection(textContentElement, charnum, nchars, metrics);
    }

    public bool TrySelectTextSubString(SvgTextBase textContentElement, int charnum, int nchars)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        try
        {
            _ = metrics.GetSubStringLength(charnum, nchars);
            SetTextSelection(textContentElement, charnum, nchars, metrics);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public bool TryBeginTextSelection(SvgTextBase textContentElement, int anchorCharnum)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        if (!metrics.TryGetCaretMetadata(anchorCharnum, out _, out _))
        {
            return false;
        }

        SetTextSelection(
            textContentElement,
            anchorCharnum,
            nchars: 0,
            anchorCharnum,
            focusCharnum: anchorCharnum,
            direction: SvgTextSelectionDirection.None,
            metrics);
        return true;
    }

    public bool TryBeginTextSelection(SvgTextBase textContentElement, SKPoint anchorPoint)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        var anchorCharnum = metrics.GetCharNumAtPosition(anchorPoint);
        return anchorCharnum >= 0 && TryBeginTextSelection(textContentElement, anchorCharnum);
    }

    public bool TryExtendTextSelection(SvgTextBase textContentElement, int focusCharnum)
    {
        if (textContentElement is null)
        {
            return false;
        }

        SvgTextSelectionRange currentSelection;
        lock (Sync)
        {
            if (_textSelections.Count == 0 ||
                !IsTextSelectionForElement(_textSelections[0], textContentElement))
            {
                return false;
            }

            currentSelection = _textSelections[0];
        }

        if (focusCharnum == currentSelection.AnchorCharnum)
        {
            return TryBeginTextSelection(textContentElement, currentSelection.AnchorCharnum);
        }

        return TrySelectTextRange(textContentElement, currentSelection.AnchorCharnum, focusCharnum);
    }

    public bool TryExtendTextSelection(SvgTextBase textContentElement, SKPoint focusPoint)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        var focusCharnum = metrics.GetCharNumAtPosition(focusPoint);
        return focusCharnum >= 0 && TryExtendTextSelection(textContentElement, focusCharnum);
    }

    public bool TrySelectTextRange(SvgTextBase textContentElement, int anchorCharnum, int focusCharnum)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        if (!TryCreateTextRangeSelection(
            textContentElement,
            anchorCharnum,
            focusCharnum,
            metrics,
            out var charnum,
            out var nchars,
            out var direction))
        {
            return false;
        }

        SetTextSelection(
            textContentElement,
            charnum,
            nchars,
            anchorCharnum,
            focusCharnum,
            direction,
            metrics);
        return true;
    }

    public bool TrySelectTextRange(SvgTextBase textContentElement, SKPoint anchorPoint, SKPoint focusPoint)
    {
        if (textContentElement is null)
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        var anchorCharnum = metrics.GetCharNumAtPosition(anchorPoint);
        var focusCharnum = metrics.GetCharNumAtPosition(focusPoint);
        if (anchorCharnum < 0 || focusCharnum < 0)
        {
            return false;
        }

        return TrySelectTextRange(textContentElement, anchorCharnum, focusCharnum);
    }

    public void ClearTextSelection()
    {
        var hadSelection = false;
        lock (Sync)
        {
            hadSelection = _textSelections.Count > 0;
            ClearTextSelectionCore();
        }

        if (hadSelection && TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            _ = RenderRetainedSceneDocument(sceneDocument);
        }
    }

    private void ClearTextSelectionCore()
    {
        _textSelections.Clear();
    }

    private void RefreshTextSelectionMetrics()
    {
        SvgTextSelectionRange[] selections;
        lock (Sync)
        {
            if (_textSelections.Count == 0)
            {
                return;
            }

            selections = _textSelections.ToArray();
        }

        var refreshedSelections = new List<SvgTextSelectionRange>(selections.Length);
        for (var i = 0; i < selections.Length; i++)
        {
            var selection = selections[i];
            if (!TryRefreshTextSelectionMetrics(selection, out var refreshedSelection))
            {
                continue;
            }

            refreshedSelections.Add(refreshedSelection);
        }

        lock (Sync)
        {
            _textSelections.Clear();
            _textSelections.AddRange(refreshedSelections);
        }
    }

    private bool TryRefreshTextSelectionMetrics(SvgTextSelectionRange selection, out SvgTextSelectionRange refreshedSelection)
    {
        refreshedSelection = default;
        if (!TryResolveTextSelectionElement(selection, out var textContentElement))
        {
            return false;
        }

        if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, GetStandaloneViewport(), AssetLoader, out var metrics))
        {
            metrics = SvgSceneTextCompiler.SvgTextContentMetrics.Empty;
        }

        try
        {
            if (selection.NChars > 0)
            {
                _ = metrics.GetSubStringLength(selection.Charnum, selection.NChars);
            }
            else if (!metrics.TryGetCaretMetadata(selection.Charnum, out _, out _))
            {
                return false;
            }

            refreshedSelection = SvgTextSelectionRange.Create(
                textContentElement,
                selection.Charnum,
                selection.NChars,
                selection.AnchorCharnum,
                selection.FocusCharnum,
                selection.Direction,
                metrics,
                metrics.GetSelectionExtents(selection.Charnum, selection.NChars));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool TryResolveTextSelectionElement(SvgTextSelectionRange selection, out SvgTextBase textContentElement)
    {
        if (selection.TextContentElement is { } existing)
        {
            textContentElement = existing;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(selection.ElementId) &&
            SourceDocument?.GetElementById(selection.ElementId!) is SvgTextBase textElement)
        {
            textContentElement = textElement;
            return true;
        }

        textContentElement = default!;
        return false;
    }

    private static bool IsTextSelectionForElement(SvgTextSelectionRange selection, SvgTextBase textContentElement)
    {
        if (selection.TextContentElement is { } existing)
        {
            return ReferenceEquals(existing, textContentElement);
        }

        if (!string.IsNullOrWhiteSpace(selection.ElementAddress) &&
            string.Equals(selection.ElementAddress, SvgSceneCompiler.TryGetElementAddressKey(textContentElement), StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(selection.ElementId) &&
               string.Equals(selection.ElementId, textContentElement.ID, StringComparison.Ordinal);
    }

    private static bool TryCreateTextRangeSelection(
        SvgTextBase textContentElement,
        int anchorCharnum,
        int focusCharnum,
        SvgSceneTextCompiler.SvgTextContentMetrics metrics,
        out int charnum,
        out int nchars,
        out SvgTextSelectionDirection direction)
    {
        charnum = 0;
        nchars = 0;
        direction = SvgTextSelectionDirection.None;

        if (anchorCharnum < 0 ||
            focusCharnum < 0 ||
            anchorCharnum >= metrics.NumberOfChars ||
            focusCharnum >= metrics.NumberOfChars)
        {
            return false;
        }

        direction = ResolveTextSelectionDirection(textContentElement, metrics, anchorCharnum, focusCharnum);
        charnum = Math.Min(anchorCharnum, focusCharnum);
        var endCharnum = Math.Max(anchorCharnum, focusCharnum) + 1;
        nchars = endCharnum - charnum;
        return true;
    }

    private static SvgTextSelectionDirection ResolveTextSelectionDirection(
        SvgTextBase textContentElement,
        SvgSceneTextCompiler.SvgTextContentMetrics metrics,
        int anchorCharnum,
        int focusCharnum)
    {
        if (anchorCharnum == focusCharnum)
        {
            return SvgTextSelectionDirection.None;
        }

        if (TryCompareTextSelectionVisualOrder(textContentElement, metrics, anchorCharnum, focusCharnum, out var comparison) &&
            comparison != 0)
        {
            return comparison < 0 ? SvgTextSelectionDirection.Forward : SvgTextSelectionDirection.Backward;
        }

        return anchorCharnum < focusCharnum ? SvgTextSelectionDirection.Forward : SvgTextSelectionDirection.Backward;
    }

    private static bool TryCompareTextSelectionVisualOrder(
        SvgTextBase textContentElement,
        SvgSceneTextCompiler.SvgTextContentMetrics metrics,
        int leftCharnum,
        int rightCharnum,
        out int comparison)
    {
        comparison = 0;
        var leftExtent = metrics.GetExtentOfChar(leftCharnum);
        var rightExtent = metrics.GetExtentOfChar(rightCharnum);
        if (leftExtent.IsEmpty || rightExtent.IsEmpty)
        {
            return false;
        }

        if (!AreSameVisualSelectionLine(leftExtent, rightExtent))
        {
            comparison = leftExtent.Top.CompareTo(rightExtent.Top);
            return comparison != 0;
        }

        comparison = IsRightToLeftTextSelection(textContentElement)
            ? rightExtent.Right.CompareTo(leftExtent.Right)
            : leftExtent.Left.CompareTo(rightExtent.Left);
        return true;
    }

    private static bool IsRightToLeftTextSelection(SvgTextBase textContentElement)
    {
        for (SvgElement? current = textContentElement; current is not null; current = current.Parent)
        {
            if (!current.TryGetOwnCascadedStyleValue("direction", out var direction) ||
                string.IsNullOrWhiteSpace(direction))
            {
                continue;
            }

            var normalized = direction.AsSpan().Trim();
            if (normalized.Equals("rtl".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Equals("ltr".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("initial".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return false;
    }

    private static bool AreSameVisualSelectionLine(SKRect left, SKRect right)
    {
        var verticalOverlap = Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top);
        var minHeight = Math.Min(Math.Abs(left.Height), Math.Abs(right.Height));
        return minHeight > 0f && verticalOverlap >= minHeight * 0.5f;
    }

    private static int GetBoundedFocusCharnum(int charnum, int nchars, int numberOfChars)
    {
        if (nchars <= 0)
        {
            return charnum;
        }

        var requestedEnd = (long)charnum + nchars;
        return requestedEnd >= numberOfChars ? numberOfChars - 1 : (int)requestedEnd - 1;
    }

    private void RefreshTextSelectionRendering()
    {
        if (AnimationController is { })
        {
            RefreshCurrentAnimationFrame(bypassThrottle: true);
            return;
        }

        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            _ = RenderRetainedSceneDocument(sceneDocument);
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

        using var systemColorScope = CreateSystemColorProviderScope();
        return RenderAnimationFrame(AnimationController.EvaluateFrameState(time), raiseInvalidation, bypassThrottle);
    }

    private bool RenderAnimationFrame(SvgAnimationFrameState frameState, bool raiseInvalidation, bool bypassThrottle)
    {
        if (SourceDocument is null || AnimationController is null)
        {
            return false;
        }

        using var systemColorScope = CreateSystemColorProviderScope();
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
