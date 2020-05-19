using System;

namespace Svg.Model.Avalonia
{
    internal abstract class DrawCommand : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
