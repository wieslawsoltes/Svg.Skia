using Avalonia.Animation;

namespace SvgML;

public class IntAnimator : InterpolatingAnimator<int>
{
    public override int Interpolate(double progress, int oldValue, int newValue)
    {
        return (int)(((newValue - oldValue) * progress) + oldValue);
    }
}
