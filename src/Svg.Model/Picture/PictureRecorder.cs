using System;
using Svg.Model.Primitives;

namespace Svg.Model.Picture
{
    public sealed class PictureRecorder : IDisposable
    {
        public Rect CullRect { get; set; }
        public Canvas? RecordingCanvas { get; set; }

        public Canvas BeginRecording(Rect cullRect)
        {
            CullRect = cullRect;

            RecordingCanvas = new Canvas();

            return RecordingCanvas;
        }

        public Picture EndRecording()
        {
            var picture = new Picture
            {
                CullRect = CullRect,
                Commands = RecordingCanvas?.Commands
            };

            CullRect = Rect.Empty;
            RecordingCanvas = null;

            return picture;
        }

        public void Dispose()
        {
        }
    }
}
