using System.Collections.Generic;

namespace Svg.Model
{
    public class Picture
    {
        public Rect CullRect;
        public IList<PictureCommand>? Commands;
    }
}
