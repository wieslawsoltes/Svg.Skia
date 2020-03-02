using System;
using Xml;

namespace Svg
{
    [Element("font-face")]
    public class SvgFontFace : SvgElement,
        ISvgCommonAttributes
    {
        [Attribute("font-family", SvgNamespace)]
        public string? FontFamily
        {
            get => this.GetAttribute("font-family");
            set => this.SetAttribute("font-family", value);
        }

        [Attribute("font-style", SvgNamespace)]
        public string? FontStyle
        {
            get => this.GetAttribute("font-style");
            set => this.SetAttribute("font-style", value);
        }

        [Attribute("font-variant", SvgNamespace)]
        public string? FontVariant
        {
            get => this.GetAttribute("font-variant");
            set => this.SetAttribute("font-variant", value);
        }

        [Attribute("font-weight", SvgNamespace)]
        public string? FontWeight
        {
            get => this.GetAttribute("font-weight");
            set => this.SetAttribute("font-weight", value);
        }

        [Attribute("font-stretch", SvgNamespace)]
        public string? FontStretch
        {
            get => this.GetAttribute("font-stretch");
            set => this.SetAttribute("font-stretch", value);
        }

        [Attribute("font-size", SvgNamespace)]
        public string? FontSize
        {
            get => this.GetAttribute("font-size");
            set => this.SetAttribute("font-size", value);
        }

        [Attribute("unicode-range", SvgNamespace)]
        public string? UnicodeRange
        {
            get => this.GetAttribute("unicode-range");
            set => this.SetAttribute("unicode-range", value);
        }

        [Attribute("units-per-em", SvgNamespace)]
        public string? UnitsPerEm
        {
            get => this.GetAttribute("units-per-em");
            set => this.SetAttribute("units-per-em", value);
        }

        [Attribute("panose-1", SvgNamespace)]
        public string? Panose1
        {
            get => this.GetAttribute("panose-1");
            set => this.SetAttribute("panose-1", value);
        }

        [Attribute("stemv", SvgNamespace)]
        public string? StemV
        {
            get => this.GetAttribute("stemv");
            set => this.SetAttribute("stemv", value);
        }

        [Attribute("stemh", SvgNamespace)]
        public string? StemH
        {
            get => this.GetAttribute("stemh");
            set => this.SetAttribute("stemh", value);
        }

        [Attribute("slope", SvgNamespace)]
        public string? Slope
        {
            get => this.GetAttribute("slope");
            set => this.SetAttribute("slope", value);
        }

        [Attribute("cap-height", SvgNamespace)]
        public string? CapHeight
        {
            get => this.GetAttribute("cap-height");
            set => this.SetAttribute("cap-height", value);
        }

        [Attribute("x-height", SvgNamespace)]
        public string? XHeight
        {
            get => this.GetAttribute("x-height");
            set => this.SetAttribute("x-height", value);
        }

        [Attribute("accent-height", SvgNamespace)]
        public string? AccentHeight
        {
            get => this.GetAttribute("accent-height");
            set => this.SetAttribute("accent-height", value);
        }

        [Attribute("ascent", SvgNamespace)]
        public string? Ascent
        {
            get => this.GetAttribute("ascent");
            set => this.SetAttribute("ascent", value);
        }

        [Attribute("descent", SvgNamespace)]
        public string? Descent
        {
            get => this.GetAttribute("descent");
            set => this.SetAttribute("descent", value);
        }

        [Attribute("widths", SvgNamespace)]
        public string? Widths
        {
            get => this.GetAttribute("widths");
            set => this.SetAttribute("widths", value);
        }

        [Attribute("bbox", SvgNamespace)]
        public string? BBox
        {
            get => this.GetAttribute("bbox");
            set => this.SetAttribute("bbox", value);
        }

        [Attribute("ideographic", SvgNamespace)]
        public string? Ideographic
        {
            get => this.GetAttribute("ideographic");
            set => this.SetAttribute("ideographic", value);
        }

        [Attribute("alphabetic", SvgNamespace)]
        public string? Alphabetic
        {
            get => this.GetAttribute("alphabetic");
            set => this.SetAttribute("alphabetic", value);
        }

        [Attribute("mathematical", SvgNamespace)]
        public string? Mathematical
        {
            get => this.GetAttribute("mathematical");
            set => this.SetAttribute("mathematical", value);
        }

        [Attribute("hanging", SvgNamespace)]
        public string? Hanging
        {
            get => this.GetAttribute("hanging");
            set => this.SetAttribute("hanging", value);
        }

        [Attribute("v-ideographic", SvgNamespace)]
        public string? VIdeographic
        {
            get => this.GetAttribute("v-ideographic");
            set => this.SetAttribute("v-ideographic", value);
        }

        [Attribute("v-alphabetic", SvgNamespace)]
        public string? VAlphabetic
        {
            get => this.GetAttribute("v-alphabetic");
            set => this.SetAttribute("v-alphabetic", value);
        }

        [Attribute("v-mathematical", SvgNamespace)]
        public string? VMathematical
        {
            get => this.GetAttribute("v-mathematical");
            set => this.SetAttribute("v-mathematical", value);
        }

        [Attribute("v-hanging", SvgNamespace)]
        public string? VHanging
        {
            get => this.GetAttribute("v-hanging");
            set => this.SetAttribute("v-hanging", value);
        }

        [Attribute("underline-position", SvgNamespace)]
        public string? UnderlinePosition
        {
            get => this.GetAttribute("underline-position");
            set => this.SetAttribute("underline-position", value);
        }

        [Attribute("underline-thickness", SvgNamespace)]
        public string? UnderlineThickness
        {
            get => this.GetAttribute("underline-thickness");
            set => this.SetAttribute("underline-thickness", value);
        }

        [Attribute("strikethrough-position", SvgNamespace)]
        public string? StrikethroughPosition
        {
            get => this.GetAttribute("strikethrough-position");
            set => this.SetAttribute("strikethrough-position", value);
        }

        [Attribute("strikethrough-thickness", SvgNamespace)]
        public string? StrikethroughThickness
        {
            get => this.GetAttribute("strikethrough-thickness");
            set => this.SetAttribute("strikethrough-thickness", value);
        }

        [Attribute("overline-position", SvgNamespace)]
        public string? OverlinePosition
        {
            get => this.GetAttribute("overline-position");
            set => this.SetAttribute("overline-position", value);
        }

        [Attribute("overline-thickness", SvgNamespace)]
        public string? OverlineThickness
        {
            get => this.GetAttribute("overline-thickness");
            set => this.SetAttribute("overline-thickness", value);
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (FontFamily != null)
            {
                write($"{indent}{nameof(FontFamily)}: \"{FontFamily}\"");
            }
            if (FontStyle != null)
            {
                write($"{indent}{nameof(FontStyle)}: \"{FontStyle}\"");
            }
            if (FontVariant != null)
            {
                write($"{indent}{nameof(FontVariant)}: \"{FontVariant}\"");
            }
            if (FontWeight != null)
            {
                write($"{indent}{nameof(FontWeight)}: \"{FontWeight}\"");
            }
            if (FontStretch != null)
            {
                write($"{indent}{nameof(FontStretch)}: \"{FontStretch}\"");
            }
            if (FontSize != null)
            {
                write($"{indent}{nameof(FontSize)}: \"{FontSize}\"");
            }
            if (UnicodeRange != null)
            {
                write($"{indent}{nameof(UnicodeRange)}: \"{UnicodeRange}\"");
            }
            if (UnitsPerEm != null)
            {
                write($"{indent}{nameof(UnitsPerEm)}: \"{UnitsPerEm}\"");
            }
            if (Panose1 != null)
            {
                write($"{indent}{nameof(Panose1)}: \"{Panose1}\"");
            }
            if (StemV != null)
            {
                write($"{indent}{nameof(StemV)}: \"{StemV}\"");
            }
            if (StemH != null)
            {
                write($"{indent}{nameof(StemH)}: \"{StemH}\"");
            }
            if (Slope != null)
            {
                write($"{indent}{nameof(Slope)}: \"{Slope}\"");
            }
            if (CapHeight != null)
            {
                write($"{indent}{nameof(CapHeight)}: \"{CapHeight}\"");
            }
            if (XHeight != null)
            {
                write($"{indent}{nameof(XHeight)}: \"{XHeight}\"");
            }
            if (AccentHeight != null)
            {
                write($"{indent}{nameof(AccentHeight)}: \"{AccentHeight}\"");
            }
            if (Ascent != null)
            {
                write($"{indent}{nameof(Ascent)}: \"{Ascent}\"");
            }
            if (Descent != null)
            {
                write($"{indent}{nameof(Descent)}: \"{Descent}\"");
            }
            if (Widths != null)
            {
                write($"{indent}{nameof(Widths)}: \"{Widths}\"");
            }
            if (BBox != null)
            {
                write($"{indent}{nameof(BBox)}: \"{BBox}\"");
            }
            if (Ideographic != null)
            {
                write($"{indent}{nameof(Ideographic)}: \"{Ideographic}\"");
            }
            if (Alphabetic != null)
            {
                write($"{indent}{nameof(Alphabetic)}: \"{Alphabetic}\"");
            }
            if (Mathematical != null)
            {
                write($"{indent}{nameof(Mathematical)}: \"{Mathematical}\"");
            }
            if (Hanging != null)
            {
                write($"{indent}{nameof(Hanging)}: \"{Hanging}\"");
            }
            if (VIdeographic != null)
            {
                write($"{indent}{nameof(VIdeographic)}: \"{VIdeographic}\"");
            }
            if (VAlphabetic != null)
            {
                write($"{indent}{nameof(VAlphabetic)}: \"{VAlphabetic}\"");
            }
            if (VMathematical != null)
            {
                write($"{indent}{nameof(VMathematical)}: \"{VMathematical}\"");
            }
            if (VHanging != null)
            {
                write($"{indent}{nameof(VHanging)}: \"{VHanging}\"");
            }
            if (UnderlinePosition != null)
            {
                write($"{indent}{nameof(UnderlinePosition)}: \"{UnderlinePosition}\"");
            }
            if (UnderlineThickness != null)
            {
                write($"{indent}{nameof(UnderlineThickness)}: \"{UnderlineThickness}\"");
            }
            if (StrikethroughPosition != null)
            {
                write($"{indent}{nameof(StrikethroughPosition)}: \"{StrikethroughPosition}\"");
            }
            if (StrikethroughThickness != null)
            {
                write($"{indent}{nameof(StrikethroughThickness)}: \"{StrikethroughThickness}\"");
            }
            if (OverlinePosition != null)
            {
                write($"{indent}{nameof(OverlinePosition)}: \"{OverlinePosition}\"");
            }
            if (OverlineThickness != null)
            {
                write($"{indent}{nameof(OverlineThickness)}: \"{OverlineThickness}\"");
            }
        }
    }
}
