#nullable enable
using System;
using System.Buffers;
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
    private static readonly byte[] s_styleElementToken = Encoding.ASCII.GetBytes("<style");
    private static readonly byte[] s_xmlStylesheetToken = Encoding.ASCII.GetBytes("xml-stylesheet");
    private static readonly byte[] s_importToken = Encoding.ASCII.GetBytes("@import");

    public static T Open<T>(string path, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        return Open<T>(path, svgOptions, captureCompatibilityStyleState: false);
    }

    public static T Open<T>(string path, SvgOptions svgOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Capture the absolute document URI before the stream is opened so later CSS resolution can
        // expand relative @import/file references exactly as the browser would relative to the SVG.
        var baseUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        using var stream = File.OpenRead(path);
        return Open<T>(stream, svgOptions, baseUri, captureCompatibilityStyleState);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, captureCompatibilityStyleState: false);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, null, captureCompatibilityStyleState);
    }

    private static T Open<T>(Stream stream, SvgOptions svgOptions, Uri? baseUri, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var preserveCompatibilityPresentationAttributes =
            captureCompatibilityStyleState && MayContainCompatibilityStyleSources(stream, svgOptions.Css);
        var reader = new SvgTextReader(stream, svgOptions.Entities)
        {
            XmlResolver = new SvgDtdResolver(),
            WhitespaceHandling = WhitespaceHandling.All,
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
        };

        return Create<T>(
            reader,
            svgOptions.Css,
            baseUri,
            captureCompatibilityStyleState,
            preserveCompatibilityPresentationAttributes: preserveCompatibilityPresentationAttributes);
    }

    public static T FromSvg<T>(string svg) where T : SvgDocument, new()
    {
        return FromSvg<T>(svg, captureCompatibilityStyleState: false);
    }

    public static T FromSvg<T>(string svg, bool captureCompatibilityStyleState) where T : SvgDocument, new()
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

        return Create<T>(
            reader,
            captureCompatibilityStyleState: captureCompatibilityStyleState,
            preserveCompatibilityPresentationAttributes: captureCompatibilityStyleState && MayContainCompatibilityStyleSources(svg, null));
    }

    public static T Open<T>(XmlReader reader) where T : SvgDocument, new()
    {
        return Open<T>(reader, captureCompatibilityStyleState: false);
    }

    public static T Open<T>(XmlReader reader, bool captureCompatibilityStyleState) where T : SvgDocument, new()
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

        return Create<T>(svgReader, baseUri: baseUri, captureCompatibilityStyleState: captureCompatibilityStyleState);
    }

    private static T Create<T>(
        XmlReader reader,
        string? css = null,
        Uri? baseUri = null,
        bool captureCompatibilityStyleState = false,
        bool preserveCompatibilityPresentationAttributes = true)
        where T : SvgDocument, new()
    {
        // Keep each stylesheet fragment together with the URI it should resolve against. That lets
        // inline CSS from the SVG document, externally supplied CSS, and recursively imported CSS
        // all share one merge/apply path without losing origin information.
        List<SvgCssStyleSource>? styles = null;
        var elementFactory = new SvgElementFactory
        {
            PreserveJavaScriptDomState = captureCompatibilityStyleState,
            PreserveCompatibilityPresentationAttributes = captureCompatibilityStyleState && preserveCompatibilityPresentationAttributes
        };
        var svgDocument = Create<T>(reader, elementFactory, ref styles, baseUri);

        // Avalonia and other hosts can concatenate optional CSS inputs into a whitespace-only
        // string (for example " ") even when no actual stylesheet content is present. Treat that
        // the same as "no CSS" so the compatibility pipeline does not run a synthetic selector
        // root over an otherwise plain document, which would mutate Parent/index paths and break
        // later animation address resolution on deep-cloned documents.
        var normalizedCss = string.IsNullOrWhiteSpace(css) ? null : css;
        if (normalizedCss is not null)
        {
            (styles ??= new List<SvgCssStyleSource>()).Add(new SvgCssStyleSource(normalizedCss, baseUri));
        }

        if (svgDocument is { })
        {
            if (styles is { Count: > 0 })
            {
                if (captureCompatibilityStyleState)
                {
                    if (!preserveCompatibilityPresentationAttributes)
                    {
                        svgDocument.CaptureCompatibilityStyleState();
                    }

                    svgDocument.SetCompatibilityStyleSources(styles);
                }

                SvgCssCompatibilityProcessor.Apply(svgDocument, styles, elementFactory);
            }

            svgDocument.FlushStyles(true);
        }

        return svgDocument!;
    }

    private static bool MayContainCompatibilityStyleSources(string svg, string? css)
    {
        return !string.IsNullOrWhiteSpace(css) ||
               svg.Contains("<style", StringComparison.OrdinalIgnoreCase) ||
               svg.Contains("xml-stylesheet", StringComparison.OrdinalIgnoreCase) ||
               svg.Contains("@import", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MayContainCompatibilityStyleSources(Stream stream, string? css)
    {
        if (!string.IsNullOrWhiteSpace(css))
        {
            return true;
        }

        if (!stream.CanSeek)
        {
            return true;
        }

        var originalPosition = stream.Position;
        try
        {
            return StreamContainsCompatibilityStyleSourceToken(stream);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool StreamContainsCompatibilityStyleSourceToken(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var styleElementMatched = 0;
            var xmlStylesheetMatched = 0;
            var importMatched = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var value = ToAsciiLower(buffer[i]);
                    if (MatchAsciiToken(value, s_styleElementToken, ref styleElementMatched) ||
                        MatchAsciiToken(value, s_xmlStylesheetToken, ref xmlStylesheetMatched) ||
                        MatchAsciiToken(value, s_importToken, ref importMatched))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool MatchAsciiToken(byte value, byte[] token, ref int matched)
    {
        if (value == token[matched])
        {
            matched++;
            if (matched == token.Length)
            {
                matched = 0;
                return true;
            }

            return false;
        }

        matched = value == token[0] ? 1 : 0;
        return false;
    }

    private static byte ToAsciiLower(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + 32)
            : value;
    }

    private static T Create<T>(XmlReader reader, SvgElementFactory elementFactory, ref List<SvgCssStyleSource>? styles, Uri? baseUri)
        where T : SvgDocument, new()
    {
        var elementStack = new Stack<SvgElement>();
        var elementEmpty = false;
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

                        if (elementStack.Count > 0)
                        {
                            parent = elementStack.Peek();
                            if (parent is not null && element is not null)
                            {
                                parent.Children.Add(element);
                                parent.Nodes.Add(element);
                            }
                        }

                        elementStack.Push(element!);
                        if (elementEmpty)
                        {
                            goto case XmlNodeType.EndElement;
                        }

                        break;

                    case XmlNodeType.EndElement:
                        element = elementStack.Pop();

                        if (TryAggregateNodeContent(element, out var content))
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
                            // Preserve the document base URI with every collected <style> block so
                            // any nested @import inside that block resolves relative to the SVG file
                            // that declared it, not to the current process working directory.
                            (styles ??= new List<SvgCssStyleSource>()).Add(new SvgCssStyleSource(unknown.Content ?? string.Empty, svgDocument?.BaseUri));
                        }

                        break;

                    case XmlNodeType.CDATA:
                    case XmlNodeType.Text:
                    case XmlNodeType.SignificantWhitespace:
                        if (elementStack.Count > 0)
                        {
                            elementStack.Peek().Nodes.Add(new SvgContentNode { Content = reader.Value });
                        }

                        break;

                    case XmlNodeType.Whitespace:
                        if (elementStack.Count > 0 && ShouldPreserveTextWhitespace(elementStack.Peek()))
                        {
                            elementStack.Peek().Nodes.Add(new SvgContentNode { Content = reader.Value });
                        }

                        break;

                    case XmlNodeType.EntityReference:
                        reader.ResolveEntity();
                        if (elementStack.Count > 0)
                        {
                            elementStack.Peek().Nodes.Add(new SvgContentNode { Content = reader.Value });
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
        }

        return svgDocument!;
    }

    private static bool ShouldPreserveTextWhitespace(SvgElement element)
    {
        return element is SvgTextBase;
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
