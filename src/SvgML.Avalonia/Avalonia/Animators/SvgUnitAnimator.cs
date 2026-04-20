using Avalonia.Animation;
using Svg;

namespace SvgML;

public class SvgUnitAnimator : InterpolatingAnimator<SvgUnit>
{
    public override SvgUnit Interpolate(double progress, SvgUnit oldValue, SvgUnit newValue)
    {
        var value = (float)(((newValue.Value - oldValue.Value) * progress) + oldValue.Value);
        return new SvgUnit(newValue.Type, value);
    }
}
