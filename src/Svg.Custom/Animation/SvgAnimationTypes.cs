using System.ComponentModel;

namespace Svg
{
    [TypeConverter(typeof(SvgAnimationAttributeTypeConverter))]
    public enum SvgAnimationAttributeType
    {
        Auto,
        Css,
        Xml
    }

    [TypeConverter(typeof(SvgAnimationRestartConverter))]
    public enum SvgAnimationRestart
    {
        Always,
        Never,
        WhenNotActive
    }

    [TypeConverter(typeof(SvgAnimationFillConverter))]
    public enum SvgAnimationFill
    {
        Remove,
        Freeze
    }

    [TypeConverter(typeof(SvgAnimationCalcModeConverter))]
    public enum SvgAnimationCalcMode
    {
        Discrete,
        Linear,
        Paced,
        Spline
    }

    [TypeConverter(typeof(SvgAnimationAdditiveConverter))]
    public enum SvgAnimationAdditive
    {
        Replace,
        Sum
    }

    [TypeConverter(typeof(SvgAnimationAccumulateConverter))]
    public enum SvgAnimationAccumulate
    {
        None,
        Sum
    }

    [TypeConverter(typeof(SvgAnimateTransformTypeConverter))]
    public enum SvgAnimateTransformType
    {
        Translate,
        Scale,
        Rotate,
        SkewX,
        SkewY
    }

    public sealed class SvgAnimationAttributeTypeConverter : EnumBaseConverter<SvgAnimationAttributeType>
    {
    }

    public sealed class SvgAnimationRestartConverter : EnumBaseConverter<SvgAnimationRestart>
    {
    }

    public sealed class SvgAnimationFillConverter : EnumBaseConverter<SvgAnimationFill>
    {
    }

    public sealed class SvgAnimationCalcModeConverter : EnumBaseConverter<SvgAnimationCalcMode>
    {
    }

    public sealed class SvgAnimationAdditiveConverter : EnumBaseConverter<SvgAnimationAdditive>
    {
    }

    public sealed class SvgAnimationAccumulateConverter : EnumBaseConverter<SvgAnimationAccumulate>
    {
    }

    public sealed class SvgAnimateTransformTypeConverter : EnumBaseConverter<SvgAnimateTransformType>
    {
    }
}
