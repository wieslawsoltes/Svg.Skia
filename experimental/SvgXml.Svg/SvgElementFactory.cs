using System;
using System.Collections.Generic;
using SvgXml.Svg.Animation;
using SvgXml.Svg.BasicShapes;
using SvgXml.Svg.ClippingAndMasking;
using SvgXml.Svg.Color;
using SvgXml.Svg.DocumentStructure;
using SvgXml.Svg.Extensibility;
using SvgXml.Svg.Interactivity;
using SvgXml.Svg.Linking;
using SvgXml.Svg.Painting;
using SvgXml.Svg.Paths;
using SvgXml.Svg.Scripting;
using SvgXml.Svg.Styling;
using SvgXml.Svg.Text;
using SvgXml.Xml;
using SvgXml.Xml.Elements;

namespace SvgXml.Svg
{
    public class SvgElementFactory : IElementFactory
    {
        // https://www.w3.org/TR/SVG11/styling.html#SVGStylingProperties
        // https://www.w3.org/TR/SVG11/propidx.html
        public Dictionary<string, Type> Types { get; } = new Dictionary<string, Type>()
        {
            // Animation
            ["animate"] = typeof(SvgAnimate),
            ["animateColor"] = typeof(SvgAnimateColor),
            ["animateMotion"] = typeof(SvgAnimateMotion),
            ["animateTransform"] = typeof(SvgAnimateTransform),
            ["set"] = typeof(SvgSet),
            ["mpath"] = typeof(SvgMotionPath),
            // Basic Shapes
            ["circle"] = typeof(SvgCircle),
            ["ellipse"] = typeof(SvgEllipse),
            ["line"] = typeof(SvgLine),
            ["polygon"] = typeof(SvgPolygon),
            ["polyline"] = typeof(SvgPolyline),
            ["rect"] = typeof(SvgRectangle),
            // Clipping and Masking
            ["clipPath"] = typeof(SvgClipPath),
            ["mask"] = typeof(SvgMask),
            // Color
            ["color-profile"] = typeof(SvgColorProfile),
            // Document Structure
            ["defs"] = typeof(SvgDefinitionList),
            ["desc"] = typeof(SvgDescription),
            ["metadata"] = typeof(SvgDocumentMetadata),
            ["svg"] = typeof(SvgDocument),
            ["g"] = typeof(SvgGroup),
            ["image"] = typeof(SvgImage),
            ["switch"] = typeof(SvgSwitch),
            ["symbol"] = typeof(SvgSymbol),
            ["title"] = typeof(SvgTitle),
            ["use"] = typeof(SvgUse),
            // Extensibility
            ["foreignObject"] = typeof(SvgForeignObject),
            // Filter Effects
            ["filter"] = typeof(FilterEffects.SvgFilter),
            ["feBlend"] = typeof(FilterEffects.Primitives.SvgBlend),
            ["feColorMatrix"] = typeof(FilterEffects.Primitives.SvgColourMatrix),
            ["feComponentTransfer"] = typeof(FilterEffects.Primitives.SvgComponentTransfer),
            ["feComposite"] = typeof(FilterEffects.Primitives.SvgComposite),
            ["feConvolveMatrix"] = typeof(FilterEffects.Primitives.SvgConvolveMatrix),
            ["feDiffuseLighting"] = typeof(FilterEffects.Primitives.SvgDiffuseLighting),
            ["feDisplacementMap"] = typeof(FilterEffects.Primitives.SvgDisplacementMap),
            ["feDistantLight"] = typeof(FilterEffects.Primitives.SvgDistantLight),
            ["feFlood"] = typeof(FilterEffects.Primitives.SvgFlood),
            ["feFuncA"] = typeof(FilterEffects.Primitives.SvgFuncA),
            ["feFuncB"] = typeof(FilterEffects.Primitives.SvgFuncB),
            ["feFuncG"] = typeof(FilterEffects.Primitives.SvgFuncG),
            ["feFuncR"] = typeof(FilterEffects.Primitives.SvgFuncR),
            ["feGaussianBlur"] = typeof(FilterEffects.Primitives.SvgGaussianBlur),
            ["feImage"] = typeof(FilterEffects.Primitives.SvgImage),
            ["feMerge"] = typeof(FilterEffects.Primitives.SvgMerge),
            ["feMergeNode"] = typeof(FilterEffects.Primitives.SvgMergeNode),
            ["feMorphology"] = typeof(FilterEffects.Primitives.SvgMorphology),
            ["feOffset"] = typeof(FilterEffects.Primitives.SvgOffset),
            ["fePointLight"] = typeof(FilterEffects.Primitives.SvgPointLight),
            ["feSpecularLighting"] = typeof(FilterEffects.Primitives.SvgSpecularLighting),
            ["feSpotLight"] = typeof(FilterEffects.Primitives.SvgSpotLight),
            ["feTile"] = typeof(FilterEffects.Primitives.SvgTile),
            ["feTurbulence"] = typeof(FilterEffects.Primitives.SvgTurbulence),
            // Interactivity
            ["cursor"] = typeof(SvgCursor),
            // Linking
            ["a"] = typeof(SvgAnchor),
            ["view"] = typeof(SvgView),
            // Painting
            ["stop"] = typeof(SvgGradientStop),
            ["linearGradient"] = typeof(SvgLinearGradientServer),
            ["marker"] = typeof(SvgMarker),
            ["pattern"] = typeof(SvgPatternServer),
            ["radialGradient"] = typeof(SvgRadialGradientServer),
            // Paths
            ["path"] = typeof(SvgPath),
            // Scripting
            ["script"] = typeof(SvgScript),
            // Styling
            ["style"] = typeof(SvgStyle),
            // Text
            ["altGlyph"] = typeof(SvgAltGlyph),
            ["altGlyphDef"] = typeof(SvgAltGlyphDef),
            ["altGlyphItem"] = typeof(SvgAltGlyphItem),
            ["font"] = typeof(SvgFont),
            ["font-face"] = typeof(SvgFontFace),
            ["font-face-format"] = typeof(SvgFontFaceFormat),
            ["font-face-name"] = typeof(SvgFontFaceName),
            ["font-face-src"] = typeof(SvgFontFaceSrc),
            ["font-face-uri"] = typeof(SvgFontFaceUri),
            ["glyph"] = typeof(SvgGlyph),
            ["glyphRef"] = typeof(SvgGlyphRef),
            ["hkern"] = typeof(SvgHorizontalKern),
            ["vkern"] = typeof(SvgVerticalKern),
            ["missing-glyph"] = typeof(SvgMissingGlyph),
            ["text"] = typeof(SvgText),
            ["textPath"] = typeof(SvgTextPath),
            ["tref"] = typeof(SvgTextRef),
            ["tspan"] = typeof(SvgTextSpan)
        };

