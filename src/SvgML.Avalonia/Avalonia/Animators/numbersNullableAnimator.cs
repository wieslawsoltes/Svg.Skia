using Avalonia.Animation;
using Svg;

namespace SvgML;

public class numbersNullableAnimator : InterpolatingAnimator<numbers?>
{
    public override numbers? Interpolate(double progress, numbers? oldValue, numbers? newValue)
    {
        var value = new SvgNumberCollection();

        for (var i = 0; i < newValue.Number.Count; i++)
        {
            value.Add((float)(((newValue.Number[i] - oldValue.Number[i]) * progress) + oldValue.Number[i]));
        }

        return new numbers(value);
    }
}
