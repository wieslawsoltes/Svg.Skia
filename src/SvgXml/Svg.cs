using System.Collections.Generic;
using Xml;

namespace Svg
{
    public interface ISvgCoreAttributes : IElement
    {
        [Attribute("id")]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [Attribute("base")]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [Attribute("lang")]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [Attribute("space")]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }
    }

    public interface ISvgPresentationAttributes : IElement
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
    }

    public interface ISvgTestsAttributes : IElement
    {
        [Attribute("requiredFeatures")]
        public string? RequiredFeatures
        {
            get => GetAttribute("requiredFeatures");
            set => SetAttribute("requiredFeatures", value);
        }

        [Attribute("requiredExtensions")]
        public string? RequiredExtensions
        {
            get => GetAttribute("requiredExtensions");
            set => SetAttribute("requiredExtensions", value);
        }

        [Attribute("systemLanguage")]
        public string? SystemLanguage
        {
            get => GetAttribute("systemLanguage");
            set => SetAttribute("systemLanguage", value);
        }
    }

    public interface ISvgStylableAttributes : IElement
    {
        [Attribute("class")]
        public string? Class
        {
            get => GetAttribute("class");
            set => SetAttribute("class", value);
        }

        [Attribute("style")]
        public string? Style
        {
            get => GetAttribute("style");
            set => SetAttribute("style", value);
        }
    }

    public interface ISvgTransformableAttributes : IElement
    {
        [Attribute("transform")]
        public string? Transform
        {
            get => GetAttribute("transform");
            set => SetAttribute("transform", value);
        }
    }

    public static class SvgAttributes
    {
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
    }

    public abstract class SvgElement : Element, ISvgCoreAttributes
    {

    }

    public class SvgDocument : SvgFragment
    {
    }

    public class SvgElementFactory : IElementFactory
    {
        public ISet<string> Namespaces { get; } = new HashSet<string>
        {
            "http://www.w3.org/2000/svg",
            "http://www.w3.org/1999/xlink",
            "http://www.w3.org/XML/1998/namespace"
        };

        public Element Create(string name)
        {
            return name switch
            {
                "style" => new SvgStyle() { Name = name },
                // Animation
                "animate" => new SvgAnimate() { Name = name },
                "animateColor" => new SvgAnimateColor() { Name = name },
                "animateMotion" => new SvgAnimateMotion() { Name = name },
                "animateTransform" => new SvgAnimateTransform() { Name = name },
                "set" => new SvgSet() { Name = name },
                "mpath" => new SvgMotionPath() { Name = name },
                // Basic Shapes
                "circle" => new SvgCircle() { Name = name },
                "ellipse" => new SvgEllipse() { Name = name },
                "line" => new SvgLine() { Name = name },
                "polygon" => new SvgPolygon() { Name = name },
                "polyline" => new SvgPolyline() { Name = name },
                "rect" => new SvgRectangle() { Name = name },
                // Clipping and Masking
                "clipPath" => new SvgClipPath() { Name = name },
                "mask" => new SvgMask() { Name = name },
                // Color
                "color-profile" => new SvgColorProfile() { Name = name },
                // Document Structure
                "defs" => new SvgDefinitionList() { Name = name },
                "desc" => new SvgDescription() { Name = name },
                "metadata" => new SvgDocumentMetadata() { Name = name },
                "svg" => new SvgFragment() { Name = name },
                "g" => new SvgGroup() { Name = name },
                "image" => new SvgImage() { Name = name },
                "switch" => new SvgSwitch() { Name = name },
                "symbol" => new SvgSymbol() { Name = name },
                "title" => new SvgTitle() { Name = name },
                "use" => new SvgUse() { Name = name },
                // Extensibility
                "foreignObject" => new SvgForeignObject() { Name = name },
                // Filter Effects
                "filter" => new FilterEffects.SvgFilter() { Name = name },
                "feBlend" => new FilterEffects.SvgBlend() { Name = name },
                "feColorMatrix" => new FilterEffects.SvgColourMatrix() { Name = name },
                "feComponentTransfer" => new FilterEffects.SvgComponentTransfer() { Name = name },
                "feComposite" => new FilterEffects.SvgComposite() { Name = name },
                "feConvolveMatrix" => new FilterEffects.SvgConvolveMatrix() { Name = name },
                "feDiffuseLighting" => new FilterEffects.SvgDiffuseLighting() { Name = name },
                "feDisplacementMap" => new FilterEffects.SvgDisplacementMap() { Name = name },
                "feDistantLight" => new FilterEffects.SvgDistantLight() { Name = name },
                "feFlood" => new FilterEffects.SvgFlood() { Name = name },
                "feFuncA" => new FilterEffects.SvgFuncA() { Name = name },
                "feFuncB" => new FilterEffects.SvgFuncB() { Name = name },
                "feFuncG" => new FilterEffects.SvgFuncG() { Name = name },
                "feFuncR" => new FilterEffects.SvgFuncR() { Name = name },
                "feGaussianBlur" => new FilterEffects.SvgGaussianBlur() { Name = name },
                "feImage" => new FilterEffects.SvgImage() { Name = name },
                "feMerge" => new FilterEffects.SvgMerge() { Name = name },
                "feMergeNode" => new FilterEffects.SvgMergeNode() { Name = name },
                "feMorphology" => new FilterEffects.SvgMorphology() { Name = name },
                "feOffset" => new FilterEffects.SvgOffset() { Name = name },
                "fePointLight" => new FilterEffects.SvgPointLight() { Name = name },
                "feSpecularLighting" => new FilterEffects.SvgSpecularLighting() { Name = name },
                "feSpotLight" => new FilterEffects.SvgSpotLight() { Name = name },
                "feTile" => new FilterEffects.SvgTile() { Name = name },
                "feTurbulence" => new FilterEffects.SvgTurbulence() { Name = name },
                // Interactivity
                "cursor" => new SvgCursor() { Name = name },
                // Linking
                "a" => new SvgAnchor() { Name = name },
                "view" => new SvgView() { Name = name },
                // Painting
                "stop" => new SvgGradientStop() { Name = name },
                "linearGradient" => new SvgLinearGradientServer() { Name = name },
                "marker" => new SvgMarker() { Name = name },
                "pattern" => new SvgPatternServer() { Name = name },
                "radialGradient" => new SvgRadialGradientServer() { Name = name },
                // Paths
                "path" => new SvgPath() { Name = name },
                // Scripting
                "script" => new SvgScript() { Name = name },
                // Text
                "altGlyph" => new SvgAltGlyph() { Name = name },
                "altGlyphDef" => new SvgAltGlyphDef() { Name = name },
                "altGlyphItem" => new SvgAltGlyphItem() { Name = name },
                "font" => new SvgFont() { Name = name },
                "font-face" => new SvgFontFace() { Name = name },
                "font-face-format" => new SvgFontFaceFormat() { Name = name },
                "font-face-name" => new SvgFontFaceName() { Name = name },
                "font-face-src" => new SvgFontFaceSrc() { Name = name },
                "font-face-uri" => new SvgFontFaceUri() { Name = name },
                "glyph" => new SvgGlyph() { Name = name },
                "glyphRef" => new SvgGlyphRef() { Name = name },
                "hkern" => new SvgHorizontalKern() { Name = name },
                "vkern" => new SvgVerticalKern() { Name = name },
                "missing-glyph" => new SvgMissingGlyph() { Name = name },
                "text" => new SvgText() { Name = name },
                "textPath" => new SvgTextPath() { Name = name },
                "tref" => new SvgTextRef() { Name = name },
                "tspan" => new SvgTextSpan() { Name = name },
                // Unknown
                _ => new UnknownElement() { Name = name }
            };
        }
    }

    [Element("style")]
    public class SvgStyle : SvgElement
    {
    }

    // Animation

    public abstract class SvgAnimationElement : SvgElement
    {
    }

    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes
    {
    }

    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement, ISvgPresentationAttributes, ISvgTestsAttributes
    {
    }

    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement, ISvgTestsAttributes
    {
    }

    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement, ISvgTestsAttributes
    {
    }

    [Element("set")]
    public class SvgSet : SvgAnimationElement, ISvgTestsAttributes
    {
    }

    [Element("mpath")]
    public class SvgMotionPath : SvgElement
    {
    }

    // Basic Shapes

    public abstract class SvgVisualElement : SvgElement
    {
    }

    public abstract class SvgPathBasedElement : SvgVisualElement
    {
    }

    public abstract class SvgMarkerElement : SvgPathBasedElement
    {
    }

    [Element("circle")]
    public class SvgCircle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("cx")]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy")]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("r")]
        public string? Radius
        {
            get => GetAttribute("r");
            set => SetAttribute("r", value);
        }
    }

    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("cx")]
        public string? CenterX
        {
            get => GetAttribute("cx");
            set => SetAttribute("cx", value);
        }

        [Attribute("cy")]
        public string? CenterY
        {
            get => GetAttribute("cy");
            set => SetAttribute("cy", value);
        }

        [Attribute("rx")]
        public string? RadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry")]
        public string? RadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
        }
    }

    [Element("line")]
    public class SvgLine : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("x1")]
        public string? StartX
        {
            get => GetAttribute("x1");
            set => SetAttribute("x1", value);
        }

        [Attribute("y1")]
        public string? StartY
        {
            get => GetAttribute("y1");
            set => SetAttribute("y1", value);
        }

        [Attribute("x2")]
        public string? EndX
        {
            get => GetAttribute("x2");
            set => SetAttribute("x2", value);
        }

        [Attribute("y2")]
        public string? EndY
        {
            get => GetAttribute("y2");
            set => SetAttribute("y2", value);
        }
    }

    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("points")]
        public string? Points
        {
            get => GetAttribute("points");
            set => SetAttribute("points", value);
        }
    }

    [Element("polyline")]
    public class SvgPolyline : SvgPolygon, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
        [Attribute("x")]
        public string? X
        {
            get => GetAttribute("x");
            set => SetAttribute("x", value);
        }

        [Attribute("y")]
        public string? Y
        {
            get => GetAttribute("y");
            set => SetAttribute("y", value);
        }

        [Attribute("width")]
        public string? Width
        {
            get => GetAttribute("width");
            set => SetAttribute("width", value);
        }

        [Attribute("height")]
        public string? Height
        {
            get => GetAttribute("height");
            set => SetAttribute("height", value);
        }

        [Attribute("rx")]
        public string? CornerRadiusX
        {
            get => GetAttribute("rx");
            set => SetAttribute("rx", value);
        }

        [Attribute("ry")]
        public string? CornerRadiusY
        {
            get => GetAttribute("ry");
            set => SetAttribute("ry", value);
        }
    }

    // Clipping and Masking

    [Element("clipPath")]
    public class SvgClipPath : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("mask")]
    public class SvgMask : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    // Color

    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
    }

    // Document Structure

    [Element("defs")]
    public class SvgDefinitionList : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("desc")]
    public class SvgDescription : SvgElement, ISvgStylableAttributes
    {
    }

    [Element("metadata")]
    public class SvgDocumentMetadata : SvgElement
    {
    }

    [Element("svg")]
    public class SvgFragment : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    [Element("g")]
    public class SvgGroup : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("image")]
    public class SvgImage : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("switch")]
    public class SvgSwitch : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("title")]
    public class SvgTitle : SvgElement, ISvgStylableAttributes
    {
    }

    [Element("use")]
    public class SvgUse : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    // Extensibility

    [Element("foreignObject")]
    public class SvgForeignObject : SvgVisualElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    // Filter Effects

    namespace FilterEffects
    {
        [Element("filter")]
        public class SvgFilter : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        public abstract class SvgFilterPrimitive : SvgElement
        {
        }

        [Element("feBlend")]
        public class SvgBlend : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feColorMatrix")]
        public class SvgColourMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feComponentTransfer")]
        public class SvgComponentTransfer : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feComposite")]
        public class SvgComposite : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feConvolveMatrix")]
        public class SvgConvolveMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feDiffuseLighting")]
        public class SvgDiffuseLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feDisplacementMap")]
        public class SvgDisplacementMap : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feDistantLight")]
        public class SvgDistantLight : SvgElement
        {
        }

        [Element("feFlood")]
        public class SvgFlood : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        public abstract class SvgComponentTransferFunction : SvgElement
        {
        }

        [Element("feFuncA")]
        public class SvgFuncA : SvgComponentTransferFunction
        {
        }

        [Element("feFuncB")]
        public class SvgFuncB : SvgComponentTransferFunction
        {
        }

        [Element("feFuncG")]
        public class SvgFuncG : SvgComponentTransferFunction
        {
        }

        [Element("feFuncR")]
        public class SvgFuncR : SvgComponentTransferFunction
        {
        }

        [Element("feGaussianBlur")]
        public class SvgGaussianBlur : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feImage")]
        public class SvgImage : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feMerge")]
        public class SvgMerge : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feMergeNode")]
        public class SvgMergeNode : SvgElement
        {
        }

        [Element("feMorphology")]
        public class SvgMorphology : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feOffset")]
        public class SvgOffset : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("fePointLight")]
        public class SvgPointLight : SvgElement
        {
        }

        [Element("feSpecularLighting")]
        public class SvgSpecularLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feSpotLight")]
        public class SvgSpotLight : SvgElement
        {
        }

        [Element("feTile")]
        public class SvgTile : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }

        [Element("feTurbulence")]
        public class SvgTurbulence : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
        {
        }
    }

    // Interactivity

    [Element("cursor")]
    public class SvgCursor : SvgElement, ISvgTestsAttributes
    {
    }

    // Linking

    [Element("a")]
    public class SvgAnchor : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("view")]
    public class SvgView : SvgElement
    {
    }

    // Painting

    public abstract class SvgPaintServer : SvgElement
    {
    }

    public class SvgColourServer : SvgPaintServer
    {
    }

    public class SvgDeferredPaintServer : SvgPaintServer
    {
    }

    public abstract class SvgGradientServer : SvgPaintServer
    {
    }

    [Element("stop")]
    public class SvgGradientStop : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStylableAttributes
    {

    }

    // Paths

    [Element("path")]
    public class SvgPath : SvgMarkerElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    // Scripting

    [Element("script")]
    public class SvgScript : SvgElement
    {
    }

    // Text

    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    [Element("altGlyphDef")]
    public class SvgAltGlyphDef : SvgElement
    {
    }

    [Element("altGlyphItem")]
    public class SvgAltGlyphItem : SvgElement
    {
    }

    [Element("font")]
    public class SvgFont : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("font-face")]
    public class SvgFontFace : SvgElement
    {
    }

    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement
    {
    }

    [Element("font-face-name")]
    public class SvgFontFaceName : SvgElement
    {
    }

    [Element("font-face-src")]
    public class SvgFontFaceSrc : SvgElement
    {
    }

    [Element("font-face-uri")]
    public class SvgFontFaceUri : SvgElement
    {
    }

    [Element("glyph")]
    public class SvgGlyph : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    [Element("glyphRef")]
    public class SvgGlyphRef : SvgElement, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    public abstract class SvgKern : Element
    {
    }

    [Element("hkern")]
    public class SvgHorizontalKern : SvgKern
    {
    }

    [Element("vkern")]
    public class SvgVerticalKern : SvgKern
    {
    }

    [Element("missing-glyph")]
    public class SvgMissingGlyph : SvgGlyph, ISvgPresentationAttributes, ISvgStylableAttributes
    {
    }

    public abstract class SvgTextBase : SvgVisualElement
    {
    }

    [Element("text")]
    public class SvgText : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes, ISvgTransformableAttributes
    {
    }

    [Element("textPath")]
    public class SvgTextPath : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    [Element("tref")]
    public class SvgTextRef : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }

    [Element("tspan")]
    public class SvgTextSpan : SvgTextBase, ISvgPresentationAttributes, ISvgTestsAttributes, ISvgStylableAttributes
    {
    }
}
