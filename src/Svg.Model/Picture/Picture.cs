using System.Collections.Generic;

namespace Svg.Model
{
    public class Picture
    {
        public Rect CullRect { get; set; }
        public IList<PictureCommand>? Commands { get; set; }
    }
}
