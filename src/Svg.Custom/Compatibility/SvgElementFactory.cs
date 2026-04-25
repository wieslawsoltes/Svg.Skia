using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml;

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
        private static readonly ConcurrentDictionary<Type, HashSet<string>> s_eventDescriptorAttributeNamesByType = new();

        private readonly SvgInlineStyleAttributeParser inlineStyleAttributeParser = new();

        internal bool PreserveJavaScriptDomState { get; set; }

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

            //string[] styles = null;
            //string[] style = null;
            //int i = 0;

            while (reader.MoveToNextAttribute())
            {
                var prefix = reader.Prefix;
                var localName = reader.LocalName;
                if (reader.ReadAttributeValue())
                {
                    if (prefix.Length == 0)
                    {
                        if (localName.Equals("xmlns"))
                        {
                            element.Namespaces[string.Empty] = reader.Value;
                            continue;
                        }
                        else if (localName.Equals("version"))
                            continue;
                    }
                    else if (prefix.Equals("xmlns"))
                    {
                        element.Namespaces[localName] = reader.Value;
                        continue;
                    }
                    if (localName.Equals("style") && !(element is NonSvgElement))
                    {
                        if (PreserveJavaScriptDomState && !string.IsNullOrWhiteSpace(reader.Value))
                        {
                            element.CustomAttributes["style"] = reader.Value;
                            TrackCompatibilityStyleStateCandidate(document, element);
                        }

                        inlineStyleAttributeParser.ApplyStyles(element, reader.Value);
                    }
                    else if (prefix.Length == 0 && localName.Equals("marker"))
                    {
                        // Compare this to the original upstream file: upstream forwarded the
                        // presentation attribute through the same style machinery as CSS, which
                        // populated SvgMarkerElement.Marker on groups and paths. We skip only the
                        // raw XML attribute here so stylesheet and inline-style marker shorthands
                        // continue to work exactly as before.
                        continue;
                    }
                    else if (prefix.Length == 0 && IsStyleAttribute(localName))
                    {
                        if (PreserveJavaScriptDomState)
                        {
                            if (element.PreserveCompatibilityPresentationAttribute(localName, reader.Value))
                            {
                                TrackCompatibilityStyleStateCandidate(document, element);
                            }
                        }

                        element.AddStyle(localName, reader.Value, SvgElement.StyleSpecificity_PresAttribute);
                    }
                    else
                    {
                        var ns = prefix.Length == 0 ? string.Empty : reader.LookupNamespace(prefix);
                        SetPropertyValue(element, ns, localName, reader.Value, document);
                    }
                }
            }

            //Trace.TraceInformation("End SetAttributes");
        }

        private static void TrackCompatibilityStyleStateCandidate(SvgDocument document, SvgElement element)
        {
            var ownerDocument = document ?? element as SvgDocument;
            ownerDocument?.TrackCompatibilityStyleStateCandidate(element);
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
        internal static bool SetPropertyValue(
            SvgElement element,
            string ns,
            string attributeName,
            string attributeValue,
            SvgDocument document,
            bool isStyle = false)
        {
            if (ns.Length == 0 &&
                IsEventDescriptorAttribute(element, attributeName))
            {
                element.CustomAttributes[attributeName] = attributeValue;
            }

            if (attributeName == "text-decoration" && !string.IsNullOrWhiteSpace(attributeValue))
            {
                element.CustomAttributes[SvgStyleAttributeNames.RawTextDecorationAttributeKey] = attributeValue;
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

        private static bool IsEventDescriptorAttribute(SvgElement element, string attributeName)
        {
            if (!IsKnownScriptAttributeName(attributeName))
            {
                return false;
            }

            var eventAttributeNames = s_eventDescriptorAttributeNamesByType.GetOrAdd(
                element.GetType(),
                _ => CreateEventDescriptorAttributeNameSet(element));

            return eventAttributeNames.Contains(attributeName);
        }

        private static bool IsKnownScriptAttributeName(string attributeName)
        {
            if (attributeName.Length < 4 ||
                (attributeName[0] != 'o' && attributeName[0] != 'O') ||
                (attributeName[1] != 'n' && attributeName[1] != 'N'))
            {
                return false;
            }

            switch (attributeName)
            {
                case "onabort":
                case "onactivate":
                case "onbegin":
                case "onchange":
                case "onclick":
                case "onend":
                case "onerror":
                case "onfocusin":
                case "onfocusout":
                case "onload":
                case "onmousedown":
                case "onmousemove":
                case "onmouseout":
                case "onmouseover":
                case "onmouseup":
                case "onmousescroll":
                case "onrepeat":
                case "onresize":
                case "onscroll":
                case "onunload":
                case "onzoom":
                    return true;
            }

            return attributeName.Equals("onabort", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onactivate", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onbegin", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onchange", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onclick", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onend", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onerror", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onfocusin", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onfocusout", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onload", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmousedown", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmousemove", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmouseout", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmouseover", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmouseup", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onmousescroll", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onrepeat", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onresize", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onscroll", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onunload", StringComparison.OrdinalIgnoreCase) ||
                   attributeName.Equals("onzoom", StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<string> CreateEventDescriptorAttributeNameSet(SvgElement element)
        {
            var eventAttributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.GetProperties())
            {
                if (property.DescriptorType == DescriptorType.Event &&
                    !string.IsNullOrEmpty(property.AttributeName))
                {
                    eventAttributeNames.Add(property.AttributeName);
                }
            }

            return eventAttributeNames;
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
