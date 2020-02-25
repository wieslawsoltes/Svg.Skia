using Xml;

namespace Svg
{
    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
