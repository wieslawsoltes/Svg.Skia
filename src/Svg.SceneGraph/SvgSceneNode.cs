using System;
using System.Collections;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public sealed class SvgSceneNode : IReadOnlyList<SvgSceneNode>
{
    private const int InlineChildCapacity = 2;

    private object? _children;
    private SvgSceneNode? _child1;
    private VisualState? _visualState;
    private ResourceKeyState? _resourceKeys;
    private EffectState? _effectState;
    private OpacityState? _opacityState;
    private SvgSceneTextCompiler.SvgTextContentMetrics? _textContentMetrics;
    private bool _hasLazyTextContentMetrics;

    internal SvgSceneNode(
        SvgSceneNodeKind kind,
        SvgElement? element,
        string? elementAddressKey,
        string elementTypeName,
        string? compilationRootKey,
        bool isCompilationRootBoundary)
    {
        Kind = kind;
        Element = element;
        ElementAddressKey = elementAddressKey;
        ElementTypeName = elementTypeName;
        ElementId = element?.ID;
        CompilationRootKey = compilationRootKey;
        IsCompilationRootBoundary = isCompilationRootBoundary;
    }

    public SvgSceneNodeKind Kind { get; private set; }

    public SvgElement? Element { get; private set; }

    public string? ElementAddressKey { get; private set; }

    public string? ElementId { get; private set; }

    public string ElementTypeName { get; private set; }

    public SvgElement? HitTestTargetElement { get; internal set; }

    public SvgPointerEvents PointerEvents { get; internal set; } = SvgPointerEvents.VisiblePainted;

    public bool IsVisible { get; internal set; } = true;

    public bool IsDisplayNone { get; internal set; }

    public string? Cursor
    {
        get => _visualState?.Cursor;
        internal set
        {
            if (value is null)
            {
                if (_visualState is not null)
                {
                    _visualState.Cursor = null;
                }

                return;
            }

            EnsureVisualState().Cursor = value;
        }
    }

    public bool CreatesBackgroundLayer
    {
        get => _visualState?.CreatesBackgroundLayer ?? false;
        internal set
        {
            if (!value)
            {
                if (_visualState is not null)
                {
                    _visualState.CreatesBackgroundLayer = false;
                }

                return;
            }

            EnsureVisualState().CreatesBackgroundLayer = true;
        }
    }

    public SKRect? BackgroundClip
    {
        get => _visualState?.BackgroundClip;
        internal set
        {
            if (value is null)
            {
                if (_visualState is not null)
                {
                    _visualState.BackgroundClip = null;
                }

                return;
            }

            EnsureVisualState().BackgroundClip = value;
        }
    }

    public bool IsIsolationGroup
    {
        get => _visualState?.IsIsolationGroup ?? false;
        internal set
        {
            if (!value)
            {
                if (_visualState is not null)
                {
                    _visualState.IsIsolationGroup = false;
                }

                return;
            }

            EnsureVisualState().IsIsolationGroup = true;
        }
    }

    public string? ClipResourceKey
    {
        get => _resourceKeys?.ClipResourceKey;
        internal set
        {
            if (value is null)
            {
                if (_resourceKeys is not null)
                {
                    _resourceKeys.ClipResourceKey = null;
                }

                return;
            }

            EnsureResourceKeys().ClipResourceKey = value;
        }
    }

    public string? MaskResourceKey
    {
        get => _resourceKeys?.MaskResourceKey;
        internal set
        {
            if (value is null)
            {
                if (_resourceKeys is not null)
                {
                    _resourceKeys.MaskResourceKey = null;
                }

                return;
            }

            EnsureResourceKeys().MaskResourceKey = value;
        }
    }

    public string? FilterResourceKey
    {
        get => _resourceKeys?.FilterResourceKey;
        internal set
        {
            if (value is null)
            {
                if (_resourceKeys is not null)
                {
                    _resourceKeys.FilterResourceKey = null;
                }

                return;
            }

            EnsureResourceKeys().FilterResourceKey = value;
        }
    }

    public string? CompilationRootKey { get; private set; }

    public bool IsCompilationRootBoundary { get; private set; }

    public SvgSceneCompilationStrategy CompilationStrategy { get; internal set; } = SvgSceneCompilationStrategy.DirectRetained;

    public SvgSceneNode? Parent { get; private set; }

    public IReadOnlyList<SvgSceneNode> Children =>
        _children is List<SvgSceneNode> children
            ? children
            : _children is null
                ? Array.Empty<SvgSceneNode>()
                : this;

    public SvgSceneNode? MaskNode => _effectState?.MaskNode;

    public SKPicture? LocalModel { get; internal set; }

    internal bool LocalModelSourceMetadataApplied { get; set; }

    internal SKPath? LocalPath { get; set; }

    internal SKPaint? LocalFill { get; set; }

    internal SKPaint? LocalStroke { get; set; }

    public SKPath? HitTestPath { get; internal set; }

    internal SvgSceneTextCompiler.SvgTextContentMetrics? TextContentMetrics
    {
        get => _textContentMetrics;
        set
        {
            _textContentMetrics = value;
            _hasLazyTextContentMetrics = false;
        }
    }

    public SKRect GeometryBounds { get; internal set; }

    public SKRect TransformedBounds { get; internal set; }

    public SKMatrix Transform { get; internal set; }

    public SKMatrix TotalTransform { get; internal set; }

    public SKRect? Overflow
    {
        get => _effectState?.Overflow;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.Overflow = null;
                }

                return;
            }

            EnsureEffectState().Overflow = value;
        }
    }

    public SKRect? Clip
    {
        get => _effectState?.Clip;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.Clip = null;
                }

                return;
            }

            EnsureEffectState().Clip = value;
        }
    }

    public SKRect? InnerClip
    {
        get => _effectState?.InnerClip;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.InnerClip = null;
                }

                return;
            }

            EnsureEffectState().InnerClip = value;
        }
    }

    public ClipPath? ClipPath
    {
        get => _effectState?.ClipPath;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.ClipPath = null;
                }

                return;
            }

            EnsureEffectState().ClipPath = value;
        }
    }

    public SKPaint? MaskPaint
    {
        get => _effectState?.MaskPaint;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.MaskPaint = null;
                }

                return;
            }

            EnsureEffectState().MaskPaint = value;
        }
    }

    public SKPaint? MaskDstIn
    {
        get => _effectState?.MaskDstIn;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.MaskDstIn = null;
                }

                return;
            }

            EnsureEffectState().MaskDstIn = value;
        }
    }

    public SKPaint? Opacity
    {
        get => _opacityState?.Opacity;
        internal set
        {
            if (value is null)
            {
                if (_opacityState is not null)
                {
                    _opacityState.Opacity = null;
                    ClearOpacityStateIfDefault();
                }

                return;
            }

            EnsureOpacityState().Opacity = value;
        }
    }

    public float OpacityValue
    {
        get => _opacityState?.OpacityValue ?? 1f;
        internal set
        {
            if (value == 1f)
            {
                if (_opacityState is not null)
                {
                    _opacityState.OpacityValue = 1f;
                    ClearOpacityStateIfDefault();
                }

                return;
            }

            EnsureOpacityState().OpacityValue = value;
        }
    }

    public SKPaint? BlendModePaint
    {
        get => _visualState?.BlendModePaint;
        internal set
        {
            if (value is null)
            {
                if (_visualState is not null)
                {
                    _visualState.BlendModePaint = null;
                }

                return;
            }

            EnsureVisualState().BlendModePaint = value;
        }
    }

    public SKPaint? Filter
    {
        get => _effectState?.Filter;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.Filter = null;
                }

                return;
            }

            EnsureEffectState().Filter = value;
        }
    }

    public SKRect? FilterClip
    {
        get => _effectState?.FilterClip;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.FilterClip = null;
                }

                return;
            }

            EnsureEffectState().FilterClip = value;
        }
    }

    public bool FilterUsesGlobalLayer
    {
        get => _effectState?.FilterUsesGlobalLayer ?? false;
        internal set
        {
            if (!value)
            {
                if (_effectState is not null)
                {
                    _effectState.FilterUsesGlobalLayer = false;
                }

                return;
            }

            EnsureEffectState().FilterUsesGlobalLayer = true;
        }
    }

    public SKRect? FilterGlobalClip
    {
        get => _effectState?.FilterGlobalClip;
        internal set
        {
            if (value is null)
            {
                if (_effectState is not null)
                {
                    _effectState.FilterGlobalClip = null;
                }

                return;
            }

            EnsureEffectState().FilterGlobalClip = value;
        }
    }

    public SKPaint? Fill { get; internal set; }

    public SKPaint? Stroke { get; internal set; }

    public bool SupportsFillHitTest { get; internal set; }

    public bool SupportsStrokeHitTest { get; internal set; }

    public float StrokeWidth { get; internal set; }

    public bool IsStrokeNonScaling { get; internal set; }

    public bool IsRenderable { get; internal set; }

    public bool IsAntialias { get; internal set; }

    public bool SuppressSubtreeRendering { get; internal set; }

    public bool IsDirty { get; private set; }

    public long Version { get; private set; }

    public bool HasLocalVisuals =>
        LocalModel?.Commands is { Count: > 0 } ||
        (LocalPath is not null && (LocalFill is not null || LocalStroke is not null));

    private VisualState EnsureVisualState()
    {
        return _visualState ??= new VisualState();
    }

    private ResourceKeyState EnsureResourceKeys()
    {
        return _resourceKeys ??= new ResourceKeyState();
    }

    private EffectState EnsureEffectState()
    {
        return _effectState ??= new EffectState();
    }

    private OpacityState EnsureOpacityState()
    {
        return _opacityState ??= new OpacityState();
    }

    private void ClearOpacityStateIfDefault()
    {
        if (_opacityState is { Opacity: null, OpacityValue: 1f })
        {
            _opacityState = null;
        }
    }

    internal void AddChild(SvgSceneNode child)
    {
        AddChild(child, expectedChildCount: 0);
    }

    internal void AddChild(SvgSceneNode child, int expectedChildCount)
    {
        child.Parent = this;
        if (_children is List<SvgSceneNode> children)
        {
            children.Add(child);
            return;
        }

        if (expectedChildCount <= InlineChildCapacity)
        {
            if (_children is null)
            {
                _children = child;
                return;
            }

            if (_child1 is null)
            {
                _child1 = child;
                return;
            }
        }

        PromoteChildrenToList(Math.Max(expectedChildCount, ChildCount + 1)).Add(child);
    }

    internal void SetMask(SvgSceneNode? maskNode)
    {
        if (maskNode is null)
        {
            if (_effectState is not null)
            {
                _effectState.MaskNode = null;
            }

            return;
        }

        EnsureEffectState().MaskNode = maskNode;
        maskNode.Parent = this;
    }

    internal void ReplaceWith(SvgSceneNode replacement)
    {
        Kind = replacement.Kind;
        Element = replacement.Element;
        ElementAddressKey = replacement.ElementAddressKey;
        ElementId = replacement.ElementId;
        ElementTypeName = replacement.ElementTypeName;
        HitTestTargetElement = replacement.HitTestTargetElement;
        PointerEvents = replacement.PointerEvents;
        IsVisible = replacement.IsVisible;
        IsDisplayNone = replacement.IsDisplayNone;
        Cursor = replacement.Cursor;
        CreatesBackgroundLayer = replacement.CreatesBackgroundLayer;
        BackgroundClip = replacement.BackgroundClip;
        IsIsolationGroup = replacement.IsIsolationGroup;
        ClipResourceKey = replacement.ClipResourceKey;
        MaskResourceKey = replacement.MaskResourceKey;
        FilterResourceKey = replacement.FilterResourceKey;
        CompilationRootKey = replacement.CompilationRootKey;
        IsCompilationRootBoundary = replacement.IsCompilationRootBoundary;
        LocalModel = replacement.LocalModel;
        LocalModelSourceMetadataApplied = replacement.LocalModelSourceMetadataApplied;
        LocalPath = replacement.LocalPath;
        LocalFill = replacement.LocalFill;
        LocalStroke = replacement.LocalStroke;
        HitTestPath = replacement.HitTestPath?.DeepClone();
        _textContentMetrics = replacement._textContentMetrics;
        _hasLazyTextContentMetrics = replacement._hasLazyTextContentMetrics;
        GeometryBounds = replacement.GeometryBounds;
        TransformedBounds = replacement.TransformedBounds;
        Transform = replacement.Transform;
        TotalTransform = replacement.TotalTransform;
        Overflow = replacement.Overflow;
        Clip = replacement.Clip;
        InnerClip = replacement.InnerClip;
        ClipPath = replacement.ClipPath;
        MaskPaint = replacement.MaskPaint;
        MaskDstIn = replacement.MaskDstIn;
        Opacity = replacement.Opacity;
        OpacityValue = replacement.OpacityValue;
        BlendModePaint = replacement.BlendModePaint;
        Filter = replacement.Filter;
        FilterClip = replacement.FilterClip;
        FilterUsesGlobalLayer = replacement.FilterUsesGlobalLayer;
        FilterGlobalClip = replacement.FilterGlobalClip;
        Fill = replacement.Fill;
        Stroke = replacement.Stroke;
        SupportsFillHitTest = replacement.SupportsFillHitTest;
        SupportsStrokeHitTest = replacement.SupportsStrokeHitTest;
        StrokeWidth = replacement.StrokeWidth;
        IsStrokeNonScaling = replacement.IsStrokeNonScaling;
        IsRenderable = replacement.IsRenderable;
        IsAntialias = replacement.IsAntialias;
        SuppressSubtreeRendering = replacement.SuppressSubtreeRendering;

        ClearChildren();
        for (var i = 0; i < replacement.Children.Count; i++)
        {
            AddChild(replacement.Children[i], replacement.Children.Count);
        }

        SetMask(null);
        SetMask(replacement.MaskNode);
        MarkDirty();
    }

    internal void SetLazyTextContentMetrics()
    {
        _textContentMetrics = null;
        _hasLazyTextContentMetrics = true;
    }

    internal SvgSceneTextCompiler.SvgTextContentMetrics? GetTextContentMetrics(
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        if (_textContentMetrics is not null)
        {
            return _textContentMetrics;
        }

        if (!_hasLazyTextContentMetrics ||
            Element is not SvgTextBase textContentElement)
        {
            return null;
        }

        if (SvgSceneTextCompiler.TryCreateTextContentMetrics(textContentElement, viewport, assetLoader, out var metrics) &&
            metrics.HasHitTestCells)
        {
            _textContentMetrics = metrics;
        }

        _hasLazyTextContentMetrics = false;
        return _textContentMetrics;
    }

    internal void RefreshElementIdentity(string? elementAddressKey)
    {
        ElementAddressKey = elementAddressKey;
        ElementId = Element?.ID;
        ElementTypeName = Element?.GetType().Name ?? ElementTypeName;
    }

    public void MarkDirty()
    {
        IsDirty = true;
        Version++;
    }

    public void MarkSubtreeDirty()
    {
        MarkDirty();

        for (var i = 0; i < ChildCount; i++)
        {
            GetChild(i).MarkSubtreeDirty();
        }

        MaskNode?.MarkSubtreeDirty();
    }

    public void ClearDirty()
    {
        IsDirty = false;

        for (var i = 0; i < ChildCount; i++)
        {
            GetChild(i).ClearDirty();
        }

        MaskNode?.ClearDirty();
    }

    private int ChildCount
    {
        get
        {
            return _children switch
            {
                null => 0,
                List<SvgSceneNode> children => children.Count,
                SvgSceneNode => _child1 is null ? 1 : 2,
                _ => 0
            };
        }
    }

    private SvgSceneNode GetChild(int index)
    {
        if (_children is List<SvgSceneNode> children)
        {
            return children[index];
        }

        if (_children is SvgSceneNode child0)
        {
            return index switch
            {
                0 => child0,
                1 when _child1 is not null => _child1,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void ClearChildren()
    {
        _children = null;
        _child1 = null;
    }

    private List<SvgSceneNode> PromoteChildrenToList(int capacity)
    {
        var children = new List<SvgSceneNode>(capacity);
        if (_children is SvgSceneNode child0)
        {
            children.Add(child0);

            if (_child1 is not null)
            {
                children.Add(_child1);
            }
        }

        _child1 = null;
        _children = children;
        return children;
    }

    int IReadOnlyCollection<SvgSceneNode>.Count => ChildCount;

    SvgSceneNode IReadOnlyList<SvgSceneNode>.this[int index] => GetChild(index);

    IEnumerator<SvgSceneNode> IEnumerable<SvgSceneNode>.GetEnumerator()
    {
        if (_children is List<SvgSceneNode> children)
        {
            return children.GetEnumerator();
        }

        return _children is SvgSceneNode child0
            ? new InlineChildEnumerator(child0, _child1, _child1 is null ? 1 : 2)
            : new InlineChildEnumerator(null, null, 0);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<SvgSceneNode>)this).GetEnumerator();
    }

    private sealed class InlineChildEnumerator : IEnumerator<SvgSceneNode>
    {
        private readonly SvgSceneNode? _child0;
        private readonly SvgSceneNode? _child1;
        private readonly int _count;
        private int _index = -1;

        public InlineChildEnumerator(SvgSceneNode? child0, SvgSceneNode? child1, int count)
        {
            _child0 = child0;
            _child1 = child1;
            _count = count;
        }

        public SvgSceneNode Current
        {
            get
            {
                return _index switch
                {
                    0 => _child0!,
                    1 => _child1!,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _count)
            {
                return false;
            }

            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }

    private sealed class VisualState
    {
        public string? Cursor;
        public bool CreatesBackgroundLayer;
        public SKRect? BackgroundClip;
        public bool IsIsolationGroup;
        public SKPaint? BlendModePaint;
    }

    private sealed class ResourceKeyState
    {
        public string? ClipResourceKey;
        public string? MaskResourceKey;
        public string? FilterResourceKey;
    }

    private sealed class OpacityState
    {
        public SKPaint? Opacity;
        public float OpacityValue = 1f;
    }

    private sealed class EffectState
    {
        public SvgSceneNode? MaskNode;
        public SKRect? Overflow;
        public SKRect? Clip;
        public SKRect? InnerClip;
        public ClipPath? ClipPath;
        public SKPaint? MaskPaint;
        public SKPaint? MaskDstIn;
        public SKPaint? Filter;
        public SKRect? FilterClip;
        public bool FilterUsesGlobalLayer;
        public SKRect? FilterGlobalClip;
    }
}
