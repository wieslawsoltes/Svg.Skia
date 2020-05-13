using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class PictureRecorder : IDisposable
    {
        private Canvas _recordingCanvas;
        private Rect _cullRect;

        public Canvas RecordingCanvas => _recordingCanvas;

        public PictureRecorder()
        {
            _recordingCanvas = new Canvas()
            {
                Commands = new List<PictureCommand>()
            };
        }

        public Canvas BeginRecording(Rect cullRect)
        {
            _cullRect = cullRect;
            _recordingCanvas.Commands?.Clear();
            return _recordingCanvas;
        }

        public Picture EndRecording()
        {
            return new Picture()
            {
                CullRect = _cullRect,
                Commands = _recordingCanvas.Commands
            };
        }

        public void Dispose()
        {
        }
    }
}
