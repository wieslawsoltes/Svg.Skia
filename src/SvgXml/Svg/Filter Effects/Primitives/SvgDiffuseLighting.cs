using Xml;

namespace Svg.FilterEffects
{
    [Element("feDiffuseLighting")]
    public class SvgDiffuseLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
