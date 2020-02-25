using Xml;

namespace Svg.FilterEffects
{
    [Element("feTurbulence")]
    public class SvgTurbulence : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
