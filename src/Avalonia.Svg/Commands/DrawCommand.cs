using System;

namespace Avalonia.Svg.Commands;

public abstract class DrawCommand : IDisposable
{
    public virtual void Dispose()
    {
    }
}