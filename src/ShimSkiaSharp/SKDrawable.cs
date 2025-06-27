// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public abstract class SKDrawable
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
}
