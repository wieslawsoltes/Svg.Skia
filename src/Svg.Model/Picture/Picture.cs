using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class Picture : IDisposable
    {
        public Rect CullRect;
        public IList<PictureCommand>? Commands;

        public void Dispose()
        {
        }
    }
}
