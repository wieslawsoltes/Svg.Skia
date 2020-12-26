using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using Svg.ExCSS;
using SvgXml.Css;
using SvgXml.Svg.DocumentStructure;
using SvgXml.Svg.Styling;
using SvgXml.Xml;
using SvgXml.Xml.Elements;

namespace SvgXml.Svg
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
            "svg, symbol, image, marker, pattern, foreignObject { overflow: hidden }" /* +
            Environment.NewLine +
            "svg { width: attr(width); height: attr(height) }" */;

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

        public static SvgDocument? Open(XmlReader reader, Uri? baseUri = null)
        {
            var element = XmlLoader.Read(reader, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                svgDocument.BaseUri = baseUri;
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? Open(Stream stream, Uri? baseUri = null)
        {
            using var reader = XmlReader.Create(stream, s_settings);
            return Open(reader, baseUri);
        }

        public static SvgDocument? OpenSvg(string path)
        {
            using var stream = File.OpenRead(path);
            var baseUri = new Uri(Path.GetFullPath(path));
            return Open(stream, baseUri);
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            var baseUri = new Uri(Path.GetFullPath(path));
            return Open(memoryStream, baseUri);
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

        public static SvgDocument? FromSvg(string svg, Uri? baseUri = null)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);
            streamWriter.Write(svg);
            streamWriter.Flush();
            memoryStream.Position = 0;
            return Open(memoryStream, baseUri);
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
                var attributes = child.Attributes.ToList();

                foreach (var attribute in attributes)
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
                                    child.AddStyle(decl.Name, value, StyleSpecificity_InlineStyle);
                                }
                            }
                        }
                    }
                    else if (IsStyleAttribute(attribute.Key))
                    {
                        if (attribute.Value != null)
                        {
                            child.AddStyle(attribute.Key, attribute.Value, StyleSpecificity_PresAttribute);
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

        private Dictionary<string, SvgElement> _ids;

        public IReadOnlyDictionary<string, SvgElement> Ids => _ids;

        public Uri? BaseUri { get; set; }

        public SvgDocument()
        {
            _ids = new Dictionary<string, SvgElement>();
        }

        public SvgElement? GetElementById(string id)
        {
            id = GetUrlString(id);
            if (id.StartsWith("#"))
            {
                id = id.Substring(1);
            }
            _ids.TryGetValue(id, out var element);
            return element;
        }

        public SvgElement? GetElementById(Uri uri)
        {
            var urlString = GetUrlString(uri.ToString());

            if (urlString.StartsWith("#"))
            {
                return GetElementById(urlString);
            }

            var index = urlString.LastIndexOf('#');
            var fragment = urlString.Substring(index);

            uri = new Uri(urlString.Remove(index, fragment.Length), UriKind.RelativeOrAbsolute);

            if (!uri.IsAbsoluteUri && BaseUri != null)
            {
                uri = new Uri(BaseUri, uri);
            }

            if (!uri.IsAbsoluteUri)
            {
                return GetElementById(urlString);
            }

            if (uri.IsFile)
            {
                var document = Open(uri.LocalPath);
                return document?.GetElementById(fragment);
            }
            else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                var httpRequest = WebRequest.Create(uri);
                using var webResponse = httpRequest.GetResponse();
                var document = Open(webResponse.GetResponseStream());
                return document?.GetElementById(fragment);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private string GetUrlString(string url)
        {
            url = url.Trim();
            if (url.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && url.EndsWith(")"))
            {
                url = new StringBuilder(url).Remove(url.Length - 1, 1).Remove(0, 4).ToString().Trim();

                if ((url.StartsWith("\"") && url.EndsWith("\"")) || (url.StartsWith("'") && url.EndsWith("'")))
                {
                    url = new StringBuilder(url).Remove(url.Length - 1, 1).Remove(0, 1).ToString().Trim();
                }
            }
            return url;
        }

        private void LoadIds(List<Element> children)
        {
            if (children == null)
            {
                return;
            }
            foreach (var child in children)
            {
                if (child is SvgElement element)
                {
                    if (element.Id != null && !string.IsNullOrEmpty(element.Id))
                    {
                        _ids.Add(element.Id, element);
                    }
                }
                LoadIds(child.Children);
            }
        }

        public void LoadIds()
        {
            _ids.Clear();
            LoadIds(Children);
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
