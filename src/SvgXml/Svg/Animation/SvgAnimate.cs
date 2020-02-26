using Xml;

namespace Svg
{
    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
