using System;
using Xml;

namespace Svg
{
    public interface ISvgCommonAttributes : IElement
    {
        string? Id { get; set; }
        string? Base { get; set; }
        string? Lang { get; set; }
        string? Space { get; set; }
    }
}
