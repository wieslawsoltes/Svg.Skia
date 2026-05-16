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
                if (TryGetAttribute("isolation", out var value))
                {
                    switch (NormalizeCssEnumToken(value))
                    {
                        case "isolate":
                            return SvgIsolation.Isolate;
                        case "auto":
                            return SvgIsolation.Auto;
                    }
                }

                return GetAttribute("isolation", false, SvgIsolation.Auto);
            }
            set { Attributes["isolation"] = value; }
        }

        [SvgAttribute("mix-blend-mode")]
        public virtual SvgMixBlendMode MixBlendMode
        {
            get
            {
                if (TryGetAttribute("mix-blend-mode", out var value))
                {
                    switch (NormalizeCssEnumToken(value))
                    {
                        case "multiply":
                            return SvgMixBlendMode.Multiply;
                        case "screen":
                            return SvgMixBlendMode.Screen;
                        case "overlay":
                            return SvgMixBlendMode.Overlay;
                        case "darken":
                            return SvgMixBlendMode.Darken;
                        case "lighten":
                            return SvgMixBlendMode.Lighten;
                        case "colordodge":
                            return SvgMixBlendMode.ColorDodge;
                        case "colorburn":
                            return SvgMixBlendMode.ColorBurn;
                        case "hardlight":
                            return SvgMixBlendMode.HardLight;
                        case "softlight":
                            return SvgMixBlendMode.SoftLight;
                        case "difference":
                            return SvgMixBlendMode.Difference;
                        case "exclusion":
                            return SvgMixBlendMode.Exclusion;
                        case "hue":
                            return SvgMixBlendMode.Hue;
                        case "saturation":
                            return SvgMixBlendMode.Saturation;
                        case "color":
                            return SvgMixBlendMode.Color;
                        case "luminosity":
                            return SvgMixBlendMode.Luminosity;
                        case "normal":
                            return SvgMixBlendMode.Normal;
                    }
                }

                return GetAttribute("mix-blend-mode", false, SvgMixBlendMode.Normal);
            }
            set { Attributes["mix-blend-mode"] = value; }
        }

        private static string NormalizeCssEnumToken(string value)
        {
            return (value ?? string.Empty).Trim().Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
