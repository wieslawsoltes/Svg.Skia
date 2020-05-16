using System;

namespace Svg.Model
{
    public abstract class Drawable : IDisposable
    {
        public Picture Snapshot()
        {
            throw new NotImplementedException();
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
