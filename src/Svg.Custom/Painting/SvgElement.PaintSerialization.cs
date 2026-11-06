using System.Drawing;

namespace Svg
{
    public partial class SvgElement
    {
        internal SvgPaintServer GetPaintForSerialization(SvgPaintServer paint)
        {
            // The upstream writer folds SvgColourServer alpha into fill/stroke-opacity.
            // Svg.Custom also writes that alpha in the CSS color, which would apply it
            // twice on every save. Supply a serialization-only value so the existing
            // writer preserves the independent color alpha and paint opacity. The live
            // paint server, inherited values, and rendering behavior remain untouched.
            if (Writing && paint is SvgColourServer solid && solid.Colour.A < byte.MaxValue)
            {
                return new SvgSerializedSolidPaint(solid.Colour);
            }

            return paint;
        }
    }

    // This is an attribute value, not an SVG element registered by the generator.
    [SvgElement("")]
    internal sealed class SvgSerializedSolidPaint : SvgPaintServer
    {
        internal SvgSerializedSolidPaint(Color colour)
        {
            Colour = colour;
        }

        internal Color Colour { get; }

        public override SvgElement DeepCopy()
        {
            return new SvgSerializedSolidPaint(Colour);
        }
    }
}
