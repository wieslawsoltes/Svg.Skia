using System.Collections.Generic;

namespace Svg.Model.Primitives
{
    public sealed class Picture
    {
        public Rect CullRect { get; set; }
        public IList<CanvasCommand>? Commands { get; set; }
    }
}
