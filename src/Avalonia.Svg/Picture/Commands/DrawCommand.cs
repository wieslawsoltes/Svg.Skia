using System;

namespace Avalonia.Svg.Picture.Commands
{
    public abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
