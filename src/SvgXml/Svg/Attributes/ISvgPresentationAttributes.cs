using System;
using Xml;

namespace Svg
{
    public interface ISvgPresentationAttributes : IElement, ISvgAttributePrinter
    {
        [Attribute("alignment-baseline")]
        public string? AlignmentBaseline
        {
            get => GetAttribute("alignment-baseline");
            set => SetAttribute("alignment-baseline", value);
        }

        [Attribute("baseline-shift")]
        public string? BaselineShift
        {
            get => GetAttribute("baseline-shift");
            set => SetAttribute("baseline-shift", value);
        }

        [Attribute("clip")]
        public string? Clip
        {
            get => GetAttribute("clip");
            set => SetAttribute("clip", value);
        }

        [Attribute("clip-path")]
        public string? ClipPath
        {
            get => GetAttribute("clip-path");
            set => SetAttribute("clip-path", value);
        }

        [Attribute("clip-rule")]
        public string? ClipRule
        {
            get => GetAttribute("clip-rule");
            set => SetAttribute("clip-rule", value);
        }

        [Attribute("color")]
        public string? Color
        {
            get => GetAttribute("color");
            set => SetAttribute("color", value);
        }

        [Attribute("color-interpolation")]
        public string? ColorInterpolation
        {
            get => GetAttribute("color-interpolation");
            set => SetAttribute("color-interpolation", value);
        }

        [Attribute("color-interpolation-filters")]
        public string? ColorInterpolationFilters
        {
            get => GetAttribute("color-interpolation-filters");
            set => SetAttribute("color-interpolation-filters", value);
        }

        [Attribute("color-profile")]
        public string? ColorProfile
        {
            get => GetAttribute("color-profile");
            set => SetAttribute("color-profile", value);
        }

        [Attribute("color-rendering")]
        public string? ColorRendering
        {
            get => GetAttribute("color-rendering");
            set => SetAttribute("color-rendering", value);
        }

        [Attribute("cursor")]
        public string? Cursor
        {
            get => GetAttribute("cursor");
            set => SetAttribute("cursor", value);
        }

        [Attribute("direction")]
        public string? Direction
        {
            get => GetAttribute("direction");
            set => SetAttribute("direction", value);
        }

        [Attribute("display")]
        public string? Display
        {
            get => GetAttribute("display");
            set => SetAttribute("display", value);
        }

        [Attribute("dominant-baseline")]
        public string? DominantBaseline
        {
            get => GetAttribute("dominant-baseline");
            set => SetAttribute("dominant-baseline", value);
        }

        [Attribute("enable-background")]
        public string? EnableBackground
        {
            get => GetAttribute("enable-background");
            set => SetAttribute("enable-background", value);
        }

        [Attribute("fill")]
        public string? Fill
        {
            get => GetAttribute("fill");
            set => SetAttribute("fill", value);
        }

        [Attribute("fill-opacity")]
        public string? FillOpacity
        {
            get => GetAttribute("fill-opacity");
            set => SetAttribute("fill-opacity", value);
        }

        [Attribute("fill-rule")]
        public string? FillRule
        {
            get => GetAttribute("fill-rule");
            set => SetAttribute("fill-rule", value);
        }

        [Attribute("filter")]
        public string? Filter
        {
            get => GetAttribute("filter");
            set => SetAttribute("filter", value);
        }

        [Attribute("flood-color")]
        public string? FloodColor
        {
            get => GetAttribute("flood-color");
            set => SetAttribute("flood-color", value);
        }

        [Attribute("flood-opacity")]
        public string? FloodOpacity
        {
            get => GetAttribute("flood-opacity");
            set => SetAttribute("flood-opacity", value);
        }

        [Attribute("font-family")]
        public string? FontFamily
        {
            get => GetAttribute("font-family");
            set => SetAttribute("font-family", value);
        }

        [Attribute("font-size")]
        public string? FontSize
        {
            get => GetAttribute("font-size");
            set => SetAttribute("font-size", value);
        }

        [Attribute("font-size-adjust")]
        public string? FontSizeAdjust
        {
            get => GetAttribute("font-size-adjust");
            set => SetAttribute("font-size-adjust", value);
        }

        [Attribute("font-stretch")]
        public string? FontStretch
        {
            get => GetAttribute("font-stretch");
            set => SetAttribute("font-stretch", value);
        }

        [Attribute("font-style")]
        public string? FontStyle
        {
            get => GetAttribute("font-style");
            set => SetAttribute("font-style", value);
        }

        [Attribute("font-variant")]
        public string? FontVariant
        {
            get => GetAttribute("font-variant");
            set => SetAttribute("font-variant", value);
        }

