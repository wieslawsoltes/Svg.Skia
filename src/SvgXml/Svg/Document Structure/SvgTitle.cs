using Xml;

namespace Svg
{
    [Element("title")]
    public class SvgTitle : SvgElement, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
