using Xml;

namespace Svg.FilterEffects
{
    [Element("feTile")]
    public class SvgTile : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
