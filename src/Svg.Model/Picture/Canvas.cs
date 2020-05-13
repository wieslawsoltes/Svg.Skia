using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class Canvas : IDisposable
    {
        public IList<PictureCommand>? Commands;

        public void Dispose()
        {
        }
    }
}
