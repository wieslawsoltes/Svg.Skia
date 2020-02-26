using Xml;

namespace Svg
{
    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement, ISvgTestsAttributes, ISvgResourcesAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
