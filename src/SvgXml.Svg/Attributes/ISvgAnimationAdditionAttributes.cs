using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationAdditionAttributes : IElement
    {
        string? Additive { get; set; }
        string? Accumulate { get; set; }
    }
}
