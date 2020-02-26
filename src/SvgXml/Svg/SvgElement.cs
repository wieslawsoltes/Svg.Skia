using System;
using Xml;

namespace Svg
{
    public abstract class SvgElement : Element, ISvgCommonAttributes
    {
        public virtual void Print(Action<string> write, string indent)
        {
            if (this is ISvgCommonAttributes svgCommonAttributes)
            {
                svgCommonAttributes.PrintCommonAttributes(write, indent);
            }
            if (this is ISvgPresentationAttributes svgPresentationAttributes)
            {
                svgPresentationAttributes.PrintPresentationAttributes(write, indent);
            }
            if (this is ISvgTestsAttributes svgTestsAttributes)
            {
                svgTestsAttributes.PrintTestsAttributes(write, indent);
            }
            if (this is ISvgStylableAttributes svgStylableAttributes)
            {
                svgStylableAttributes.PrintStylableAttributes(write, indent);
            }
            if (this is ISvgResourcesAttributes svgResourcesAttributes)
            {
                svgResourcesAttributes.PrintResourcesAttributes(write, indent);
            }
            if (this is ISvgTransformableAttributes svgTransformableAttributes)
            {
                svgTransformableAttributes.PrintTransformableAttributes(write, indent);
            }
        }
    }
}
