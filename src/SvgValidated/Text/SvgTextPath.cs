using System;

namespace SvgValidated
{
    public class SvgTextPath : SvgTextBase
    {
        public SvgUnit StartOffset { get; set; }
        public SvgTextPathMethod Method { get; set; }
        public SvgTextPathSpacing Spacing { get; set; }
        public Uri ReferencedPath { get; set; }
    }
}
