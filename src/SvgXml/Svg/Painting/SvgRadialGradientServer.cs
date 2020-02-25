using Xml;

namespace Svg
{
    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
