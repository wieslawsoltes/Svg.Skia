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

    public string? Cursor { get; internal set; }

    public bool CreatesBackgroundLayer { get; internal set; }

    public SKRect? BackgroundClip { get; internal set; }

    public bool IsIsolationGroup { get; internal set; }

    public string? ClipResourceKey { get; internal set; }

    public string? MaskResourceKey { get; internal set; }

    public string? FilterResourceKey { get; internal set; }

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

    public SvgSceneNode? MaskNode { get; private set; }

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

    public SKRect? Overflow { get; internal set; }

    public SKRect? Clip { get; internal set; }

    public SKRect? InnerClip { get; internal set; }

    public ClipPath? ClipPath { get; internal set; }

    public SKPaint? MaskPaint { get; internal set; }

    public SKPaint? MaskDstIn { get; internal set; }

    public SKPaint? Opacity { get; internal set; }

    public float OpacityValue { get; internal set; } = 1f;

    public SKPaint? BlendModePaint { get; internal set; }

    public SKPaint? Filter { get; internal set; }

    public SKRect? FilterClip { get; internal set; }

    public bool FilterUsesGlobalLayer { get; internal set; }

    public SKRect? FilterGlobalClip { get; internal set; }

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
        MaskNode = maskNode;
        if (maskNode is not null)
        {
            maskNode.Parent = this;
        }
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

        MaskNode = null;
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
}
