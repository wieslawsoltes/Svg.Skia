using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml;
using Svg.ExCSS;
using SvgXml.Css;
using Xml;

namespace Svg
{
#nullable disable warnings
    public class DtdXmlUrlResolver : XmlUrlResolver
    {
        public static string s_name = "SvgXml.Svg.Resources.svg11.dtd";

        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            if (absoluteUri.ToString().IndexOf("svg", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
                return Assembly.GetExecutingAssembly().GetManifestResourceStream(s_name);
            }
            else
            {
                return base.GetEntity(absoluteUri, role, ofObjectToReturn);
            }
        }
    }
#nullable enable warnings

    public class SvgDocument : SvgFragment
    {
        public static XmlReaderSettings s_settings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new DtdXmlUrlResolver(),
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

        public static IElementFactory s_elementFactory = new SvgElementFactory();

        public static string s_userAgentStyleSheet =
            "svg, symbol, image, marker, pattern, foreignObject { overflow: hidden }" +
            Environment.NewLine +
            "svg { width: attr(width); height: attr(height) }";

        public static void GetElements<T>(Element element, List<T> elements)
        {
            foreach (var child in element.Children)
            {
                if (child is T t)
                {
                    elements.Add(t);
                }
                else
                {
                    GetElements(child, elements);
                }
            }
        }

        public static SvgDocument? Open(XmlReader reader)
        {
            var element = XmlLoader.Read(reader, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? Open(Stream stream)
        {
            using var reader = XmlReader.Create(stream, s_settings);
            return Open(reader);
        }

        public static SvgDocument? OpenSvg(string path)
        {
            using var stream = File.OpenRead(path);
            return Open(stream);
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return Open(memoryStream);
        }

        public static SvgDocument? Open(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.ToLower() switch
            {
                ".svg" => OpenSvg(path),
                ".svgz" => OpenSvgz(path),
                _ => OpenSvg(path),
            };
        }

        public static SvgDocument? FromSvg(string svg)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);
            streamWriter.Write(svg);
            streamWriter.Flush();
            memoryStream.Position = 0;
            return Open(memoryStream);
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
                case "font": // NOTE: css only
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
                case "mask": // NOTE: css only
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
                case "text-transform": // NOTE: css only
                case "unicode-bidi":
                case "visibility":
                case "word-spacing":
                case "writing-mode":
                    return true;
                default:
                    return false;
            }
        }

        private static void SetProperties(Element element, Parser parser)
        {
            foreach (var child in element.Children)
            {
                foreach (var attribute in child.Attributes)
                {
                    if (attribute.Key.Equals("style"))
                    {
                        var sheet = parser.Parse("#a{" + attribute.Value + "}");
                        foreach (var rule in sheet.StyleRules)
                        {
                            foreach (var decl in rule.Declarations)
                            {
                                var value = decl.Term.ToString();
                                if (value != null)
                                {
                                    child.AddStyle(decl.Name, value, SvgElement.StyleSpecificity_InlineStyle);
                                }
                            }
                        }
                    }
                    else if (IsStyleAttribute(attribute.Key))
                    {
                        if (attribute.Value != null)
                        {
                            child.AddStyle(attribute.Key, attribute.Value, SvgElement.StyleSpecificity_PresAttribute);
                        }
                    }
                    else
                    {
                        child.SetPropertyValue(attribute.Key, attribute.Value);
                    }
                }

                SetProperties(child, parser);
            }
        }

        public void LoadStyles()
        {
            SetProperties(this, new Parser());

            var styles = new List<SvgStyle>();
            GetElements(this, styles);

            var css = string.Empty;
            css += s_userAgentStyleSheet;

            if (styles.Count > 0)
            {
                foreach (var style in styles)
                {
                    foreach (var child in style.Children)
                    {
                        if (child is ContentElement content)
                        {
                            css += Environment.NewLine + content.Content;
                        }
                    }
                }
            }

            var parser = new Parser();
            var sheet = parser.Parse(css);

            foreach (var rule in sheet.StyleRules)
            {
                IEnumerable<BaseSelector> selectors;
                if (rule.Selector is AggregateSelectorList aggregateList && aggregateList.Delimiter == ",")
                {
                    selectors = aggregateList;
                }
                else
                {
                    selectors = Enumerable.Repeat(rule.Selector, 1);
                }

                foreach (var selector in selectors)
                {
                    try
                    {
                        var rootNode = new UnknownElement();
                        rootNode.Children.Add(this);

                        var elements = rootNode.QuerySelectorAll(rule.Selector.ToString(), s_elementFactory);
                        foreach (var element in elements)
                        {
                            foreach (var decl in rule.Declarations)
                            {
                                var value = decl.Term.ToString();
                                if (value != null)
                                {
                                    element.AddStyle(decl.Name, value, rule.Selector.GetSpecificity());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
        }
    }
}
