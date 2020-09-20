using System;

namespace Svg.Skia.Avalonia
{
    public abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
