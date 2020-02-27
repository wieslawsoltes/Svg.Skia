using System;
using Xml;

namespace Svg
{
    [Element("font-face")]
    public class SvgFontFace : SvgElement
    {
        [Attribute("font-family", SvgElement.SvgNamespace)]
        public string? FontFamily
        {
            get => GetAttribute("font-family");
            set => SetAttribute("font-family", value);
        }

        [Attribute("font-style", SvgElement.SvgNamespace)]
        public string? FontStyle
        {
            get => GetAttribute("font-style");
            set => SetAttribute("font-style", value);
        }

        [Attribute("font-variant", SvgElement.SvgNamespace)]
        public string? FontVariant
        {
            get => GetAttribute("font-variant");
            set => SetAttribute("font-variant", value);
        }

        [Attribute("font-weight", SvgElement.SvgNamespace)]
        public string? FontWeight
        {
            get => GetAttribute("font-weight");
            set => SetAttribute("font-weight", value);
        }

        [Attribute("font-stretch", SvgElement.SvgNamespace)]
        public string? FontStretch
        {
            get => GetAttribute("font-stretch");
            set => SetAttribute("font-stretch", value);
        }

        [Attribute("font-size", SvgElement.SvgNamespace)]
        public string? FontSize
        {
            get => GetAttribute("font-size");
            set => SetAttribute("font-size", value);
        }

        [Attribute("unicode-range", SvgElement.SvgNamespace)]
        public string? UnicodeRange
        {
            get => GetAttribute("unicode-range");
            set => SetAttribute("unicode-range", value);
        }

        [Attribute("units-per-em", SvgElement.SvgNamespace)]
        public string? UnitsPerEm
        {
            get => GetAttribute("units-per-em");
            set => SetAttribute("units-per-em", value);
        }

        [Attribute("panose-1", SvgElement.SvgNamespace)]
        public string? Panose1
        {
            get => GetAttribute("panose-1");
            set => SetAttribute("panose-1", value);
        }

        [Attribute("stemv", SvgElement.SvgNamespace)]
        public string? StemV
        {
            get => GetAttribute("stemv");
            set => SetAttribute("stemv", value);
        }

        [Attribute("stemh", SvgElement.SvgNamespace)]
        public string? StemH
        {
            get => GetAttribute("stemh");
            set => SetAttribute("stemh", value);
        }

        [Attribute("slope", SvgElement.SvgNamespace)]
        public string? Slope
        {
            get => GetAttribute("slope");
            set => SetAttribute("slope", value);
        }

        [Attribute("cap-height", SvgElement.SvgNamespace)]
        public string? CapHeight
        {
            get => GetAttribute("cap-height");
            set => SetAttribute("cap-height", value);
        }

        [Attribute("x-height", SvgElement.SvgNamespace)]
        public string? XHeight
        {
            get => GetAttribute("x-height");
            set => SetAttribute("x-height", value);
        }

        [Attribute("accent-height", SvgElement.SvgNamespace)]
        public string? AccentHeight
        {
            get => GetAttribute("accent-height");
            set => SetAttribute("accent-height", value);
        }

        [Attribute("ascent", SvgElement.SvgNamespace)]
        public string? Ascent
        {
            get => GetAttribute("ascent");
            set => SetAttribute("ascent", value);
        }

        [Attribute("descent", SvgElement.SvgNamespace)]
        public string? Descent
        {
            get => GetAttribute("descent");
            set => SetAttribute("descent", value);
        }

        [Attribute("widths", SvgElement.SvgNamespace)]
        public string? Widths
        {
            get => GetAttribute("widths");
            set => SetAttribute("widths", value);
        }

        [Attribute("bbox", SvgElement.SvgNamespace)]
        public string? BBox
        {
            get => GetAttribute("bbox");
            set => SetAttribute("bbox", value);
        }

        [Attribute("ideographic", SvgElement.SvgNamespace)]
        public string? Ideographic
        {
            get => GetAttribute("ideographic");
            set => SetAttribute("ideographic", value);
        }

        [Attribute("alphabetic", SvgElement.SvgNamespace)]
        public string? Alphabetic
        {
            get => GetAttribute("alphabetic");
            set => SetAttribute("alphabetic", value);
        }

        [Attribute("mathematical", SvgElement.SvgNamespace)]
        public string? Mathematical
        {
            get => GetAttribute("mathematical");
            set => SetAttribute("mathematical", value);
        }

        [Attribute("hanging", SvgElement.SvgNamespace)]
        public string? Hanging
        {
            get => GetAttribute("hanging");
            set => SetAttribute("hanging", value);
        }

        [Attribute("v-ideographic", SvgElement.SvgNamespace)]
        public string? VIdeographic
        {
            get => GetAttribute("v-ideographic");
            set => SetAttribute("v-ideographic", value);
        }

        [Attribute("v-alphabetic", SvgElement.SvgNamespace)]
        public string? VAlphabetic
        {
            get => GetAttribute("v-alphabetic");
            set => SetAttribute("v-alphabetic", value);
        }

        [Attribute("v-mathematical", SvgElement.SvgNamespace)]
        public string? VMathematical
        {
            get => GetAttribute("v-mathematical");
            set => SetAttribute("v-mathematical", value);
        }

        [Attribute("v-hanging", SvgElement.SvgNamespace)]
        public string? VHanging
        {
            get => GetAttribute("v-hanging");
            set => SetAttribute("v-hanging", value);
        }

        [Attribute("underline-position", SvgElement.SvgNamespace)]
        public string? UnderlinePosition
        {
            get => GetAttribute("underline-position");
            set => SetAttribute("underline-position", value);
        }

        [Attribute("underline-thickness", SvgElement.SvgNamespace)]
        public string? UnderlineThickness
        {
            get => GetAttribute("underline-thickness");
            set => SetAttribute("underline-thickness", value);
        }

        [Attribute("strikethrough-position", SvgElement.SvgNamespace)]
        public string? StrikethroughPosition
        {
            get => GetAttribute("strikethrough-position");
            set => SetAttribute("strikethrough-position", value);
        }

        [Attribute("strikethrough-thickness", SvgElement.SvgNamespace)]
        public string? StrikethroughThickness
        {
            get => GetAttribute("strikethrough-thickness");
            set => SetAttribute("strikethrough-thickness", value);
        }

        [Attribute("overline-position", SvgElement.SvgNamespace)]
        public string? OverlinePosition
        {
            get => GetAttribute("overline-position");
            set => SetAttribute("overline-position", value);
        }

        [Attribute("overline-thickness", SvgElement.SvgNamespace)]
        public string? OverlineThickness
        {
            get => GetAttribute("overline-thickness");
            set => SetAttribute("overline-thickness", value);
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
