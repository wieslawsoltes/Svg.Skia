using Xml;

namespace Svg
{
    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}
