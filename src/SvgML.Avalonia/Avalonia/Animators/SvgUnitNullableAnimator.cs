using Avalonia.Animation;
using Svg;

namespace SvgML;

public class SvgUnitNullableAnimator : InterpolatingAnimator<SvgUnit?>
{
    public override SvgUnit? Interpolate(double progress, SvgUnit? oldValue, SvgUnit? newValue)
    {
        var value = (float)(((newValue.Value.Value - oldValue.Value.Value) * progress) + oldValue.Value.Value);
        return new SvgUnit(newValue.Value.Type, value);
    }
}
