using System;

namespace Svg
{
    public class SvgTextPath : SvgTextBase
    {
        public SvgUnit StartOffset { get; set; }
        public SvgTextPathMethod Method { get; set; }
        public SvgTextPathSpacing Spacing { get; set; }
        public Uri ReferencedPath { get; set; }
    }
}
