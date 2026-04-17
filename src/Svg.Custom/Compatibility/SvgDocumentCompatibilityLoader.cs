#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Svg;

/// <summary>
/// Browser-compatibility focused loader for Svg.Custom.
///
/// The upstream loader is good enough for basic SVG parsing, but the Chrome-backed W3C rows showed
/// two loader-specific gaps that matter before CSS can be applied correctly:
///
/// 1. relative stylesheet references need a stable document base URI, even when the SVG is opened
///    through the convenience API and CSS is applied after parsing;
/// 2. the XML/tree-loading path needs to preserve the raw stylesheet text so the stricter
///    browser-compatibility CSS pass can run after the document model is built.
///
/// This class keeps the upstream XML tree construction shape and delegates the Chrome-aligned CSS
/// policy to <see cref="SvgCssCompatibilityProcessor"/> once the raw style sources have been
/// collected.
/// </summary>
public static class SvgDocumentCompatibilityLoader
{
    private const int EagerCompatibilityStyleSvgLengthThreshold = 128 * 1024;
    private const string ParsedAnimationElementsHintKey = "__svgskia:contains-animation-elements";
    private const string ParsedXmlBaseHintKey = "__svgskia:contains-xml-base";

    public static T Open<T>(Stream stream) where T : SvgDocument, new()
    {
        return Open<T>(stream, baseUri: null);
    }

