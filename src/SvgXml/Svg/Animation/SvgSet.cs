using Xml;

namespace Svg
{
    [Element("set")]
    public class SvgSet : SvgAnimationElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
