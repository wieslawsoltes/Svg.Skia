using Avalonia.Animation;

namespace SvgML;

public class FloatAnimator : InterpolatingAnimator<float>
{
    public override float Interpolate(double progress, float oldValue, float newValue)
    {
        return (float)(((newValue - oldValue) * progress) + oldValue);
    }
}