        public ISet<string> Namespaces { get; } = new HashSet<string>
        {
            SvgElement.SvgNamespace,
            SvgElement.XLinkNamespace,
            SvgElement.XmlNamespace
        };

        public Element Create(string tag, IElement? parent)
        {
            return tag switch
            {
                // Animation
                "animate" => new SvgAnimate() { Tag = tag },
                "animateColor" => new SvgAnimateColor() { Tag = tag },
                "animateMotion" => new SvgAnimateMotion() { Tag = tag },
                "animateTransform" => new SvgAnimateTransform() { Tag = tag },
                "set" => new SvgSet() { Tag = tag },
                "mpath" => new SvgMotionPath() { Tag = tag },
                // Basic Shapes
                "circle" => new SvgCircle() { Tag = tag },
                "ellipse" => new SvgEllipse() { Tag = tag },
                "line" => new SvgLine() { Tag = tag },
                "polygon" => new SvgPolygon() { Tag = tag },
                "polyline" => new SvgPolyline() { Tag = tag },
                "rect" => new SvgRectangle() { Tag = tag },
                // Clipping and Masking
                "clipPath" => new SvgClipPath() { Tag = tag },
                "mask" => new SvgMask() { Tag = tag },
                // Color
                "color-profile" => new SvgColorProfile() { Tag = tag },
                // Document Structure
                "defs" => new SvgDefinitionList() { Tag = tag },
                "desc" => new SvgDescription() { Tag = tag },
                "metadata" => new SvgDocumentMetadata() { Tag = tag },
                "svg" => (parent == null) ? new SvgDocument() { Tag = tag } : new SvgFragment() { Tag = tag },
                "g" => new SvgGroup() { Tag = tag },
                "image" => new SvgImage() { Tag = tag },
                "switch" => new SvgSwitch() { Tag = tag },
                "symbol" => new SvgSymbol() { Tag = tag },
                "title" => new SvgTitle() { Tag = tag },
                "use" => new SvgUse() { Tag = tag },
                // Extensibility
                "foreignObject" => new SvgForeignObject() { Tag = tag },
                // Filter Effects
                "filter" => new FilterEffects.SvgFilter() { Tag = tag },
                "feBlend" => new FilterEffects.Primitives.SvgBlend() { Tag = tag },
                "feColorMatrix" => new FilterEffects.Primitives.SvgColourMatrix() { Tag = tag },
                "feComponentTransfer" => new FilterEffects.Primitives.SvgComponentTransfer() { Tag = tag },
                "feComposite" => new FilterEffects.Primitives.SvgComposite() { Tag = tag },
                "feConvolveMatrix" => new FilterEffects.Primitives.SvgConvolveMatrix() { Tag = tag },
                "feDiffuseLighting" => new FilterEffects.Primitives.SvgDiffuseLighting() { Tag = tag },
                "feDisplacementMap" => new FilterEffects.Primitives.SvgDisplacementMap() { Tag = tag },
                "feDistantLight" => new FilterEffects.Primitives.SvgDistantLight() { Tag = tag },
                "feFlood" => new FilterEffects.Primitives.SvgFlood() { Tag = tag },
                "feFuncA" => new FilterEffects.Primitives.SvgFuncA() { Tag = tag },
                "feFuncB" => new FilterEffects.Primitives.SvgFuncB() { Tag = tag },
                "feFuncG" => new FilterEffects.Primitives.SvgFuncG() { Tag = tag },
                "feFuncR" => new FilterEffects.Primitives.SvgFuncR() { Tag = tag },
                "feGaussianBlur" => new FilterEffects.Primitives.SvgGaussianBlur() { Tag = tag },
                "feImage" => new FilterEffects.Primitives.SvgImage() { Tag = tag },
                "feMerge" => new FilterEffects.Primitives.SvgMerge() { Tag = tag },
                "feMergeNode" => new FilterEffects.Primitives.SvgMergeNode() { Tag = tag },
                "feMorphology" => new FilterEffects.Primitives.SvgMorphology() { Tag = tag },
                "feOffset" => new FilterEffects.Primitives.SvgOffset() { Tag = tag },
                "fePointLight" => new FilterEffects.Primitives.SvgPointLight() { Tag = tag },
                "feSpecularLighting" => new FilterEffects.Primitives.SvgSpecularLighting() { Tag = tag },
                "feSpotLight" => new FilterEffects.Primitives.SvgSpotLight() { Tag = tag },
                "feTile" => new FilterEffects.Primitives.SvgTile() { Tag = tag },
                "feTurbulence" => new FilterEffects.Primitives.SvgTurbulence() { Tag = tag },
                // Interactivity
                "cursor" => new SvgCursor() { Tag = tag },
                // Linking
                "a" => new SvgAnchor() { Tag = tag },
                "view" => new SvgView() { Tag = tag },
                // Painting
                "stop" => new SvgGradientStop() { Tag = tag },
                "linearGradient" => new SvgLinearGradientServer() { Tag = tag },
                "marker" => new SvgMarker() { Tag = tag },
                "pattern" => new SvgPatternServer() { Tag = tag },
                "radialGradient" => new SvgRadialGradientServer() { Tag = tag },
                // Paths
                "path" => new SvgPath() { Tag = tag },
                // Scripting
                "script" => new SvgScript() { Tag = tag },
                // Styling
                "style" => new SvgStyle() { Tag = tag },
                // Text
                "altGlyph" => new SvgAltGlyph() { Tag = tag },
                "altGlyphDef" => new SvgAltGlyphDef() { Tag = tag },
                "altGlyphItem" => new SvgAltGlyphItem() { Tag = tag },
                "font" => new SvgFont() { Tag = tag },
                "font-face" => new SvgFontFace() { Tag = tag },
                "font-face-format" => new SvgFontFaceFormat() { Tag = tag },
                "font-face-name" => new SvgFontFaceName() { Tag = tag },
                "font-face-src" => new SvgFontFaceSrc() { Tag = tag },
                "font-face-uri" => new SvgFontFaceUri() { Tag = tag },
                "glyph" => new SvgGlyph() { Tag = tag },
                "glyphRef" => new SvgGlyphRef() { Tag = tag },
                "hkern" => new SvgHorizontalKern() { Tag = tag },
                "vkern" => new SvgVerticalKern() { Tag = tag },
                "missing-glyph" => new SvgMissingGlyph() { Tag = tag },
                "text" => new SvgText() { Tag = tag },
                "textPath" => new SvgTextPath() { Tag = tag },
                "tref" => new SvgTextRef() { Tag = tag },
                "tspan" => new SvgTextSpan() { Tag = tag },
                // Unknown
                _ => new UnknownElement() { Tag = tag }
            };
        }
    }
}
