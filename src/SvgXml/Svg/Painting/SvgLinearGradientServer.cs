using Xml;

namespace Svg
{
    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
