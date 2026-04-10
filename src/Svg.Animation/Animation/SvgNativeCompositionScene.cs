using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShimSkiaSharp;

namespace Svg.Skia;

public sealed class SvgNativeCompositionLayer
{
    public SvgNativeCompositionLayer(
        int documentChildIndex,
        bool isAnimated,
        SKPicture? picture,
        SKPoint offset,
        SKSize size,
        float opacity,
        bool isVisible)
    {
        DocumentChildIndex = documentChildIndex;
        IsAnimated = isAnimated;
        Picture = picture;
        Offset = offset;
        Size = size;
        Opacity = opacity;
        IsVisible = isVisible;
    }

    public int DocumentChildIndex { get; }

    public bool IsAnimated { get; }

    public SKPicture? Picture { get; }

    public SKPoint Offset { get; }

    public SKSize Size { get; }

    public float Opacity { get; }

    public bool IsVisible { get; }
}

public sealed class SvgNativeCompositionScene
{
    public SvgNativeCompositionScene(SKRect sourceBounds, IReadOnlyList<SvgNativeCompositionLayer> layers)
    {
        SourceBounds = sourceBounds;
        Layers = new ReadOnlyCollection<SvgNativeCompositionLayer>(layers.ToArray());
    }

    public SKRect SourceBounds { get; }

    public IReadOnlyList<SvgNativeCompositionLayer> Layers { get; }
}

public sealed class SvgNativeCompositionFrame
{
    public SvgNativeCompositionFrame(SKRect sourceBounds, IReadOnlyList<SvgNativeCompositionLayer> layers)
    {
        SourceBounds = sourceBounds;
        Layers = new ReadOnlyCollection<SvgNativeCompositionLayer>(layers.ToArray());
    }

    public SKRect SourceBounds { get; }

    public IReadOnlyList<SvgNativeCompositionLayer> Layers { get; }
}
