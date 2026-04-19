using Avalonia.Animation;
using Svg;

namespace SvgML;

public class SvgNumberCollectionNullableAnimator : InterpolatingAnimator<SvgNumberCollection?>
{
    public override SvgNumberCollection? Interpolate(double progress, SvgNumberCollection? oldValue, SvgNumberCollection? newValue)
    {
        var value = new SvgNumberCollection();

        for (var i = 0; i < newValue.Count; i++)
        {
            value.Add((float)(((newValue[i] - oldValue[i]) * progress) + oldValue[i]));
        }

        return value;
    }
}
