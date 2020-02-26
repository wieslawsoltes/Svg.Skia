using Xml;

namespace Svg
{
    [Element("script")]
    public class SvgScript : SvgElement, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
