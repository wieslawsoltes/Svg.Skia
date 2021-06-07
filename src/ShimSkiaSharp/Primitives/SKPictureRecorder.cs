namespace ShimSkiaSharp.Primitives
{
    public sealed class SKPictureRecorder
    {
        public SKRect CullRect { get; set; }

        public SKCanvas? RecordingCanvas { get; set; }

        public SKCanvas BeginRecording(SKRect cullRect)
        {
            CullRect = cullRect;

            RecordingCanvas = new SKCanvas();

            return RecordingCanvas;
        }

        public SKPicture EndRecording()
        {
            var picture = new SKPicture
            {
                CullRect = CullRect,
                Commands = RecordingCanvas?.Commands
            };

            CullRect = SKRect.Empty;
            RecordingCanvas = null;

            return picture;
        }
    }
}
