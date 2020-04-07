using System;

namespace Svg
{
    public abstract partial class SvgVisualElement : SvgElement, ISvgStylable, ISvgClipable
    {
        public string Clip { get; set; }
        public Uri ClipPath { get; set; }
        public SvgClipRule ClipRule { get; set; }
        public Uri Filter { get; set; }
    }
}
