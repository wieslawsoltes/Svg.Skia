using System.Collections.Generic;

namespace Svg.Model.Primitives
{
    public sealed class SKPicture
    {
        public SKRect CullRect { get; set; }
        public IList<CanvasCommand>? Commands { get; set; }
    }
}