        [Attribute("font-weight")]
        public string? FontWeight
        {
            get => GetAttribute("font-weight");
            set => SetAttribute("font-weight", value);
        }

        [Attribute("glyph-orientation-horizontal")]
        public string? GlyphOrientationHorizontal
        {
            get => GetAttribute("glyph-orientation-horizontal");
            set => SetAttribute("glyph-orientation-horizontal", value);
        }

        [Attribute("glyph-orientation-vertical")]
        public string? GlyphOrientationVertical
        {
            get => GetAttribute("glyph-orientation-vertical");
            set => SetAttribute("glyph-orientation-vertical", value);
        }

        [Attribute("image-rendering")]
        public string? ImageRendering
        {
            get => GetAttribute("image-rendering");
            set => SetAttribute("image-rendering", value);
        }

        [Attribute("kerning")]
        public string? Kerning
        {
            get => GetAttribute("kerning");
            set => SetAttribute("kerning", value);
        }

        [Attribute("letter-spacing")]
        public string? LetterSpacing
        {
            get => GetAttribute("letter-spacing");
            set => SetAttribute("letter-spacing", value);
        }

        [Attribute("lighting-color")]
        public string? LightingColor
        {
            get => GetAttribute("lighting-color");
            set => SetAttribute("lighting-color", value);
        }

        [Attribute("marker-end")]
        public string? MarkerEnd
        {
            get => GetAttribute("marker-end");
            set => SetAttribute("marker-end", value);
        }

        [Attribute("marker-mid")]
        public string? MarkerMid
        {
            get => GetAttribute("marker-mid");
            set => SetAttribute("marker-mid", value);
        }

        [Attribute("marker-start")]
        public string? MarkerStart
        {
            get => GetAttribute("marker-start");
            set => SetAttribute("marker-start", value);
        }

        [Attribute("mask")]
        public string? Mask
        {
            get => GetAttribute("mask");
            set => SetAttribute("mask", value);
        }

        [Attribute("opacity")]
        public string? Opacity
        {
            get => GetAttribute("opacity");
            set => SetAttribute("opacity", value);
        }

        [Attribute("overflow")]
        public string? Overflow
        {
            get => GetAttribute("overflow");
            set => SetAttribute("overflow", value);
        }

        [Attribute("pointer-events")]
        public string? PointerEvents
        {
            get => GetAttribute("pointer-events");
            set => SetAttribute("pointer-events", value);
        }

        [Attribute("shape-rendering")]
        public string? ShapeRendering
        {
            get => GetAttribute("shape-rendering");
            set => SetAttribute("shape-rendering", value);
        }

        [Attribute("stop-color")]
        public string? StopColor
        {
            get => GetAttribute("stop-color");
            set => SetAttribute("stop-color", value);
        }

        [Attribute("stop-opacity")]
        public string? StopOpacity
        {
            get => GetAttribute("stop-opacity");
            set => SetAttribute("stop-opacity", value);
        }

        [Attribute("stroke")]
        public string? Stroke
        {
            get => GetAttribute("stroke");
            set => SetAttribute("stroke", value);
        }

        [Attribute("stroke-dasharray")]
        public string? StrokeDasharray
        {
            get => GetAttribute("stroke-dasharray");
            set => SetAttribute("stroke-dasharray", value);
        }

        [Attribute("stroke-dashoffset")]
        public string? StrokeDashoffset
        {
            get => GetAttribute("stroke-dashoffset");
            set => SetAttribute("stroke-dashoffset", value);
        }

        [Attribute("stroke-linecap")]
        public string? StrokeLinecap
        {
            get => GetAttribute("stroke-linecap");
            set => SetAttribute("stroke-linecap", value);
        }

        [Attribute("stroke-linejoin")]
        public string? SrokeLinejoin
        {
            get => GetAttribute("stroke-linejoin");
            set => SetAttribute("stroke-linejoin", value);
        }

        [Attribute("stroke-miterlimit")]
        public string? StrokeMiterlimit
        {
            get => GetAttribute("stroke-miterlimit");
            set => SetAttribute("stroke-miterlimit", value);
        }

        [Attribute("stroke-opacity")]
        public string? StrokeOpacity
        {
            get => GetAttribute("stroke-opacity");
            set => SetAttribute("stroke-opacity", value);
        }

        [Attribute("stroke-width")]
        public string? StrokeWidth
        {
            get => GetAttribute("stroke-width");
            set => SetAttribute("stroke-width", value);
        }

        [Attribute("text-anchor")]
        public string? TextAnchor
        {
            get => GetAttribute("text-anchor");
            set => SetAttribute("text-anchor", value);
        }

