using Avalonia.Animation;
using Svg;

namespace SvgML;

public abstract partial class element
{
    static element()
    {
        Animation.RegisterCustomAnimator<float, FloatAnimator>();
        Animation.RegisterCustomAnimator<float?, FloatNullableAnimator>();

        Animation.RegisterCustomAnimator<int, IntAnimator>();
        Animation.RegisterCustomAnimator<int?, IntNullableAnimator>();

        Animation.RegisterCustomAnimator<SvgUnit, SvgUnitAnimator>();
        Animation.RegisterCustomAnimator<SvgUnit?, SvgUnitNullableAnimator>();

        Animation.RegisterCustomAnimator<SvgNumberCollection, SvgNumberCollectionAnimator>();
        Animation.RegisterCustomAnimator<SvgNumberCollection?, SvgNumberCollectionNullableAnimator>();

        Animation.RegisterCustomAnimator<numbers, numbersAnimator>();
        Animation.RegisterCustomAnimator<numbers?, numbersNullableAnimator>();
    }
}
