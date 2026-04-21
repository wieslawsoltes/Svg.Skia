namespace CodeGenerator;

internal static class SvgTypeDefs
{
    // svg classes definitions
    public static readonly TypeDef[] TypeDefs =
    [
        #region Basic Shapes

        new(TargetTpe: "circle",
            IsAbstract: false,
            BaseType: "path-based",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("cx", "Svg.SvgUnit"),
                new ("cy", "Svg.SvgUnit"),
                new ("r", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "ellipse",
            IsAbstract: false,
            BaseType: "path-based",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("cx", "Svg.SvgUnit"),
                new ("cy", "Svg.SvgUnit"),
                new ("rx", "Svg.SvgUnit"),
                new ("ry", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "line",
            IsAbstract: false,
            BaseType: "marker-element",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("x1", "Svg.SvgUnit"),
                new ("y1", "Svg.SvgUnit"),
                new ("x2", "Svg.SvgUnit"),
                new ("y2", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "marker-element",
            IsAbstract: true,
            BaseType: "path-based",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("marker"),
                new ("marker-end"),
                new ("marker-mid"),
                new ("marker-start"),
            ]),

        new(TargetTpe: "path-based",
            IsAbstract: true,
            BaseType: "visual",
            FilePath: "Basic Shapes",
            Properties:
            [
            ]),

        new(TargetTpe: "polygon",
            IsAbstract: false,
            BaseType: "marker-element",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("points"),
            ]),

        new(TargetTpe: "polyline",
            IsAbstract: false,
            BaseType: "polygon",
            FilePath: "Basic Shapes",
            Properties:
            [
            ]),

        new(TargetTpe: "rect",
            IsAbstract: false,
            BaseType: "path-based",
            FilePath: "Basic Shapes",
            Properties:
            [
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
                new ("rx", "Svg.SvgUnit"),
                new ("ry", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "visual",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Basic Shapes",
            Properties:
            [
                // visual
                new ("clip"),
                new ("clip-path"),
                new ("clip-rule"),
                new ("filter"),
                new ("pointer-events", "Svg.SvgPointerEvents"),
                // style
                new ("enable-background"),
            ]),

        #endregion

        #region Clipping and Masking

        new(TargetTpe: "clipPath",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Clipping and Masking",
            Properties:
            [
                new ("clipPathUnits"),
            ]),

        new(TargetTpe: "mask",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Clipping and Masking",
            Properties:
            [
                new ("maskUnits"),
                new ("maskContentUnits"),
                new ("x"),
                new ("y"),
                new ("width"),
                new ("height"),
            ]),
        
        #endregion

        #region Document Structure

        new(TargetTpe: "defs",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Document Structure",
            Properties:
            [
            ]),

        new(TargetTpe: "desc",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Document Structure",
            Properties:
            [
            ]),

        new(TargetTpe: "g",
            IsAbstract: false,
            BaseType: "marker-element",
            FilePath: "Document Structure",
            Properties:
            [
            ]),

        new(TargetTpe: "image",
            IsAbstract: false,
            BaseType: "visual",
            FilePath: "Document Structure",
            Properties:
            [
                new ("preserveAspectRatio"),
                new ("x"),
                new ("y"),
                new ("width"),
                new ("height"),
                new ("href"),
            ]),

        new(TargetTpe: "svg",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Document Structure",
            Properties:
            [
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
                new ("overflow"),
                new ("viewBox"),
                new ("preserveAspectRatio"),
                new ("font-size", "Svg.SvgUnit"),
                new ("font-family"),
            ]),

        new(TargetTpe: "switch",
            IsAbstract: false,
            BaseType: "visual",
            FilePath: "Document Structure",
            Properties:
            [
            ]),

        new(TargetTpe: "symbol",
            IsAbstract: false,
            BaseType: "visual",
            FilePath: "Document Structure",
            Properties:
            [
                new ("viewBox"),
                new ("preserveAspectRatio"),
            ]),

        new(TargetTpe: "title",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Document Structure",
            Properties:
            [
            ]),

        new(TargetTpe: "use",
            IsAbstract: false,
            BaseType: "visual",
            FilePath: "Document Structure",
            Properties:
            [
                new ("href"),
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
            ]),

        #endregion

        #region Animation

        new(TargetTpe: "animation-element",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Animation",
            Properties:
            [
                new ("href"),
                new ("requiredFeatures"),
                new ("requiredExtensions"),
                new ("systemLanguage"),
                new ("externalResourcesRequired", "bool"),
                new ("begin"),
                new ("dur"),
                new ("end"),
                new ("min"),
                new ("max"),
                new ("restart", "Svg.SvgAnimationRestart"),
                new ("repeatCount"),
                new ("repeatDur"),
                new ("fill", "Svg.SvgAnimationFill"),
                new ("onbegin"),
                new ("onend"),
                new ("onrepeat"),
                new ("onload"),
            ]),

        new(TargetTpe: "animation-attribute-element",
            IsAbstract: true,
            BaseType: "animation-element",
            FilePath: "Animation",
            Properties:
            [
                new ("attributeName"),
                new ("attributeType", "Svg.SvgAnimationAttributeType"),
            ]),

        new(TargetTpe: "animation-value-element",
            IsAbstract: true,
            BaseType: "animation-attribute-element",
            FilePath: "Animation",
            Properties:
            [
                new ("calcMode", "Svg.SvgAnimationCalcMode"),
                new ("values"),
                new ("keyTimes", "numbers"),
                new ("keySplines"),
                new ("from"),
                new ("to"),
                new ("by"),
                new ("additive", "Svg.SvgAnimationAdditive"),
                new ("accumulate", "Svg.SvgAnimationAccumulate"),
            ]),

        new(TargetTpe: "animate",
            IsAbstract: false,
            BaseType: "animation-value-element",
            FilePath: "Animation",
            Properties:
            [
            ]),

        new(TargetTpe: "set",
            IsAbstract: false,
            BaseType: "animation-attribute-element",
            FilePath: "Animation",
            Properties:
            [
                new ("to"),
            ]),

        new(TargetTpe: "animateMotion",
            IsAbstract: false,
            BaseType: "animation-element",
            FilePath: "Animation",
            Properties:
            [
                new ("calcMode", "Svg.SvgAnimationCalcMode"),
                new ("values"),
                new ("keyTimes", "numbers"),
                new ("keySplines"),
                new ("from"),
                new ("to"),
                new ("by"),
                new ("additive", "Svg.SvgAnimationAdditive"),
                new ("accumulate", "Svg.SvgAnimationAccumulate"),
                new ("path"),
                new ("keyPoints", "numbers"),
                new ("rotate"),
                new ("origin"),
            ]),

        new(TargetTpe: "animateColor",
            IsAbstract: false,
            BaseType: "animation-value-element",
            FilePath: "Animation",
            Properties:
            [
            ]),

        new(TargetTpe: "animateTransform",
            IsAbstract: false,
            BaseType: "animation-value-element",
            FilePath: "Animation",
            Properties:
            [
                new ("type", "Svg.SvgAnimateTransformType"),
            ]),

        new(TargetTpe: "mpath",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Animation",
            Properties:
            [
                new ("href"),
                new ("externalResourcesRequired", "bool"),
            ]),

        #endregion

        #region Extensibility

        new(TargetTpe: "foreignObject",
            IsAbstract: false,
            BaseType: "visual",
            FilePath: "Extensibility",
            Properties:
            [
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
            ]),
        
        #endregion

        #region Filter Effects

        new(TargetTpe: "feBlend",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feBlend",
            Properties:
            [
                new ("mode", "blend-mode"),
                new ("in2"),
            ]),

        new(TargetTpe: "feColorMatrix",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feColorMatrix",
            Properties:
            [
                new ("type", "type-feColorMatrix"),
                new ("values"),
            ]),

        new(TargetTpe: "component-transfer-function",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Filter Effects/feComponentTransfer",
            Properties:
            [
                new ("type", "type-component-transfer-function"),
                new ("tableValues", "numbers"),
                new ("slope", "float"),
                new ("intercept", "float"),
                new ("amplitude", "float"),
                new ("exponent", "float"),
                new ("offset", "float"),
            ]),

        new(TargetTpe: "feComponentTransfer",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feComponentTransfer",
            Properties:
            [
            ]),

        new(TargetTpe: "feComposite",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feComposite",
            Properties:
            [
                new ("operator", "operator"),
                new ("k1", "float"),
                new ("k2", "float"),
                new ("k3", "float"),
                new ("k4", "float"),
                new ("in2"),
            ]),

        new(TargetTpe: "feConvolveMatrix",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feConvolveMatrix",
            Properties:
            [
                new ("order", "numbers"),
                new ("kernelMatrix", "numbers"),
                new ("divisor", "float"),
                new ("bias", "float"),
                new ("targetX", "int"),
                new ("targetY", "int"),
                new ("edgeMode"),
                new ("kernelUnitLength", "numbers"),
                new ("preserveAlpha"),
            ]),

        new(TargetTpe: "feDiffuseLighting",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feDiffuseLighting",
            Properties:
            [
                new ("surfaceScale", "float"),
                new ("diffuseConstant", "float"),
                new ("kernelUnitLength", "numbers"),
                new ("lighting-color"),
            ]),

        new(TargetTpe: "feDisplacementMap",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feDisplacementMap",
            Properties:
            [
                new ("scale", "float"),
                new ("xChannelSelector"),
                new ("yChannelSelector"),
                new ("in2"),
            ]),

        new(TargetTpe: "feDistantLight",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Filter Effects/feDistantLight",
            Properties:
            [
                new ("azimuth"),
                new ("elevation"),
            ]),

        new(TargetTpe: "feFlood",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feFlood",
            Properties:
            [
                new ("flood-color"),
                new ("flood-opacity"),
            ]),

        new(TargetTpe: "feFuncA",
            IsAbstract: false,
            BaseType: "component-transfer-function",
            FilePath: "Filter Effects/feFuncA",
            Properties:
            [
            ]),

        new(TargetTpe: "feFuncB",
            IsAbstract: false,
            BaseType: "component-transfer-function",
            FilePath: "Filter Effects/feFuncB",
            Properties:
            [
            ]),

        new(TargetTpe: "feFuncG",
            IsAbstract: false,
            BaseType: "component-transfer-function",
            FilePath: "Filter Effects/feFuncG",
            Properties:
            [
            ]),

        new(TargetTpe: "feFuncR",
            IsAbstract: false,
            BaseType: "component-transfer-function",
            FilePath: "Filter Effects/feFuncR",
            Properties:
            [
            ]),

        new(TargetTpe: "feGaussianBlur",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feGaussianBlur",
            Properties:
            [
                new ("stdDeviation", "numbers"),
            ]),

        new(TargetTpe: "feImage",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feImage",
            Properties:
            [
                new ("href"),
                new ("preserveAspectRatio"),
            ]),

        new(TargetTpe: "feMerge",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feMerge",
            Properties:
            [
            ]),

        new(TargetTpe: "feMergeNode",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Filter Effects/feMerge",
            Properties:
            [
                new ("in"),
            ]),

        new(TargetTpe: "feMorphology",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feMorphology",
            Properties:
            [
                new ("operator"),
                new ("radius", "numbers"),
            ]),

        new(TargetTpe: "feOffset",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feOffset",
            Properties:
            [
                new ("dx", "Svg.SvgUnit"),
                new ("dy", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "fePointLight",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Filter Effects/fePointLight",
            Properties:
            [
                new ("x", "float"),
                new ("y", "float"),
                new ("z", "float"),
            ]),

        new(TargetTpe: "feSpecularLighting",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feSpecularLighting",
            Properties:
            [
                new ("surfaceScale", "float"),
                new ("specularConstant", "float"),
                new ("specularExponent", "float"),
                new ("kernelUnitLength", "numbers"),
                new ("lighting-color"),
            ]),

        new(TargetTpe: "feSpotLight",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Filter Effects/feSpotLight",
            Properties:
            [
                new ("x", "float"),
                new ("y", "float"),
                new ("z", "float"),
                new ("pointsAtX", "float"),
                new ("pointsAtY", "float"),
                new ("pointsAtZ", "float"),
                new ("specularExponent", "float"),
                new ("limitingConeAngle", "float"),
            ]),

        new(TargetTpe: "feTile",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feTile",
            Properties:
            [
            ]),

        new(TargetTpe: "feTurbulence",
            IsAbstract: false,
            BaseType: "filter-primitive",
            FilePath: "Filter Effects/feTurbulence",
            Properties:
            [
                new ("baseFrequency", "numbers"),
                new ("numOctaves", "int"),
                new ("seed", "float"),
                new ("stitchTiles"),
                new ("type"),
            ]),

        new(TargetTpe: "filter",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Filter Effects",
            Properties:
            [
                new ("filterUnits"),
                new ("primitiveUnits"),
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
                new ("href"),
            ]),

        new(TargetTpe: "filter-primitive",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Filter Effects",
            Properties:
            [
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
                new ("in"),
                new ("result"),
            ]),

        #endregion

        #region Linking

        new(TargetTpe: "a",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Linking",
            Properties:
            [
                new ("href"),
                new ("show"),
                new ("title"),
                new ("target"),
            ]),
        
        #endregion

        #region Metadata

        new(TargetTpe: "metadata",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Metadata",
            Properties:
            [
            ]),
        
        #endregion

        #region Painting

        new(TargetTpe: "gradient",
            IsAbstract: true,
            BaseType: "paint",
            FilePath: "Painting",
            Properties:
            [
                new ("spreadMethod"),
                new ("gradientUnits"),
                new ("href"),
                new ("gradientTransform"),
                new ("stop-color"),
                new ("stop-opacity"),
            ]),

        new(TargetTpe: "linearGradient",
            IsAbstract: false,
            BaseType: "gradient",
            FilePath: "Painting",
            Properties:
            [
                new ("x1", "Svg.SvgUnit"),
                new ("y1", "Svg.SvgUnit"),
                new ("x2", "Svg.SvgUnit"),
                new ("y2", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "marker",
            IsAbstract: false,
            BaseType: "path-based",
            FilePath: "Painting",
            Properties:
            [
                new ("refX", "Svg.SvgUnit"),
                new ("refY", "Svg.SvgUnit"),
                new ("orient"),
                new ("overflow"),
                new ("viewBox"),
                new ("preserveAspectRatio"),
                new ("markerWidth", "Svg.SvgUnit"),
                new ("markerHeight", "Svg.SvgUnit"),
                new ("markerUnits"),
            ]),

        new(TargetTpe: "paint",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Painting",
            Properties:
            [
            ]),

        new(TargetTpe: "pattern",
            IsAbstract: false,
            BaseType: "paint",
            FilePath: "Painting",
            Properties:
            [
                new ("x", "Svg.SvgUnit"),
                new ("y", "Svg.SvgUnit"),
                new ("width", "Svg.SvgUnit"),
                new ("height", "Svg.SvgUnit"),
                new ("patternUnits"),
                new ("patternContentUnits"),
                new ("viewBox"),
                new ("href"),
                new ("overflow"),
                new ("preserveAspectRatio"),
                new ("patternTransform"),
            ]),

        new(TargetTpe: "radialGradient",
            IsAbstract: false,
            BaseType: "gradient",
            FilePath: "Painting",
            Properties:
            [
                new ("cx", "Svg.SvgUnit"),
                new ("cy", "Svg.SvgUnit"),
                new ("r", "Svg.SvgUnit"),
                new ("fx", "Svg.SvgUnit"),
                new ("fy", "Svg.SvgUnit"),
                new ("fr", "Svg.SvgUnit"),
            ]),

        new(TargetTpe: "stop",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Painting",
            Properties:
            [
                new ("offset", "Svg.SvgUnit"),
                new ("stop-color"),
                new ("stop-opacity"),
            ]),

        #endregion

        #region Paths

        new(TargetTpe: "path",
            IsAbstract: false,
            BaseType: "marker-element",
            FilePath: "Paths",
            Properties:
            [
                new ("d"),
                new ("pathLength", "float"),
            ]),
        
        #endregion

        #region Scripting

        new(TargetTpe: "script",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Scripting",
            Properties:
            [
                new ("type"),
                new ("crossorigin"),
                new ("href"),
            ]),

        #endregion
        
        #region Text

        new(TargetTpe: "font",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Text",
            Properties:
            [
                new ("horiz-adv-x", "float"),
                new ("horiz-origin-x", "float"),
                new ("horiz-origin-y", "float"),
                new ("vert-adv-y", "float"),
                new ("vert-origin-x", "float"),
                new ("vert-origin-y", "float"),
            ]),

        new(TargetTpe: "font-face",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Text",
            Properties:
            [
                new ("alphabetic", "float"),
                new ("ascent", "float"),
                new ("ascent-height", "float"),
                new ("descent", "float"),
                new ("panose-1"),
                new ("units-per-em", "float"),
                new ("x-height", "float"),
            ]),

        new(TargetTpe: "font-face-src",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Text",
            Properties:
            [
            ]),

        new(TargetTpe: "font-face-uri",
            IsAbstract: false,
            BaseType: "element",
            FilePath: "Text",
            Properties:
            [
                new ("href"),
            ]),

        new(TargetTpe: "glyph",
            IsAbstract: false,
            BaseType: "path-based",
            FilePath: "Text",
            Properties:
            [
                new ("d"),
                new ("glyph-name"),
                new ("horiz-adv-x", "float"),
                new ("unicode"),
                new ("vert-adv-y", "float"),
                new ("vert-origin-x", "float"),
                new ("vert-origin-y", "float"),
            ]),

        new(TargetTpe: "hkern",
            IsAbstract: false,
            BaseType: "kern",
            FilePath: "Text",
            Properties:
            [
            ]),

        new(TargetTpe: "kern",
            IsAbstract: true,
            BaseType: "element",
            FilePath: "Text",
            Properties:
            [
                new ("g1"),
                new ("g2"),
                new ("u1"),
                new ("u2"),
                new ("k", "float"),
            ]),

        new(TargetTpe: "missing-glyph",
            IsAbstract: false,
            BaseType: "glyph",
            FilePath: "Text",
            Properties:
            [
                new ("glyph-name"),
            ]),

        new(TargetTpe: "text",
            IsAbstract: false,
            BaseType: "text-base",
            FilePath: "Text",
            Properties:
            [
            ]),

        new(TargetTpe: "text-base",
            IsAbstract: true,
            BaseType: "visual",
            FilePath: "Text",
            Properties:
            [
                new ("x"),
                new ("dx"),
                new ("y"),
                new ("dy"),
                new ("rotate"),
                new ("textLength", "Svg.SvgUnit"),
                new ("lengthAdjust"),
                new ("letter-spacing", "Svg.SvgUnit"),
                new ("word-spacing", "Svg.SvgUnit"),
                new ("onchange"),
            ]),

        new(TargetTpe: "textPath",
            IsAbstract: false,
            BaseType: "text-base",
            FilePath: "Text",
            Properties:
            [
                new ("startOffset", "Svg.SvgUnit"),
                new ("method"),
                new ("spacing"),
                new ("href"),
            ]),

        new(TargetTpe: "tref",
            IsAbstract: false,
            BaseType: "text-base",
            FilePath: "Text",
            Properties:
            [
                new ("href"),
            ]),

        new(TargetTpe: "tspan",
            IsAbstract: false,
            BaseType: "text-base",
            FilePath: "Text",
            Properties:
            [
            ]),

        new(TargetTpe: "vkern",
            IsAbstract: false,
            BaseType: "kern",
            FilePath: "Text",
            Properties:
            [
            ]),

        #endregion

        #region element

        new(TargetTpe: "element",
            IsAbstract: true,
            BaseType: "",
            FilePath: "",
            Properties:
            [
                // element
                new ("style"),
                new ("color"),
                new ("transform"),
                new ("id"),
                new ("sp"),
                new ("onclick"),
                new ("onmousedown"),
                new ("onmouseup"),
                new ("onmousemove"),
                new ("onmousescroll"),
                new ("onmouseover"),
                new ("onmouseout"),
                // style
                new ("fill"),
                new ("stroke"),
                new ("fill-rule", "fill-rule"),
                new ("fill-opacity", "float"),
                new ("stroke-width", "Svg.SvgUnit"),
                new ("stroke-linecap"),
                new ("stroke-linejoin"),
                new ("stroke-miterlimit", "float"),
                new ("stroke-dasharray"),
                new ("stroke-dashoffset", "Svg.SvgUnit"),
                new ("stroke-opacity", "float"),
                new ("opacity", "float"),
                new ("shape-rendering"),
                new ("color-interpolation"),
                new ("color-interpolation-filters"),
                new ("visibility"),
                new ("display"),
                new ("text-anchor"),
                new ("baseline-shift"),
                new ("font-family"),
                new ("font-size", "Svg.SvgUnit"),
                new ("font-style"),
                new ("font-variant"),
                new ("text-decoration"),
                new ("font-weight"),
                new ("font-stretch"),
                new ("text-transform"),
                new ("font"),
            ]),
        
        #endregion
    ];
}
