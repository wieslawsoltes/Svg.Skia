using Xml;

namespace Svg
{
    [Element("desc")]
    public class SvgDescription : SvgElement, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
