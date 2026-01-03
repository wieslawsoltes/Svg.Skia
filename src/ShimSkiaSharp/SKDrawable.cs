// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public abstract class SKDrawable : ICloneable, IDeepCloneable<SKDrawable>
{
    public SKRect Bounds => OnGetBounds();

    public SKPicture Snapshot() => Snapshot(OnGetBounds());

    public SKPicture Snapshot(SKRect bounds)
    {
        var skPictureRecorder = new SKPictureRecorder();
        var skCanvas = skPictureRecorder.BeginRecording(bounds);
        OnDraw(skCanvas);
        return skPictureRecorder.EndRecording();
    }

    protected virtual void OnDraw(SKCanvas canvas)
    {
    }

    protected virtual SKRect OnGetBounds() => SKRect.Empty;

    public abstract SKDrawable Clone();

    public virtual SKDrawable DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
