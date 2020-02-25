using System.Collections.Generic;
using Xml;

namespace Svg
{
    public class SvgElementFactory : IElementFactory
    {
        public ISet<string> Namespaces { get; } = new HashSet<string>
        {
            "http://www.w3.org/2000/svg",
            "http://www.w3.org/1999/xlink",
            "http://www.w3.org/XML/1998/namespace"
        };

        public Element Create(string name, IElement? parent)
        {
            return name switch
            {
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
                "svg" => (parent == null) ? new SvgDocument() { Name = name } : new SvgFragment() { Name = name },
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
                // Styling
                "style" => new SvgStyle() { Name = name },
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
}
