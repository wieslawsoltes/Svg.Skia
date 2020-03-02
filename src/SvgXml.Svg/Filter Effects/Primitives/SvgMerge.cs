using Xml;

namespace Svg.FilterEffects
{
    [Element("feMerge")]
    public class SvgMerge : SvgFilterPrimitive,
        ISvgCommonAttributes,
        ISvgPresentationAttributes,
        ISvgStylableAttributes
    {
    }
}
