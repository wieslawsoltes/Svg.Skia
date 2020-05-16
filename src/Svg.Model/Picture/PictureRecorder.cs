using System;
using System.Runtime.InteropServices;

namespace Svg.Model
{
    public class PictureRecorder : IDisposable
    {
        public Rect CullRect;
        public Canvas? RecordingCanvas;

        public Canvas BeginRecording(Rect cullRect)
        {
            CullRect = cullRect;

            RecordingCanvas = new Canvas();

            return RecordingCanvas;
        }

        public Picture EndRecording()
        {
            var picture = new Picture()
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
