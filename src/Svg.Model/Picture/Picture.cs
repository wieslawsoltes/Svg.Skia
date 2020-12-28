using System;
using System.Collections.Generic;
using Svg.Model.Primitives;

namespace Svg.Model.Picture
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
