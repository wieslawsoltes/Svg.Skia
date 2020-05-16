using System;

namespace Svg.Model
{
    public abstract class Drawable : IDisposable
    {
        public Rect Bounds => OnGetBounds();

        public Picture Snapshot()
        {
            var skBounds = OnGetBounds();
            using var skPictureRecorder = new PictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);
            OnDraw(skCanvas);
            return skPictureRecorder.EndRecording();
        }

        protected virtual void OnDraw(Canvas canvas)
        {
        }

        protected virtual Rect OnGetBounds()
        {
            return Rect.Empty;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
        }
    }
}
