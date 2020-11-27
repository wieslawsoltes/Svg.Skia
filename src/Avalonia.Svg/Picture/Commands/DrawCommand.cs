using System;

namespace Avalonia.Svg.Skia
{
    public abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}