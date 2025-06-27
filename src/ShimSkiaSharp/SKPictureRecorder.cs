// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

namespace ShimSkiaSharp;

public sealed class SKPictureRecorder
{
    public SKRect CullRect { get; private set; }

    public SKCanvas? RecordingCanvas { get; private set; }

    public SKCanvas BeginRecording(SKRect cullRect)
    {
        CullRect = cullRect;

        RecordingCanvas = new SKCanvas(new List<CanvasCommand>(), SKMatrix.Identity);

        return RecordingCanvas;
    }

    public SKPicture EndRecording()
    {
        var picture = new SKPicture(CullRect, RecordingCanvas?.Commands);

        CullRect = SKRect.Empty;
        RecordingCanvas = null;

        return picture;
    }
}
