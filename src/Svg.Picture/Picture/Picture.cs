using System;
using System.Collections.Generic;

namespace Svg.Picture
{
    public class Picture : IDisposable
    {
        public Rect CullRect;
        public IList<CanvasCommand>? Commands;

        public void Dispose()
        {
        }
    }
}