    public static T Open<T>(Stream stream, Uri? baseUri) where T : SvgDocument, new()
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var reader = new SvgTextReader(stream, null)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        return Create<T>(reader, css: null, baseUri: baseUri, eagerApplyCompatibilityStyles: false);
    }

    public static T Open<T>(string path, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Capture the absolute document URI before the stream is opened so later CSS resolution can
        // expand relative @import/file references exactly as the browser would relative to the SVG.
        var baseUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        using var stream = File.OpenRead(path);
        return Open<T>(stream, svgOptions, baseUri);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, null);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions, Uri? baseUri) where T : SvgDocument, new()
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var reader = new SvgTextReader(stream, svgOptions.Entities)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        return Create<T>(reader, svgOptions.Css, baseUri, eagerApplyCompatibilityStyles: false);
    }

    public static T FromSvg<T>(string svg) where T : SvgDocument, new()
    {
        return FromSvg<T>(svg, new SvgOptions(), null);
    }

    public static T FromSvg<T>(string svg, Uri? baseUri) where T : SvgDocument, new()
    {
        if (string.IsNullOrEmpty(svg))
        {
            throw new ArgumentNullException(nameof(svg));
        }

        using var stringReader = new StringReader(svg);
        var reader = new SvgTextReader(stringReader, null)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        var loadResult = LoadStructure<T>(
            reader,
            css: null,
            baseUri,
            eagerApplyCompatibilityStyles: false);
        FinalizeDocument(loadResult);
        return (T)loadResult.Document;
    }

    public static T FromSvg<T>(string svg, SvgOptions svgOptions, Uri? baseUri = null) where T : SvgDocument, new()
    {
        if (string.IsNullOrEmpty(svg))
        {
            throw new ArgumentNullException(nameof(svg));
        }

        using var stringReader = new StringReader(svg);
        var reader = new SvgTextReader(stringReader, svgOptions.Entities)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        var loadResult = LoadStructure<T>(
            reader,
            svgOptions.Css,
            baseUri,
            eagerApplyCompatibilityStyles: CanEagerApplyCompatibilityStyles(svgOptions.Css, svg));
        FinalizeDocument(loadResult);
        return (T)loadResult.Document;
    }

    public static T Open<T>(XmlReader reader) where T : SvgDocument, new()
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var baseUri = TryGetAbsoluteBaseUri(reader.BaseURI);

        if (SvgDocument.DisableDtdProcessing &&
            reader.Settings?.DtdProcessing == DtdProcessing.Parse)
        {
            throw new InvalidOperationException("XmlReader input must not enable DTD processing when SvgDocument.DisableDtdProcessing is true.");
        }

        using var svgReader = XmlReader.Create(reader, new XmlReaderSettings
        {
            XmlResolver = new SvgDtdResolver(),
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
            IgnoreWhitespace = false,
        });

        var loadResult = LoadStructure<T>(
            svgReader,
            baseUri: baseUri,
            eagerApplyCompatibilityStyles: true);
        FinalizeDocument(loadResult);
        return (T)loadResult.Document;
    }

    private static T Create<T>(
        XmlReader reader,
        string? css = null,
        Uri? baseUri = null,
        bool? eagerApplyCompatibilityStyles = null) where T : SvgDocument, new()
    {
        var loadResult = LoadStructure<T>(
            reader,
            css,
            baseUri,
            eagerApplyCompatibilityStyles ?? CanEagerApplyCompatibilityStyles(css));
        FinalizeDocument(loadResult);
        return (T)loadResult.Document;
    }

    internal static SvgCompatibilityLoadResult LoadStructure<T>(string svg, string? css = null, Uri? baseUri = null) where T : SvgDocument, new()
    {
        if (string.IsNullOrEmpty(svg))
        {
            throw new ArgumentNullException(nameof(svg));
        }

        using var stringReader = new StringReader(svg);
        using var reader = new SvgTextReader(stringReader, null)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        return LoadStructure<T>(
            reader,
            css,
            baseUri,
            eagerApplyCompatibilityStyles: CanEagerApplyCompatibilityStyles(css, svg));
    }

    internal static int CreateElementsOnly<T>(string svg) where T : SvgDocument, new()
    {
        if (string.IsNullOrEmpty(svg))
        {
            throw new ArgumentNullException(nameof(svg));
        }

        using var stringReader = new StringReader(svg);
        using var reader = new SvgTextReader(stringReader, null)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        return CreateElementsOnly<T>(reader);
    }

    internal static SvgCompatibilityLoadResult LoadStructure<T>(
        XmlReader reader,
        string? css = null,
        Uri? baseUri = null,
        bool eagerApplyCompatibilityStyles = false) where T : SvgDocument, new()
    {
        // Keep each stylesheet fragment together with the URI it should resolve against. That lets
        // inline CSS from the SVG document, externally supplied CSS, and recursively imported CSS
        // all share one merge/apply path without losing origin information.
        var styles = new List<SvgCssStyleSource>();
        var elementFactory = new SvgElementFactory(eagerApplyCompatibilityStyles);
        var svgDocument = Create<T>(reader, elementFactory, styles, baseUri);

        // Avalonia and other hosts can concatenate optional CSS inputs into a whitespace-only
        // string (for example " ") even when no actual stylesheet content is present. Treat that
        // the same as "no CSS" so the compatibility pipeline does not run a synthetic selector
        // root over an otherwise plain document, which would mutate Parent/index paths and break
        // later animation address resolution on deep-cloned documents.
        var normalizedCss = string.IsNullOrWhiteSpace(css) ? null : css;
        if (normalizedCss is not null)
        {
            styles.Add(new SvgCssStyleSource(
                SvgCssCompatibilityProcessor.NormalizeStyleElementContent(normalizedCss),
                baseUri));
        }

        return new SvgCompatibilityLoadResult(svgDocument!, elementFactory, styles, elementFactory.HasStagedStyles);
    }

    internal static int CreateElementsOnly<T>(XmlReader reader) where T : SvgDocument, new()
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var elementFactory = new SvgElementFactory();
        T? svgDocument = null;
        var elementCount = 0;

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (svgDocument is null)
            {
                svgDocument = elementFactory.CreateDocument<T>(reader);
            }
            else
            {
                _ = elementFactory.CreateElement(reader, svgDocument);
            }

            elementCount++;
        }

        return elementCount;
    }

    internal static bool ApplyCompatibilityCss(SvgCompatibilityLoadResult loadResult)
    {
        if (loadResult is null)
        {
            throw new ArgumentNullException(nameof(loadResult));
        }

        var appliedStyles = SvgCssCompatibilityProcessor.Apply(loadResult.Document, loadResult.Styles, loadResult.ElementFactory);
        loadResult.HasStagedStyles |= appliedStyles;
        return appliedStyles;
    }

    internal static void FlushCompatibilityStyles(SvgCompatibilityLoadResult loadResult, bool onlyWhenStagedStyles = false)
    {
        if (loadResult is null)
        {
            throw new ArgumentNullException(nameof(loadResult));
        }

        if (onlyWhenStagedStyles && !loadResult.HasStagedStyles)
        {
            return;
        }

        loadResult.Document.FlushStylesCompatibility(true);
    }

    internal static void FinalizeDocument(SvgCompatibilityLoadResult loadResult)
    {
        if (loadResult is null)
        {
            throw new ArgumentNullException(nameof(loadResult));
        }

        ApplyCompatibilityCss(loadResult);
        FlushCompatibilityStyles(loadResult, onlyWhenStagedStyles: true);
    }

    private static T Create<T>(XmlReader reader, SvgElementFactory elementFactory, List<SvgCssStyleSource> styles, Uri? baseUri)
        where T : SvgDocument, new()
    {
        var elementStack = new List<ParseFrame>(64);
        var elementEmpty = false;
        var containsAnimationElements = false;
        var containsXmlBase = false;
        SvgElement? element = null;
        SvgElement? parent;
        T? svgDocument = null;

        while (reader.Read())
        {
            try
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        elementEmpty = reader.IsEmptyElement;
                        if (elementStack.Count > 0)
                        {
                            element = elementFactory.CreateElement(reader, svgDocument!);
                        }
                        else
                        {
                            svgDocument = elementFactory.CreateDocument<T>(reader);
                            svgDocument.BaseUri = baseUri;
                            element = svgDocument;
                        }

                        containsAnimationElements |= element is SvgAnimationElement;
                        containsXmlBase |= element.ContainsAttribute("xml:base") || element.ContainsAttribute("base");

                        if (elementStack.Count > 0)
                        {
                            var parentFrameIndex = elementStack.Count - 1;
                            var parentFrame = elementStack[parentFrameIndex];
                            parent = parentFrame.Element;
                            if (parent is not null && element is not null)
                            {
                                parent.Children.Add(element);
                                parent.MergeChildStyleSubtreeCompatibility(element);

                                if (parentFrame.TracksChildNodes)
                                {
                                    parent.Nodes.Add(element);
                                }

                                elementStack[parentFrameIndex] = parentFrame;
                                elementStack.Add(new ParseFrame(element));
                            }
                            else
                            {
                                elementStack.Add(new ParseFrame(element!));
                            }
                        }
                        else
                        {
                            elementStack.Add(new ParseFrame(element!));
                        }

                        if (elementEmpty)
                        {
                            goto case XmlNodeType.EndElement;
                        }

                        break;

                    case XmlNodeType.EndElement:
                        var topIndex = elementStack.Count - 1;
                        var parseFrame = elementStack[topIndex];
                        elementStack.RemoveAt(topIndex);
                        element = parseFrame.Element;

                        if (parseFrame.AccumulatedContent is { } accumulatedContent)
                        {
                            element.Content = accumulatedContent.ToString();
                            element.Nodes.Clear();
                        }
                        else if (parseFrame.HasContentNode &&
                                 TryAggregateNodeContent(element, out var content))
                        {
                            element.Content = content;
                        }
                        else
                        {
                            element.Nodes.Clear();
                        }

                        if (element is SvgUnknownElement unknown &&
                            unknown.ElementName == "style" &&
                            SvgCssCompatibilityProcessor.ShouldApplyStyleElement(unknown))
                        {
                            var normalizedStyleContent = SvgCssCompatibilityProcessor.NormalizeStyleElementContent(unknown.Content ?? string.Empty);
                            if (!string.Equals(unknown.Content, normalizedStyleContent, StringComparison.Ordinal))
                            {
                                unknown.Content = normalizedStyleContent;
                            }

                            // Preserve the document base URI with every collected <style> block so
                            // any nested @import inside that block resolves relative to the SVG file
                            // that declared it, not to the current process working directory.
                            styles.Add(new SvgCssStyleSource(normalizedStyleContent, svgDocument?.BaseUri));
                        }

                        break;

                    case XmlNodeType.CDATA:
                    case XmlNodeType.Text:
                    case XmlNodeType.SignificantWhitespace:
                        if (elementStack.Count > 0)
                        {
                            var currentFrameIndex = elementStack.Count - 1;
                            var currentFrame = elementStack[currentFrameIndex];
                            AppendContentNodeValue(ref currentFrame, reader.Value);
                            currentFrame.HasContentNode = true;
                            elementStack[currentFrameIndex] = currentFrame;
                        }

                        break;

                    case XmlNodeType.Whitespace:
                        if (elementStack.Count > 0)
                        {
                            var currentFrameIndex = elementStack.Count - 1;
                            var currentFrame = elementStack[currentFrameIndex];
                            if (!currentFrame.AccumulatesContentWithoutNodes &&
                                !ShouldPreserveTextWhitespace(currentFrame.Element))
                            {
                                break;
                            }

                            AppendContentNodeValue(ref currentFrame, reader.Value);
                            currentFrame.HasContentNode = true;
                            elementStack[currentFrameIndex] = currentFrame;
                        }

                        break;

                    case XmlNodeType.EntityReference:
                        reader.ResolveEntity();
                        if (elementStack.Count > 0)
                        {
                            var currentFrameIndex = elementStack.Count - 1;
                            var currentFrame = elementStack[currentFrameIndex];
                            AppendContentNodeValue(ref currentFrame, reader.Value);
                            currentFrame.HasContentNode = true;
                            elementStack[currentFrameIndex] = currentFrame;
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
        }

        if (svgDocument is not null)
        {
            svgDocument.CustomAttributes[ParsedAnimationElementsHintKey] = containsAnimationElements ? "1" : "0";
            svgDocument.CustomAttributes[ParsedXmlBaseHintKey] = containsXmlBase ? "1" : "0";
        }

        return svgDocument!;
    }

    private static bool ShouldPreserveTextWhitespace(SvgElement element)
    {
        return element is SvgTextBase;
    }

    private static void EnsureTrackedChildNodes(ref ParseFrame frame)
    {
        if (frame.TracksChildNodes)
        {
            return;
        }

        var children = frame.Element.Children;
        for (var i = 0; i < children.Count; i++)
        {
            frame.Element.Nodes.Add(children[i]);
        }

        frame.TracksChildNodes = true;
    }

    private static void AppendContentNodeValue(ref ParseFrame frame, string value)
    {
        if (frame.AccumulatesContentWithoutNodes)
        {
            (frame.AccumulatedContent ??= new StringBuilder()).Append(value);
            return;
        }

        EnsureTrackedChildNodes(ref frame);
        frame.Element.Nodes.Add(new SvgContentNode { Content = value });
    }

    private static bool CanEagerApplyCompatibilityStyles(string? css, string? svg = null)
    {
        if (!string.IsNullOrWhiteSpace(css))
        {
            return false;
        }

        if (svg is { Length: > EagerCompatibilityStyleSvgLengthThreshold })
        {
            return false;
        }

        return svg is null || !ContainsStyleElement(svg);
    }

    private static bool ContainsStyleElement(string svg)
    {
        return svg.IndexOf("<style", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private struct ParseFrame
    {
        public ParseFrame(SvgElement element)
        {
            Element = element;
            AccumulatesContentWithoutNodes = ShouldAccumulateContentWithoutNodes(element);
            AccumulatedContent = null;
        }

        public SvgElement Element { get; }

        public bool HasContentNode { get; set; }

        public bool TracksChildNodes { get; set; }

        public bool AccumulatesContentWithoutNodes { get; }

        public StringBuilder? AccumulatedContent { get; set; }
    }

    private static bool ShouldAccumulateContentWithoutNodes(SvgElement element)
    {
        return element is SvgUnknownElement unknown &&
               unknown.ElementName == "style";
    }

    private static bool TryAggregateNodeContent(SvgElement element, out string content)
    {
        var nodes = element.Nodes;
        if (nodes.Count == 0)
        {
            content = string.Empty;
            return false;
        }

        if (nodes.Count == 1)
        {
            var node = nodes[0];
            if (node is SvgContentNode)
            {
                content = node.Content ?? string.Empty;
                return true;
            }

            content = string.Empty;
            return false;
        }

        var hasContentNode = false;
        string? aggregatedContent = null;
        StringBuilder? builder = null;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node is SvgContentNode)
            {
                hasContentNode = true;
            }

            var nodeContent = node.Content;
            if (string.IsNullOrEmpty(nodeContent))
            {
                continue;
            }

            if (builder is not null)
            {
                builder.Append(nodeContent);
                continue;
            }

            if (aggregatedContent is null)
            {
                aggregatedContent = nodeContent;
                continue;
            }

            builder = new StringBuilder(aggregatedContent.Length + nodeContent.Length);
            builder.Append(aggregatedContent);
            builder.Append(nodeContent);
        }

        if (!hasContentNode)
        {
            content = string.Empty;
            return false;
        }

        content = builder?.ToString() ?? aggregatedContent ?? string.Empty;
        return true;
    }

    private static Uri? TryGetAbsoluteBaseUri(string? baseUri)
    {
        if (string.IsNullOrWhiteSpace(baseUri))
        {
            return null;
        }

        // XmlReader can surface the source location as BaseURI when it was opened from a file or
        // other URI-backed source. Thread that information into the compatibility loader so all
        // Open(...) entry points resolve relative stylesheets the same way.
        return Uri.TryCreate(baseUri, UriKind.Absolute, out var absoluteBaseUri)
            ? absoluteBaseUri
            : null;
    }

}
