using System;
using System.Collections.Generic;
using Xml;

namespace Svg
{
    public static class SvgAttributes
    {
        public const string SvgNamespace = "http://www.w3.org/2000/svg";

        public const string XLinkNamespace = "http://www.w3.org/1999/xlink";

        public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

        public static ISet<string> s_coreElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "altGlyphDef",
            "altGlyphItem",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "circle",
            "clipPath",
            "color-profile",
            "cursor",
            "defs",
            "desc",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feDistantLight",
            "feFlood",
            "feFuncA",
            "feFuncB",
            "feFuncG",
            "feFuncR",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMergeNode",
            "feMorphology",
            "feOffset",
            "fePointLight",
            "feSpecularLighting",
            "feSpotLight",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "font-face",
            "font-face-format",
            "font-face-name",
            "font-face-src",
            "font-face-uri",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "hkern",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "metadata",
            "missing-glyph",
            "mpath",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "script",
            "set",
            "stop",
            "style",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "title",
            "tref",
            "tspan",
            "use",
            "view",
            "vkern"
        };

        public static ISet<string> s_testsElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "circle",
            "clipPath",
            "cursor",
            "defs",
            "ellipse",
            "foreignObject",
            "g",
            "image",
            "line",
            "mask",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "rect",
            "set",
            "svg",
            "switch",
            "text",
            "textPath",
            "tref",
            "tspan",
            "use"
        };

        public static ISet<string> s_presentationElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "animate",
            "animateColor",
            "circle",
            "clipPath",
            "defs",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feFlood",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMorphology",
            "feOffset",
            "feSpecularLighting",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "missing-glyph",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "stop",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "tref",
            "tspan",
            "use"
        };

        public static ISet<string> s_stylableElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "circle",
            "clipPath",
            "defs",
            "desc",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feFlood",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMorphology",
            "feOffset",
            "feSpecularLighting",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "missing-glyph",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "stop",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "title",
            "tref",
            "tspan",
            "use"
        };

        public static ISet<string> s_transformableElements = new HashSet<string>()
        {
            "a",
            "circle",
            "clipPath",
            "defs",
            "ellipse",
            "foreignObject",
            "g",
            "image",
            "line",
            "path",
            "polygon",
            "polyline",
            "rect",
            "switch",
            "text",
            "use"
        };

        public static ISet<string> s_resourcesElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "circle",
            "clipPath",
            "cursor",
            "defs",
            "ellipse",
            "feImage",
            "filter",
            "font",
            "foreignObject",
            "g",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "mpath",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "script",
            "set",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "tref",
            "tspan",
            "use",
            "view"
        };

        public static ISet<string> s_xlinkElements = new HashSet<string>()
        {
            "a",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "altGlyph",
            "color-profile",
            "cursor",
            "feImage",
            "filter",
            "font-face-uri",
            "glyphRef",
            "image",
            "mpath",
            "pattern",
            "script",
            "set",
            "use",
            "linearGradient",
            "radialGradient",
            "textPath",
            "tref"
        };

        public static void Print(this SvgElement element, Action<string> write, string indent = "", bool printAttributes = true)
        {
            write($"{indent}{element.GetType().Name}:");

            if (printAttributes)
            {
#if true
                if (element.Content != string.Empty)
                {
                    write($"{indent}  {nameof(element.Content)}: \"{element.Content}\"");
                }
                element.Print(write, indent + "  ");
#else
                foreach (var attribute in element.Attributes)
                {
                    write($"{indent}  {attribute.Key}: \"{attribute.Value}\"");
                }
#endif
            }

            if (element.Children.Count > 0)
            {
                write($"{indent}  Children:");
                foreach (var child in element.Children)
                {
                    if (child is SvgElement childElement)
                    {
                        Print(childElement, write, indent + "    ", printAttributes);
                    }
                    else if (child is ContentElement contentElement)
                    {
                        write($"{indent}    {contentElement.GetType().Name}:");
                        if (printAttributes)
                        {
                            write($"{indent}      {nameof(contentElement.Content)}: \"{contentElement.Content}\"");
                        }
                    }
                    else if (child is UnknownElement unknownElement)
                    {
                        write($"{indent}  {unknownElement.GetType().Name}");
                        if (printAttributes)
                        {
                            write($"{indent}    {nameof(unknownElement.Tag)}: {unknownElement.Tag}");
                            foreach (var attribute in element.Attributes)
                            {
                                write($"{indent}    {attribute.Key}: \"{attribute.Value}\"");
                            }
                        }
                    }
                }
            }
        }

        public static void PrintCommonAttributes(this ISvgCommonAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.Id != null)
            {
                write($"{indent}{nameof(attributes.Id)}: \"{attributes.Id}\"");
            }
            if (attributes.Base != null)
            {
                write($"{indent}{nameof(attributes.Base)}: \"{attributes.Base}\"");
            }
            if (attributes.Lang != null)
            {
                write($"{indent}{nameof(attributes.Lang)}: \"{attributes.Lang}\"");
            }
            if (attributes.Space != null)
            {
                write($"{indent}{nameof(attributes.Space)}: \"{attributes.Space}\"");
            }
        }

        public static void PrintPresentationAttributes(this ISvgPresentationAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.AlignmentBaseline != null)
            {
                write($"{indent}{nameof(attributes.AlignmentBaseline)}: \"{attributes.AlignmentBaseline}\"");
            }
            if (attributes.BaselineShift != null)
            {
                write($"{indent}{nameof(attributes.BaselineShift)}: \"{attributes.BaselineShift}\"");
            }
            if (attributes.Clip != null)
            {
                write($"{indent}{nameof(attributes.Clip)}: \"{attributes.Clip}\"");
            }
            if (attributes.ClipPath != null)
            {
                write($"{indent}{nameof(attributes.ClipPath)}: \"{attributes.ClipPath}\"");
            }
            if (attributes.ClipRule != null)
            {
                write($"{indent}{nameof(attributes.ClipRule)}: \"{attributes.ClipRule}\"");
            }
            if (attributes.Color != null)
            {
                write($"{indent}{nameof(attributes.Color)}: \"{attributes.Color}\"");
            }
            if (attributes.ColorInterpolation != null)
            {
                write($"{indent}{nameof(attributes.ColorInterpolation)}: \"{attributes.ColorInterpolation}\"");
            }
            if (attributes.ColorInterpolationFilters != null)
            {
                write($"{indent}{nameof(attributes.ColorInterpolationFilters)}: \"{attributes.ColorInterpolationFilters}\"");
            }
            if (attributes.ColorProfile != null)
            {
                write($"{indent}{nameof(attributes.ColorProfile)}: \"{attributes.ColorProfile}\"");
            }
            if (attributes.ColorRendering != null)
            {
                write($"{indent}{nameof(attributes.ColorRendering)}: \"{attributes.ColorRendering}\"");
            }
            if (attributes.Cursor != null)
            {
                write($"{indent}{nameof(attributes.Cursor)}: \"{attributes.Cursor}\"");
            }
            if (attributes.Direction != null)
            {
                write($"{indent}{nameof(attributes.Direction)}: \"{attributes.Direction}\"");
            }
            if (attributes.Display != null)
            {
                write($"{indent}{nameof(attributes.Display)}: \"{attributes.Display}\"");
            }
            if (attributes.DominantBaseline != null)
            {
                write($"{indent}{nameof(attributes.DominantBaseline)}: \"{attributes.DominantBaseline}\"");
            }
            if (attributes.EnableBackground != null)
            {
                write($"{indent}{nameof(attributes.EnableBackground)}: \"{attributes.EnableBackground}\"");
            }
            if (attributes.Fill != null)
            {
                write($"{indent}{nameof(attributes.Fill)}: \"{attributes.Fill}\"");
            }
            if (attributes.FillOpacity != null)
            {
                write($"{indent}{nameof(attributes.FillOpacity)}: \"{attributes.FillOpacity}\"");
            }
            if (attributes.FillRule != null)
            {
                write($"{indent}{nameof(attributes.FillRule)}: \"{attributes.FillRule}\"");
            }
            if (attributes.Filter != null)
            {
                write($"{indent}{nameof(attributes.Filter)}: \"{attributes.Filter}\"");
            }
            if (attributes.FloodColor != null)
            {
                write($"{indent}{nameof(attributes.FloodColor)}: \"{attributes.FloodColor}\"");
            }
            if (attributes.FloodOpacity != null)
            {
                write($"{indent}{nameof(attributes.FloodOpacity)}: \"{attributes.FloodOpacity}\"");
            }
            if (attributes.FontFamily != null)
            {
                write($"{indent}{nameof(attributes.FontFamily)}: \"{attributes.FontFamily}\"");
            }
            if (attributes.FontSize != null)
            {
                write($"{indent}{nameof(attributes.FontSize)}: \"{attributes.FontSize}\"");
            }
            if (attributes.FontSizeAdjust != null)
            {
                write($"{indent}{nameof(attributes.FontSizeAdjust)}: \"{attributes.FontSizeAdjust}\"");
            }
            if (attributes.FontStretch != null)
            {
                write($"{indent}{nameof(attributes.FontStretch)}: \"{attributes.FontStretch}\"");
            }
            if (attributes.FontStyle != null)
            {
                write($"{indent}{nameof(attributes.FontStyle)}: \"{attributes.FontStyle}\"");
            }
            if (attributes.FontVariant != null)
            {
                write($"{indent}{nameof(attributes.FontVariant)}: \"{attributes.FontVariant}\"");
            }
            if (attributes.FontWeight != null)
            {
                write($"{indent}{nameof(attributes.FontWeight)}: \"{attributes.FontWeight}\"");
            }
            if (attributes.GlyphOrientationHorizontal != null)
            {
                write($"{indent}{nameof(attributes.GlyphOrientationHorizontal)}: \"{attributes.GlyphOrientationHorizontal}\"");
            }
            if (attributes.GlyphOrientationVertical != null)
            {
                write($"{indent}{nameof(attributes.GlyphOrientationVertical)}: \"{attributes.GlyphOrientationVertical}\"");
            }
            if (attributes.ImageRendering != null)
            {
                write($"{indent}{nameof(attributes.ImageRendering)}: \"{attributes.ImageRendering}\"");
            }
            if (attributes.Kerning != null)
            {
                write($"{indent}{nameof(attributes.Kerning)}: \"{attributes.Kerning}\"");
            }
            if (attributes.LetterSpacing != null)
            {
                write($"{indent}{nameof(attributes.LetterSpacing)}: \"{attributes.LetterSpacing}\"");
            }
            if (attributes.LightingColor != null)
            {
                write($"{indent}{nameof(attributes.LightingColor)}: \"{attributes.LightingColor}\"");
            }
            if (attributes.MarkerEnd != null)
            {
                write($"{indent}{nameof(attributes.MarkerEnd)}: \"{attributes.MarkerEnd}\"");
            }
            if (attributes.MarkerMid != null)
            {
                write($"{indent}{nameof(attributes.MarkerMid)}: \"{attributes.MarkerMid}\"");
            }
            if (attributes.MarkerStart != null)
            {
                write($"{indent}{nameof(attributes.MarkerStart)}: \"{attributes.MarkerStart}\"");
            }
            if (attributes.Mask != null)
            {
                write($"{indent}{nameof(attributes.Mask)}: \"{attributes.Mask}\"");
            }
            if (attributes.Opacity != null)
            {
                write($"{indent}{nameof(attributes.Opacity)}: \"{attributes.Opacity}\"");
            }
            if (attributes.Overflow != null)
            {
                write($"{indent}{nameof(attributes.Overflow)}: \"{attributes.Overflow}\"");
            }
            if (attributes.PointerEvents != null)
            {
                write($"{indent}{nameof(attributes.PointerEvents)}: \"{attributes.PointerEvents}\"");
            }
            if (attributes.ShapeRendering != null)
            {
                write($"{indent}{nameof(attributes.ShapeRendering)}: \"{attributes.ShapeRendering}\"");
            }
            if (attributes.StopColor != null)
            {
                write($"{indent}{nameof(attributes.StopColor)}: \"{attributes.StopColor}\"");
            }
            if (attributes.StopOpacity != null)
            {
                write($"{indent}{nameof(attributes.StopOpacity)}: \"{attributes.StopOpacity}\"");
            }
            if (attributes.Stroke != null)
            {
                write($"{indent}{nameof(attributes.Stroke)}: \"{attributes.Stroke}\"");
            }
            if (attributes.StrokeDasharray != null)
            {
                write($"{indent}{nameof(attributes.StrokeDasharray)}: \"{attributes.StrokeDasharray}\"");
            }
            if (attributes.StrokeDashoffset != null)
            {
                write($"{indent}{nameof(attributes.StrokeDashoffset)}: \"{attributes.StrokeDashoffset}\"");
            }
            if (attributes.StrokeLinecap != null)
            {
                write($"{indent}{nameof(attributes.StrokeLinecap)}: \"{attributes.StrokeLinecap}\"");
            }
            if (attributes.SrokeLinejoin != null)
            {
                write($"{indent}{nameof(attributes.SrokeLinejoin)}: \"{attributes.SrokeLinejoin}\"");
            }
            if (attributes.StrokeMiterlimit != null)
            {
                write($"{indent}{nameof(attributes.StrokeMiterlimit)}: \"{attributes.StrokeMiterlimit}\"");
            }
            if (attributes.StrokeOpacity != null)
            {
                write($"{indent}{nameof(attributes.StrokeOpacity)}: \"{attributes.StrokeOpacity}\"");
            }
            if (attributes.StrokeWidth != null)
            {
                write($"{indent}{nameof(attributes.StrokeWidth)}: \"{attributes.StrokeWidth}\"");
            }
            if (attributes.TextAnchor != null)
            {
                write($"{indent}{nameof(attributes.TextAnchor)}: \"{attributes.TextAnchor}\"");
            }
            if (attributes.TextDecoration != null)
            {
                write($"{indent}{nameof(attributes.TextDecoration)}: \"{attributes.TextDecoration}\"");
            }
            if (attributes.TextRendering != null)
            {
                write($"{indent}{nameof(attributes.TextRendering)}: \"{attributes.TextRendering}\"");
            }
            if (attributes.UnicodeBidi != null)
            {
                write($"{indent}{nameof(attributes.UnicodeBidi)}: \"{attributes.UnicodeBidi}\"");
            }
            if (attributes.Visibility != null)
            {
                write($"{indent}{nameof(attributes.Visibility)}: \"{attributes.Visibility}\"");
            }
            if (attributes.WordSpacing != null)
            {
                write($"{indent}{nameof(attributes.WordSpacing)}: \"{attributes.WordSpacing}\"");
            }
            if (attributes.WritingMode != null)
            {
                write($"{indent}{nameof(attributes.WritingMode)}: \"{attributes.WritingMode}\"");
            }
        }

        public static void PrintTestsAttributes(this ISvgTestsAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.RequiredFeatures != null)
            {
                write($"{indent}{nameof(attributes.RequiredFeatures)}: \"{attributes.RequiredFeatures}\"");
            }
            if (attributes.RequiredExtensions != null)
            {
                write($"{indent}{nameof(attributes.RequiredExtensions)}: \"{attributes.RequiredExtensions}\"");
            }
            if (attributes.SystemLanguage != null)
            {
                write($"{indent}{nameof(attributes.SystemLanguage)}: \"{attributes.SystemLanguage}\"");
            }
        }

        public static void PrintStylableAttributes(this ISvgStylableAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.Class != null)
            {
                write($"{indent}{nameof(attributes.Class)}: \"{attributes.Class}\"");
            }
            if (attributes.Style != null)
            {
                write($"{indent}{nameof(attributes.Style)}: \"{attributes.Style}\"");
            }
        }

        public static void PrintResourcesAttributes(this ISvgResourcesAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.ExternalResourcesRequired != null)
            {
                write($"{indent}{nameof(attributes.ExternalResourcesRequired)}: \"{attributes.ExternalResourcesRequired}\"");
            }
        }

        public static void PrintTransformableAttributes(this ISvgTransformableAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.Transform != null)
            {
                write($"{indent}{nameof(attributes.Transform)}: \"{attributes.Transform}\"");
            }
        }

        public static void PrintXLinkAttributes(this ISvgXLinkAttributes attributes, Action<string> write, string indent)
        {
            if (attributes.Href != null)
            {
                write($"{indent}{nameof(attributes.Href)}: \"{attributes.Href}\"");
            }
            if (attributes.Show != null)
            {
                write($"{indent}{nameof(attributes.Show)}: \"{attributes.Show}\"");
            }
            if (attributes.Actuate != null)
            {
                write($"{indent}{nameof(attributes.Actuate)}: \"{attributes.Actuate}\"");
            }
            if (attributes.Type != null)
            {
                write($"{indent}{nameof(attributes.Type)}: \"{attributes.Type}\"");
            }
            if (attributes.Role != null)
            {
                write($"{indent}{nameof(attributes.Role)}: \"{attributes.Role}\"");
            }
            if (attributes.Arcrole != null)
            {
                write($"{indent}{nameof(attributes.Arcrole)}: \"{attributes.Arcrole}\"");
            }
            if (attributes.Title != null)
            {
                write($"{indent}{nameof(attributes.Title)}: \"{attributes.Title}\"");
            }
        }
    }
}
