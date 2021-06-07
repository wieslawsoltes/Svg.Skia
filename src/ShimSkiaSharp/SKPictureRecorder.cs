using System.Collections.Generic;

namespace ShimSkiaSharp
{
    public sealed class SKPictureRecorder
    {
        public SKRect CullRect { get; set; }

        public SKCanvas? RecordingCanvas { get; set; }

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
}