        [Attribute("text-decoration")]
        public string? TextDecoration
        {
            get => GetAttribute("text-decoration");
            set => SetAttribute("text-decoration", value);
        }

        [Attribute("text-rendering")]
        public string? TextRendering
        {
            get => GetAttribute("text-rendering");
            set => SetAttribute("text-rendering", value);
        }

        [Attribute("unicode-bidi")]
        public string? UnicodeBidi
        {
            get => GetAttribute("unicode-bidi");
            set => SetAttribute("unicode-bidi", value);
        }

        [Attribute("visibility")]
        public string? Visibility
        {
            get => GetAttribute("visibility");
            set => SetAttribute("visibility", value);
        }

        [Attribute("word-spacing")]
        public string? WordSpacing
        {
            get => GetAttribute("word-spacing");
            set => SetAttribute("word-spacing", value);
        }

        [Attribute("writing-mode")]
        public string? WritingMode
        {
            get => GetAttribute("writing-mode");
            set => SetAttribute("writing-mode", value);
        }

        public void PrintPresentationAttributes(string indent)
        {
            if (AlignmentBaseline != null)
            {
                Console.WriteLine($"{indent}{nameof(AlignmentBaseline)}='{AlignmentBaseline}'");
            }
            if (BaselineShift != null)
            {
                Console.WriteLine($"{indent}{nameof(BaselineShift)}='{BaselineShift}'");
            }
            if (Clip != null)
            {
                Console.WriteLine($"{indent}{nameof(Clip)}='{Clip}'");
            }
            if (ClipPath != null)
            {
                Console.WriteLine($"{indent}{nameof(ClipPath)}='{ClipPath}'");
            }
            if (ClipRule != null)
            {
                Console.WriteLine($"{indent}{nameof(ClipRule)}='{ClipRule}'");
            }
            if (Color != null)
            {
                Console.WriteLine($"{indent}{nameof(Color)}='{Color}'");
            }
            if (ColorInterpolation != null)
            {
                Console.WriteLine($"{indent}{nameof(ColorInterpolation)}='{ColorInterpolation}'");
            }
            if (ColorInterpolationFilters != null)
            {
                Console.WriteLine($"{indent}{nameof(ColorInterpolationFilters)}='{ColorInterpolationFilters}'");
            }
            if (ColorProfile != null)
            {
                Console.WriteLine($"{indent}{nameof(ColorProfile)}='{ColorProfile}'");
            }
            if (ColorRendering != null)
            {
                Console.WriteLine($"{indent}{nameof(ColorRendering)}='{ColorRendering}'");
            }
            if (Cursor != null)
            {
                Console.WriteLine($"{indent}{nameof(Cursor)}='{Cursor}'");
            }
            if (Direction != null)
            {
                Console.WriteLine($"{indent}{nameof(Direction)}='{Direction}'");
            }
            if (Display != null)
            {
                Console.WriteLine($"{indent}{nameof(Display)}='{Display}'");
            }
            if (DominantBaseline != null)
            {
                Console.WriteLine($"{indent}{nameof(DominantBaseline)}='{DominantBaseline}'");
            }
            if (EnableBackground != null)
            {
                Console.WriteLine($"{indent}{nameof(EnableBackground)}='{EnableBackground}'");
            }
            if (Fill != null)
            {
                Console.WriteLine($"{indent}{nameof(Fill)}='{Fill}'");
            }
            if (FillOpacity != null)
            {
                Console.WriteLine($"{indent}{nameof(FillOpacity)}='{FillOpacity}'");
            }
            if (FillRule != null)
            {
                Console.WriteLine($"{indent}{nameof(FillRule)}='{FillRule}'");
            }
            if (Filter != null)
            {
                Console.WriteLine($"{indent}{nameof(Filter)}='{Filter}'");
            }
            if (FloodColor != null)
            {
                Console.WriteLine($"{indent}{nameof(FloodColor)}='{FloodColor}'");
            }
            if (FloodOpacity != null)
            {
                Console.WriteLine($"{indent}{nameof(FloodOpacity)}='{FloodOpacity}'");
            }
            if (FontFamily != null)
            {
                Console.WriteLine($"{indent}{nameof(FontFamily)}='{FontFamily}'");
            }
            if (FontSize != null)
            {
                Console.WriteLine($"{indent}{nameof(FontSize)}='{FontSize}'");
            }
            if (FontSizeAdjust != null)
            {
                Console.WriteLine($"{indent}{nameof(FontSizeAdjust)}='{FontSizeAdjust}'");
            }
            if (FontStretch != null)
            {
                Console.WriteLine($"{indent}{nameof(FontStretch)}='{FontStretch}'");
            }
            if (FontStyle != null)
            {
                Console.WriteLine($"{indent}{nameof(FontStyle)}='{FontStyle}'");
            }
            if (FontVariant != null)
            {
                Console.WriteLine($"{indent}{nameof(FontVariant)}='{FontVariant}'");
            }
            if (FontWeight != null)
            {
                Console.WriteLine($"{indent}{nameof(FontWeight)}='{FontWeight}'");
            }
            if (GlyphOrientationHorizontal != null)
            {
                Console.WriteLine($"{indent}{nameof(GlyphOrientationHorizontal)}='{GlyphOrientationHorizontal}'");
            }
            if (GlyphOrientationVertical != null)
            {
                Console.WriteLine($"{indent}{nameof(GlyphOrientationVertical)}='{GlyphOrientationVertical}'");
            }
            if (ImageRendering != null)
            {
                Console.WriteLine($"{indent}{nameof(ImageRendering)}='{ImageRendering}'");
            }
            if (Kerning != null)
            {
                Console.WriteLine($"{indent}{nameof(Kerning)}='{Kerning}'");
            }
            if (LetterSpacing != null)
            {
                Console.WriteLine($"{indent}{nameof(LetterSpacing)}='{LetterSpacing}'");
            }
            if (LightingColor != null)
            {
                Console.WriteLine($"{indent}{nameof(LightingColor)}='{LightingColor}'");
            }
            if (MarkerEnd != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerEnd)}='{MarkerEnd}'");
            }
            if (MarkerMid != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerMid)}='{MarkerMid}'");
            }
            if (MarkerStart != null)
            {
                Console.WriteLine($"{indent}{nameof(MarkerStart)}='{MarkerStart}'");
            }
            if (Mask != null)
            {
                Console.WriteLine($"{indent}{nameof(Mask)}='{Mask}'");
            }
            if (Opacity != null)
            {
                Console.WriteLine($"{indent}{nameof(Opacity)}='{Opacity}'");
            }
            if (Overflow != null)
            {
                Console.WriteLine($"{indent}{nameof(Overflow)}='{Overflow}'");
            }
            if (PointerEvents != null)
            {
                Console.WriteLine($"{indent}{nameof(PointerEvents)}='{PointerEvents}'");
            }
            if (ShapeRendering != null)
            {
                Console.WriteLine($"{indent}{nameof(ShapeRendering)}='{ShapeRendering}'");
            }
            if (StopColor != null)
            {
                Console.WriteLine($"{indent}{nameof(StopColor)}='{StopColor}'");
            }
            if (StopOpacity != null)
            {
                Console.WriteLine($"{indent}{nameof(StopOpacity)}='{StopOpacity}'");
            }
            if (Stroke != null)
            {
                Console.WriteLine($"{indent}{nameof(Stroke)}='{Stroke}'");
            }
            if (StrokeDasharray != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeDasharray)}='{StrokeDasharray}'");
            }
            if (StrokeDashoffset != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeDashoffset)}='{StrokeDashoffset}'");
            }
            if (StrokeLinecap != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeLinecap)}='{StrokeLinecap}'");
            }
            if (SrokeLinejoin != null)
            {
                Console.WriteLine($"{indent}{nameof(SrokeLinejoin)}='{SrokeLinejoin}'");
            }
            if (StrokeMiterlimit != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeMiterlimit)}='{StrokeMiterlimit}'");
            }
            if (StrokeOpacity != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeOpacity)}='{StrokeOpacity}'");
            }
            if (StrokeWidth != null)
            {
                Console.WriteLine($"{indent}{nameof(StrokeWidth)}='{StrokeWidth}'");
            }
            if (TextAnchor != null)
            {
                Console.WriteLine($"{indent}{nameof(TextAnchor)}='{TextAnchor}'");
            }
            if (TextDecoration != null)
            {
                Console.WriteLine($"{indent}{nameof(TextDecoration)}='{TextDecoration}'");
            }
            if (TextRendering != null)
            {
                Console.WriteLine($"{indent}{nameof(TextRendering)}='{TextRendering}'");
            }
            if (UnicodeBidi != null)
            {
                Console.WriteLine($"{indent}{nameof(UnicodeBidi)}='{UnicodeBidi}'");
            }
            if (Visibility != null)
            {
                Console.WriteLine($"{indent}{nameof(Visibility)}='{Visibility}'");
            }
            if (WordSpacing != null)
            {
                Console.WriteLine($"{indent}{nameof(WordSpacing)}='{WordSpacing}'");
            }
            if (WritingMode != null)
            {
                Console.WriteLine($"{indent}{nameof(WritingMode)}='{WritingMode}'");
            }
        }
    }
}
