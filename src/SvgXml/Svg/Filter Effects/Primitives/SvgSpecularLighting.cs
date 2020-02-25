using Xml;

namespace Svg.FilterEffects
{
    [Element("feSpecularLighting")]
    public class SvgSpecularLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
