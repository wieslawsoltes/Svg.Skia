using Xml;

namespace Svg
{
    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
