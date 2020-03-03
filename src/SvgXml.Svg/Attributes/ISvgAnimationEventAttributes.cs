using System;
using Xml;

namespace Svg
{
    public interface ISvgAnimationEventAttributes : IElement
    {
        string? OnBegin { get; set; }
        string? OnEnd { get; set; }
        string? OnRepeat { get; set; }
        string? OnLoad { get; set; }
    }
}
