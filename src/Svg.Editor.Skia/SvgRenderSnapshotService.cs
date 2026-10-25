using System;
using SkiaSharp;
using Svg.Skia;

namespace Svg.Editor.Skia;

/// <summary>Captures a complete vector frame, including Svg.Skia's cached animation layers.</summary>
public static class SvgRenderSnapshotService
{
    /// <summary>The caller owns the returned picture, which remains valid after the renderer changes or is disposed.</summary>
    public static SKPicture Capture(SKSvg renderer)
    {
        if (renderer is null)
            throw new ArgumentNullException(nameof(renderer));
        var bounds = renderer.Picture?.CullRect
            ?? throw new InvalidOperationException("The document does not contain renderable artwork.");
        using var recorder = new SKPictureRecorder();
        renderer.Draw(recorder.BeginRecording(bounds));
        return recorder.EndRecording();
    }
}
