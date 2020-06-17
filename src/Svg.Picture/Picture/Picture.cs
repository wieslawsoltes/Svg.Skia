using System;
using System.Collections.Generic;

namespace Svg.Picture
{
    public sealed class Picture : IDisposable
    {
        public Rect CullRect { get; set; }
        public IList<CanvasCommand>? Commands { get; set; }

        public void Dispose()
        {
        }
    }
}
