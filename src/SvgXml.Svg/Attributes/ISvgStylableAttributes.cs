using System;
using Xml;

namespace Svg
{
    public interface ISvgStylableAttributes : IElement
    {
        string? Class { get; set; }
        string? Style { get; set; }
    }
}
