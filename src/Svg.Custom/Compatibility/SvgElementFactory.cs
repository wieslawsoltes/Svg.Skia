using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml;
using Svg.Helpers;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg
{
    /// <summary>
    /// Provides the methods required in order to parse and create <see cref="SvgElement"/> instances from XML.
    ///
    /// This Svg.Custom copy intentionally diverges from the upstream SVG parser in one narrow
    /// place: raw presentation attributes of the form <c>marker="..."</c> are ignored, while
    /// stylesheet and inline-style <c>marker</c> declarations still use the original CSS path.
    /// The W3C marker tests expect that distinction, but the original upstream logic treated the
    /// presentation-attribute shorthand the same as CSS shorthand. We keep the behavioral change
    /// here so the reason stays documented next to the override and the submodule remains clean.
    /// </summary>
    [ElementFactory]
    internal partial class SvgElementFactory
    {
        private const string RawTextDecorationAttributeKey = "__svgskia:text-decoration-raw";
        private const int SharedPathDataPrototypeCacheLimit = 1024;
        private static readonly char[] ViewBoxSplitChars = { ' ', '\t', '\n', '\r', ',' };
        private static readonly ConcurrentDictionary<string, SharedPathDataPrototypeEntry> SharedPathDataPrototypeCache =
            new(StringComparer.Ordinal);
        private readonly SvgInlineStyleAttributeParser inlineStyleAttributeParser = new();
        private readonly bool eagerApplyCompatibilityStyles;

        public SvgElementFactory(bool eagerApplyCompatibilityStyles = false)
        {
            this.eagerApplyCompatibilityStyles = eagerApplyCompatibilityStyles;
        }

        internal bool HasStagedStyles { get; private set; }

        internal static void ClearPathDataPrototypeCacheForBenchmarks()
        {
            SharedPathDataPrototypeCache.Clear();
        }

        /// <summary>
        /// Gets a list of available types that can be used when creating an <see cref="SvgElement"/>.
        /// </summary>
        public List<ElementInfo> AvailableElements => availableElements;

        /// <summary>
        /// Gets a list of available types that can be used when creating an <see cref="SvgElement"/>.
        /// </summary>
        internal Dictionary<string, List<Type>> AvailableElementsDictionary => availableElementsDictionary;

        /// <summary>
        /// Creates an <see cref="SvgDocument"/> from the current node in the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> containing the node to parse into an <see cref="SvgDocument"/>.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="reader"/> parameter cannot be <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The CreateDocument method can only be used to parse root &lt;svg&gt; elements.</exception>
        public T CreateDocument<T>(XmlReader reader) where T : SvgDocument, new()
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            if (reader.LocalName != "svg")
            {
                throw new InvalidOperationException("The CreateDocument method can only be used to parse root <svg> elements.");
            }

            return (T)CreateElement<T>(reader, true, null);
        }

        /// <summary>
        /// Creates an <see cref="SvgElement"/> from the current node in the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="XmlReader"/> containing the node to parse into a subclass of <see cref="SvgElement"/>.</param>
        /// <param name="document">The <see cref="SvgDocument"/> that the created element belongs to.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="reader"/> and <paramref name="document"/> parameters cannot be <c>null</c>.</exception>
        public SvgElement CreateElement(XmlReader reader, SvgDocument document)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            return CreateElement<SvgDocument>(reader, false, document);
        }

        private SvgElement CreateElement<T>(XmlReader reader, bool fragmentIsDocument, SvgDocument document) where T : SvgDocument, new()
        {
            SvgElement createdElement = null;
            string elementName = reader.LocalName;
            string elementNS = reader.NamespaceURI;

            //Trace.TraceInformation("Begin CreateElement: {0}", elementName);

            if (elementNS == SvgNamespaces.SvgNamespace || string.IsNullOrEmpty(elementNS))
            {
                if (elementName == "svg")
                {
                    createdElement = (fragmentIsDocument) ? new T() : new SvgFragment();
                }
                else
                {
                    if (availableElementsWithoutSvg.TryGetValue(elementName, out var validType))
                    {
                        createdElement = validType.CreateInstance();
                    }
                    else
                    {
                        createdElement = new SvgUnknownElement(elementName);
                    }
                }

                if (createdElement != null)
                {
                    SetAttributes(createdElement, reader, document);
                }
            }
            else
            {
                // All non svg element (html, ...)
                createdElement = new NonSvgElement(elementName, elementNS);
                SetAttributes(createdElement, reader, document);
            }

            //Trace.TraceInformation("End CreateElement");

            return createdElement;
        }

        private void SetAttributes(SvgElement element, XmlReader reader, SvgDocument document)
        {
            //Trace.TraceInformation("Begin SetAttributes");

            while (reader.MoveToNextAttribute())
            {
                var prefix = reader.Prefix;
                var localName = reader.LocalName;
                var value = reader.Value;

                if (prefix.Length == 0)
                {
                    if (localName.Equals("xmlns"))
                    {
                        element.Namespaces[string.Empty] = value;
                        continue;
                    }

                    if (localName.Equals("version"))
                    {
                        continue;
                    }

                    if (localName.Equals("style") && !(element is NonSvgElement))
                    {
                        if (inlineStyleAttributeParser.ApplyStyles(
                                element,
                                value,
                                document,
                                eagerApplyCompatibilityStyles,
                                out var stagedStyles) &&
                            stagedStyles)
                        {
                            HasStagedStyles = true;
                        }
                    }
                    else if (localName.Equals("marker"))
                    {
                        // Compare this to the original upstream file: upstream forwarded the
                        // presentation attribute through the same style machinery as CSS, which
                        // populated SvgMarkerElement.Marker on groups and paths. We skip only the
                        // raw XML attribute here so stylesheet and inline-style marker shorthands
                        // continue to work exactly as before.
                        continue;
                    }
                    else if (IsStyleAttribute(localName))
                    {
                        if (!TryApplyCompatibilityStyleImmediately(element, localName, value, document))
                        {
                            element.AddStyleCompatibility(localName, value, SvgElement.StyleSpecificity_PresAttribute);
                            HasStagedStyles = true;
                        }
                    }
                    else
                    {
                        SetPropertyValue(element, string.Empty, localName, value, document);
                    }

                    continue;
                }

                if (prefix.Equals("xmlns"))
                {
                    element.Namespaces[localName] = value;
                    continue;
                }

                SetPropertyValue(element, reader.NamespaceURI, localName, value, document);
            }

            //Trace.TraceInformation("End SetAttributes");
        }

        private bool TryApplyCompatibilityStyleImmediately(
            SvgElement element,
            string attributeName,
            string attributeValue,
            SvgDocument document)
        {
            return eagerApplyCompatibilityStyles &&
                   SetPropertyValue(element, string.Empty, attributeName, attributeValue, document, true);
        }

        private static bool IsStyleAttribute(string name)
        {
            switch (name)
            {
                case "alignment-baseline":
                case "baseline-shift":
                case "clip":
                case "clip-path":
                case "clip-rule":
                case "color":
                case "color-interpolation":
                case "color-interpolation-filters":
                case "color-profile":
                case "color-rendering":
                case "cursor":
                case "direction":
                case "display":
                case "dominant-baseline":
                case "enable-background":
                case "fill":
                case "fill-opacity":
                case "fill-rule":
                case "filter":
                case "flood-color":
                case "flood-opacity":
                case "font":
                case "font-family":
                case "font-size":
                case "font-size-adjust":
                case "font-stretch":
                case "font-style":
                case "font-variant":
                case "font-weight":
                case "glyph-orientation-horizontal":
                case "glyph-orientation-vertical":
                case "image-rendering":
                case "kerning":
                case "letter-spacing":
                case "lighting-color":
                case "marker":
                case "marker-end":
                case "marker-mid":
                case "marker-start":
                case "mask":
                case "opacity":
                case "overflow":
                case "pointer-events":
                case "shape-rendering":
                case "stop-color":
                case "stop-opacity":
                case "stroke":
                case "stroke-dasharray":
                case "stroke-dashoffset":
                case "stroke-linecap":
                case "stroke-linejoin":
                case "stroke-miterlimit":
                case "stroke-opacity":
                case "stroke-width":
                case "text-anchor":
                case "text-decoration":
                case "text-rendering":
                case "text-transform":
                case "unicode-bidi":
                case "visibility":
                case "word-spacing":
                case "writing-mode":
                    return true;
            }
            return false;
        }
        internal static bool SetPropertyValue(SvgElement element, string ns, string attributeName, string attributeValue, SvgDocument document, bool isStyle = false)
        {
            if (attributeName == "text-decoration" && !string.IsNullOrWhiteSpace(attributeValue))
            {
                element.CustomAttributes[RawTextDecorationAttributeKey] = attributeValue;
            }

            if (!isStyle &&
                ns.Length == 0 &&
                TrySetCommonUnprefixedPropertyFast(element, attributeName, attributeValue))
            {
                return true;
            }

            if (attributeName == "stop-opacity" && string.Equals(attributeValue, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                if (isStyle)
                {
                    // Keep style values staged exactly as authored so TryGetAttribute can still
                    // see the inherit keyword later.
                    return false;
                }

                // The upstream float conversion path accepts stop-opacity but loses the literal
                // "inherit" token before gradient evaluation runs. Svg.Custom keeps the raw
                // presentation attribute so the gradient stop/server overrides can follow the SVG
                // inheritance chain at render time.
                element.CustomAttributes[ns.Length == 0 ? attributeName : $"{ns}:{attributeName}"] = attributeValue;
                return true;
            }

            if (attributeName == "opacity" && attributeValue == "undefined")
            {
                attributeValue = "1";
            }
            var setValueResult = element.SetValue(attributeName, document, CultureInfo.InvariantCulture, attributeValue);
            if (setValueResult)
            {
                return true;
            }
            {
                if (isStyle)
                    // custom styles shall remain as style
                    return false;
                // attribute is not a svg attribute, store it in custom attributes
                element.CustomAttributes[ns.Length == 0 ? attributeName : $"{ns}:{attributeName}"] = attributeValue;
            }
            return true;
        }

        private static bool TrySetCommonUnprefixedPropertyFast(SvgElement element, string attributeName, string attributeValue)
        {
            try
            {
                switch (attributeName)
                {
                    case "id":
                        element.ID = attributeValue;
                        return true;
                    case "viewBox":
                        return TrySetViewBoxFast(element, attributeValue);
                    case "transform":
                        element.Transforms = SvgTransformConverter.Parse(attributeValue.AsSpan());
                        return true;
                    case "d" when element is SvgPath path:
                        var parsedPathData = ParsePathDataWithSharedCache(attributeValue);
                        path.PathData = parsedPathData.PathData;
                        path.SetSceneGraphPathDataHash(parsedPathData.Hash);
                        return true;
                    case "cx":
                        return TrySetCenterXFast(element, attributeValue);
                    case "cy":
                        return TrySetCenterYFast(element, attributeValue);
                    case "r":
                        return TrySetRadiusFast(element, attributeValue);
                    case "x":
                        return TrySetXFast(element, attributeValue);
                    case "y":
                        return TrySetYFast(element, attributeValue);
                    case "rx":
                        return TrySetCornerRadiusXFast(element, attributeValue);
                    case "ry":
                        return TrySetCornerRadiusYFast(element, attributeValue);
                    case "width":
                        return TrySetWidthFast(element, attributeValue);
                    case "height":
                        return TrySetHeightFast(element, attributeValue);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private readonly struct ParsedPathDataResult
        {
            public ParsedPathDataResult(SvgPathSegmentList pathData, SceneGraphPathDataHash hash)
            {
                PathData = pathData;
                Hash = hash;
            }

            public SvgPathSegmentList PathData { get; }
            public SceneGraphPathDataHash Hash { get; }
        }

        private sealed class SharedPathDataPrototypeEntry
        {
            public SharedPathDataPrototypeEntry(SvgPathSegmentList prototype, SceneGraphPathDataHash hash)
            {
                Prototype = prototype;
                Hash = hash;
            }

            public SvgPathSegmentList Prototype { get; }
            public SceneGraphPathDataHash Hash { get; }
        }

        private static ParsedPathDataResult ParsePathDataWithSharedCache(string attributeValue)
        {
            if (string.IsNullOrEmpty(attributeValue))
            {
                var emptyPathData = SvgPathBuilder.Parse(attributeValue.AsSpan());
                return new ParsedPathDataResult(emptyPathData, SceneGraphPathDataHashFactory.Create(emptyPathData));
            }

            if (SharedPathDataPrototypeCache.TryGetValue(attributeValue, out var cachedPrototype))
            {
                return new ParsedPathDataResult((SvgPathSegmentList)cachedPrototype.Prototype.Clone(), cachedPrototype.Hash);
            }

            var parsed = SvgPathBuilder.Parse(attributeValue.AsSpan());
            var hash = SceneGraphPathDataHashFactory.Create(parsed);
            SharedPathDataPrototypeCache[attributeValue] = new SharedPathDataPrototypeEntry(parsed, hash);
            TrimSharedPathDataPrototypeCacheIfNeeded();
            return new ParsedPathDataResult((SvgPathSegmentList)parsed.Clone(), hash);
        }

        private static void TrimSharedPathDataPrototypeCacheIfNeeded()
        {
            if (SharedPathDataPrototypeCache.Count > SharedPathDataPrototypeCacheLimit)
            {
                SharedPathDataPrototypeCache.Clear();
            }
        }

        private static bool TrySetViewBoxFast(SvgElement element, string attributeValue)
        {
            var viewBox = ParseViewBox(attributeValue.AsSpan());
            if (element is ISvgViewPort viewPort)
            {
                viewPort.ViewBox = viewBox;
                return true;
            }

            if (element is SvgSymbol symbol)
            {
                symbol.ViewBox = viewBox;
                return true;
            }

            if (element is SvgPatternServer patternServer)
            {
                patternServer.ViewBox = viewBox;
                return true;
            }

            return false;
        }

        private static SvgViewBox ParseViewBox(ReadOnlySpan<char> attributeValue)
        {
            var splitChars = ViewBoxSplitChars.AsSpan();
            var parts = new StringSplitEnumerator(attributeValue, splitChars);

            if (!parts.MoveNext())
            {
                throw new SvgException("The 'viewBox' attribute must be in the format 'minX, minY, width, height'.");
            }

            var minX = StringParser.ToFloat(parts.Current.Value);
            if (!parts.MoveNext())
            {
                throw new SvgException("The 'viewBox' attribute must be in the format 'minX, minY, width, height'.");
            }

            var minY = StringParser.ToFloat(parts.Current.Value);
            if (!parts.MoveNext())
            {
                throw new SvgException("The 'viewBox' attribute must be in the format 'minX, minY, width, height'.");
            }

            var width = StringParser.ToFloat(parts.Current.Value);
            if (!parts.MoveNext())
            {
                throw new SvgException("The 'viewBox' attribute must be in the format 'minX, minY, width, height'.");
            }

            var height = StringParser.ToFloat(parts.Current.Value);
            if (parts.MoveNext())
            {
                throw new SvgException("The 'viewBox' attribute must be in the format 'minX, minY, width, height'.");
            }

            return new SvgViewBox(minX, minY, width, height);
        }

        private static bool TrySetCenterXFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgCircle circle)
            {
                circle.CenterX = unit;
                return true;
            }

            if (element is SvgEllipse ellipse)
            {
                ellipse.CenterX = unit;
                return true;
            }

            if (element is SvgRadialGradientServer radialGradient)
            {
                radialGradient.CenterX = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetCenterYFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgCircle circle)
            {
                circle.CenterY = unit;
                return true;
            }

            if (element is SvgEllipse ellipse)
            {
                ellipse.CenterY = unit;
                return true;
            }

            if (element is SvgRadialGradientServer radialGradient)
            {
                radialGradient.CenterY = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetRadiusFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgCircle circle)
            {
                circle.Radius = unit;
                return true;
            }

            if (element is SvgRadialGradientServer radialGradient)
            {
                radialGradient.Radius = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetXFast(SvgElement element, string attributeValue)
        {
            if (element is SvgTextBase textBase)
            {
                textBase.X = SvgUnitCollectionConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            if (element is SvgRectangle rectangle)
            {
                rectangle.X = SvgUnitConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            if (element is SvgFragment fragment)
            {
                fragment.X = SvgUnitConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            return false;
        }

        private static bool TrySetYFast(SvgElement element, string attributeValue)
        {
            if (element is SvgTextBase textBase)
            {
                textBase.Y = SvgUnitCollectionConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            if (element is SvgRectangle rectangle)
            {
                rectangle.Y = SvgUnitConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            if (element is SvgFragment fragment)
            {
                fragment.Y = SvgUnitConverter.Parse(attributeValue.AsSpan());
                return true;
            }

            return false;
        }

        private static bool TrySetCornerRadiusXFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgRectangle rectangle)
            {
                rectangle.CornerRadiusX = unit;
                return true;
            }

            if (element is SvgEllipse ellipse)
            {
                ellipse.RadiusX = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetCornerRadiusYFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgRectangle rectangle)
            {
                rectangle.CornerRadiusY = unit;
                return true;
            }

            if (element is SvgEllipse ellipse)
            {
                ellipse.RadiusY = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetWidthFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgRectangle rectangle)
            {
                rectangle.Width = unit;
                return true;
            }

            if (element is SvgFragment fragment)
            {
                fragment.Width = unit;
                return true;
            }

            return false;
        }

        private static bool TrySetHeightFast(SvgElement element, string attributeValue)
        {
            var unit = SvgUnitConverter.Parse(attributeValue.AsSpan());
            if (element is SvgRectangle rectangle)
            {
                rectangle.Height = unit;
                return true;
            }

            if (element is SvgFragment fragment)
            {
                fragment.Height = unit;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Contains information about a type inheriting from <see cref="SvgElement"/>.
        /// </summary>
        [DebuggerDisplay("{ElementName}, {ElementType}")]
        internal sealed class ElementInfo
        {
            /// <summary>
            /// Gets the SVG name of the <see cref="SvgElement"/>.
            /// </summary>
            public string ElementName { get; set; }
            /// <summary>
            /// Gets the <see cref="Type"/> of the <see cref="SvgElement"/> subclass.
            /// </summary>
            public Type ElementType { get; set; }
            /// <summary>
            /// Creates a new instance based on <see cref="ElementType"/> type.
            /// </summary>
            public Func<SvgElement> CreateInstance { get; set; }
            /// <summary>
            /// Initializes a new instance of the <see cref="ElementInfo"/> struct.
            /// </summary>
            /// <param name="elementName">Name of the element.</param>
            /// <param name="elementType">Type of the element.</param>
            public ElementInfo(string elementName, Type elementType)
            {
                this.ElementName = elementName;
                this.ElementType = elementType;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ElementInfo"/> class.
            /// </summary>
            public ElementInfo()
            {
            }
        }
    }
}
