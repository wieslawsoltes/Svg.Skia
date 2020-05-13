using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class Path : IDisposable
    {
        public PathFillType FillType;
        public IList<PathCommand>? Commands;

        public void Dispose()
        {
        }
    }
}
