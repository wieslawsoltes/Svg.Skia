using Xml;

namespace Svg
{
    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
