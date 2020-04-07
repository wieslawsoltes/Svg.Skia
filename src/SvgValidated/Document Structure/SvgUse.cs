using System;

namespace SvgValidated
{
    public class SvgUse : SvgVisualElement
    {
        public Uri ReferencedElement { get; set; }
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
    }
}
