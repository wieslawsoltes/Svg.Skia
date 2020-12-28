using System;

namespace Svg.Model.Primitives
{
    public abstract class Drawable : IDisposable
    {
        public Rect Bounds => OnGetBounds();

        public Picture Snapshot()
        {
            return Snapshot(OnGetBounds());
        }

        public Picture Snapshot(Rect bounds)
        {
            using var skPictureRecorder = new PictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(bounds);
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
