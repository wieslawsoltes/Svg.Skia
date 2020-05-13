namespace Svg.Model
{
    public abstract class Drawable
    {
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
    }
}
