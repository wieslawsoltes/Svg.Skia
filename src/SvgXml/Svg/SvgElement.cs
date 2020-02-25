using Xml;

namespace Svg
{
    public abstract class SvgElement : Element, ISvgCoreAttributes
    {
        public virtual void Print(string indent)
        {
            if (this is ISvgCoreAttributes svgCoreAttributes)
            {
                svgCoreAttributes.PrintCoreAttributes(indent);
            }
            if (this is ISvgPresentationAttributes svgPresentationAttributes)
            {
                svgPresentationAttributes.PrintPresentationAttributes(indent);
            }
            if (this is ISvgTestsAttributes svgTestsAttributes)
            {
                svgTestsAttributes.PrintTestsAttributes(indent);
            }
            if (this is ISvgStylableAttributes svgStylableAttributes)
            {
                svgStylableAttributes.PrintStylableAttributes(indent);
            }
            if (this is ISvgTransformableAttributes svgTransformableAttributes)
            {
                svgTransformableAttributes.PrintTransformableAttributes(indent);
            }
        }
    }
}
