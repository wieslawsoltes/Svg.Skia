using System.ComponentModel;

namespace Svg
{
    [TypeConverter(typeof(SvgIsolationConverter))]
    public enum SvgIsolation
    {
        Auto,
        Isolate
    }

    public sealed class SvgIsolationConverter : EnumBaseConverter<SvgIsolation>
    {
        public SvgIsolationConverter() : base(CaseHandling.LowerCase)
        {
        }
    }

    [TypeConverter(typeof(SvgMixBlendModeConverter))]
    public enum SvgMixBlendMode
    {
        Normal,
        Multiply,
        Screen,
        Overlay,
        Darken,
        Lighten,
        ColorDodge,
        ColorBurn,
        HardLight,
        SoftLight,
        Difference,
        Exclusion,
        Hue,
        Saturation,
        Color,
        Luminosity
    }

    public sealed class SvgMixBlendModeConverter : EnumBaseConverter<SvgMixBlendMode>
    {
        public SvgMixBlendModeConverter() : base(CaseHandling.KebabCase)
        {
        }
    }

    public partial class SvgElement
    {
        [SvgAttribute("isolation")]
        public virtual SvgIsolation Isolation
        {
            get
            {
                return ComputedStyle.TryGetIsolation(out var isolation)
                    ? isolation
                    : SvgIsolation.Auto;
            }
            set { Attributes["isolation"] = value; }
        }

        [SvgAttribute("mix-blend-mode")]
        public virtual SvgMixBlendMode MixBlendMode
        {
            get
            {
                return ComputedStyle.TryGetMixBlendMode(out var mixBlendMode)
                    ? mixBlendMode
                    : SvgMixBlendMode.Normal;
            }
            set { Attributes["mix-blend-mode"] = value; }
        }
    }
}
