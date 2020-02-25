using Xml;

namespace Svg
{
    [Element("stop")]
    public class SvgGradientStop : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
