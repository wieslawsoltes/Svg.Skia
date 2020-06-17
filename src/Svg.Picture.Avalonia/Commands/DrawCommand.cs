using System;

namespace Svg.Picture.Avalonia
{
    public abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
