using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;

namespace Svg.Skia;

public sealed class SvgSceneNode
{
    private readonly List<SvgSceneNode> _children = new();

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

    public string? ClipResourceKey { get; internal set; }

    public string? MaskResourceKey { get; internal set; }

    public string? FilterResourceKey { get; internal set; }

    public string? CompilationRootKey { get; private set; }

    public bool IsCompilationRootBoundary { get; private set; }

    public SvgSceneCompilationStrategy CompilationStrategy { get; internal set; } = SvgSceneCompilationStrategy.DirectRetained;

    public SvgSceneNode? Parent { get; private set; }

    public IReadOnlyList<SvgSceneNode> Children => _children;

    public SvgSceneNode? MaskNode { get; private set; }

    public SKPicture? LocalModel { get; internal set; }

    public SKPath? HitTestPath { get; internal set; }

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

    public SKPaint? Filter { get; internal set; }

    public SKRect? FilterClip { get; internal set; }

    public SKPaint? Fill { get; internal set; }

    public SKPaint? Stroke { get; internal set; }

    public bool SupportsFillHitTest { get; internal set; }

    public bool SupportsStrokeHitTest { get; internal set; }

    public float StrokeWidth { get; internal set; }

    public bool IsRenderable { get; internal set; }

    public bool IsAntialias { get; internal set; }

    public bool SuppressSubtreeRendering { get; internal set; }

    public bool IsDirty { get; private set; }

    public long Version { get; private set; }

    public bool HasLocalVisuals => LocalModel?.Commands is { Count: > 0 };

    internal void AddChild(SvgSceneNode child)
    {
        child.Parent = this;
        _children.Add(child);
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
        ClipResourceKey = replacement.ClipResourceKey;
        MaskResourceKey = replacement.MaskResourceKey;
        FilterResourceKey = replacement.FilterResourceKey;
        CompilationRootKey = replacement.CompilationRootKey;
        IsCompilationRootBoundary = replacement.IsCompilationRootBoundary;
        LocalModel = replacement.LocalModel;
        HitTestPath = replacement.HitTestPath?.DeepClone();
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
        Filter = replacement.Filter;
        FilterClip = replacement.FilterClip;
        Fill = replacement.Fill;
        Stroke = replacement.Stroke;
        SupportsFillHitTest = replacement.SupportsFillHitTest;
        SupportsStrokeHitTest = replacement.SupportsStrokeHitTest;
        StrokeWidth = replacement.StrokeWidth;
        IsRenderable = replacement.IsRenderable;
        IsAntialias = replacement.IsAntialias;
        SuppressSubtreeRendering = replacement.SuppressSubtreeRendering;

        _children.Clear();
        for (var i = 0; i < replacement.Children.Count; i++)
        {
            AddChild(replacement.Children[i]);
        }

        MaskNode = null;
        SetMask(replacement.MaskNode);
        MarkDirty();
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

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].MarkSubtreeDirty();
        }

        MaskNode?.MarkSubtreeDirty();
    }

    public void ClearDirty()
    {
        IsDirty = false;

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].ClearDirty();
        }

        MaskNode?.ClearDirty();
    }
}
