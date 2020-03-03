using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationTimingAttributes : IElement
    {
        string? Begin { get; set; }
        string? Dur { get; set; }
        string? End { get; set; }
        string? Min { get; set; }
        string? Max { get; set; }
        string? Restart { get; set; }
        string? RepeatCount { get; set; }
        string? RepeatDur { get; set; }
        string? Fill { get; set; }
    }
}
