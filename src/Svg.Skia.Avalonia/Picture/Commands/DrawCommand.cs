using System;

namespace Svg.Picture.Avalonia
{
    internal abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
