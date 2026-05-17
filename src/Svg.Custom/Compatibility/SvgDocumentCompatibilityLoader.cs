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
    private const int CompatibilityStyleSourceScanBufferSize = 8 * 1024;
    private const int CompatibilityStyleSourceTokenTailSize = 15;

    public static T Open<T>(string path, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        return Open<T>(path, svgOptions, captureCompatibilityStyleState: false);
    }

    public static T Open<T>(string path, SvgOptions svgOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        return Open<T>(path, svgOptions, loadOptions: null, captureCompatibilityStyleState);
    }

    public static T Open<T>(string path, SvgOptions svgOptions, SvgDocumentLoadOptions? loadOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Capture the absolute document URI before the stream is opened so later CSS resolution can
        // expand relative @import/file references exactly as the browser would relative to the SVG.
        var baseUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        using var stream = File.OpenRead(path);
        return Open<T>(stream, svgOptions, baseUri, loadOptions, captureCompatibilityStyleState);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, captureCompatibilityStyleState: false);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, loadOptions: null, captureCompatibilityStyleState);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions, SvgDocumentLoadOptions? loadOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
    {
        return Open<T>(stream, svgOptions, null, loadOptions, captureCompatibilityStyleState);
    }

    public static T Open<T>(Stream stream, SvgOptions svgOptions, Uri? baseUri, SvgDocumentLoadOptions? loadOptions, bool captureCompatibilityStyleState) where T : SvgDocument, new()
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
            loadOptions,
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
            loadOptions: null,
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

        return Create<T>(svgReader, baseUri: baseUri, loadOptions: null, captureCompatibilityStyleState: captureCompatibilityStyleState);
    }

    private static T Create<T>(
        XmlReader reader,
        string? css = null,
        Uri? baseUri = null,
        SvgDocumentLoadOptions? loadOptions = null,
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
        var svgDocument = Create<T>(reader, elementFactory, ref styles, baseUri, loadOptions);
        svgDocument.LoadOptions = loadOptions;

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

                SvgCssCompatibilityProcessor.Apply(svgDocument, styles, elementFactory, svgDocument.LoadOptions);
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
        var buffer = ArrayPool<byte>.Shared.Rent(CompatibilityStyleSourceScanBufferSize);
        Span<byte> tail = stackalloc byte[CompatibilityStyleSourceTokenTailSize];
        var tailLength = 0;
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                var span = buffer.AsSpan(0, read);
                if ((tailLength > 0 && BoundaryContainsCompatibilityStyleSourceToken(tail.Slice(0, tailLength), span)) ||
                    SpanContainsCompatibilityStyleSourceToken(span))
                {
                    return true;
                }

                tailLength = UpdateCompatibilityStyleSourceScanTail(span, tail, tailLength);
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool BoundaryContainsCompatibilityStyleSourceToken(ReadOnlySpan<byte> tail, ReadOnlySpan<byte> head)
    {
        return BoundaryContainsAsciiToken(tail, head, s_styleElementToken) ||
               BoundaryContainsAsciiToken(tail, head, s_xmlStylesheetToken) ||
               BoundaryContainsAsciiToken(tail, head, s_importToken);
    }

    private static int UpdateCompatibilityStyleSourceScanTail(ReadOnlySpan<byte> span, Span<byte> tail, int tailLength)
    {
        if (span.Length >= tail.Length)
        {
            span.Slice(span.Length - tail.Length).CopyTo(tail);
            return tail.Length;
        }

        var totalLength = tailLength + span.Length;
        if (totalLength <= tail.Length)
        {
            span.CopyTo(tail.Slice(tailLength));
            return totalLength;
        }

        var keepFromTail = tail.Length - span.Length;
        if (keepFromTail > 0)
        {
            tail.Slice(tailLength - keepFromTail, keepFromTail).CopyTo(tail);
        }

        span.CopyTo(tail.Slice(keepFromTail));
        return tail.Length;
    }

    private static bool BoundaryContainsAsciiToken(ReadOnlySpan<byte> tail, ReadOnlySpan<byte> head, ReadOnlySpan<byte> token)
    {
        var maxSplit = Math.Min(token.Length - 1, tail.Length);
        for (var split = 1; split <= maxSplit; split++)
        {
            var headLength = token.Length - split;
            if (head.Length < headLength)
            {
                continue;
            }

            if (EndsWithAsciiIgnoreCase(tail, token.Slice(0, split)) &&
                StartsWithAsciiIgnoreCase(head, token.Slice(split, headLength)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SpanContainsCompatibilityStyleSourceToken(ReadOnlySpan<byte> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            switch (ToAsciiLower(span[i]))
            {
                case (byte)'<':
                    if (SpanStartsWithAsciiTokenAt(span, i, s_styleElementToken))
                    {
                        return true;
                    }

                    break;
                case (byte)'x':
                    if (SpanStartsWithAsciiTokenAt(span, i, s_xmlStylesheetToken))
                    {
                        return true;
                    }

                    break;
                case (byte)'@':
                    if (SpanStartsWithAsciiTokenAt(span, i, s_importToken))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool SpanStartsWithAsciiTokenAt(ReadOnlySpan<byte> span, int index, ReadOnlySpan<byte> token)
    {
        return span.Length - index >= token.Length &&
               StartsWithAsciiIgnoreCase(span.Slice(index, token.Length), token);
    }

    private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> token)
    {
        for (var i = 0; i < token.Length; i++)
        {
            if (ToAsciiLower(value[i]) != ToAsciiLower(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EndsWithAsciiIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> token)
    {
        return value.Length >= token.Length &&
               StartsWithAsciiIgnoreCase(value.Slice(value.Length - token.Length, token.Length), token);
    }

    private static byte ToAsciiLower(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + 32)
            : value;
    }

    private static T Create<T>(
        XmlReader reader,
        SvgElementFactory elementFactory,
        ref List<SvgCssStyleSource>? styles,
        Uri? baseUri,
        SvgDocumentLoadOptions? loadOptions)
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
                        else if (element is SvgUnknownElement link &&
                                 link.ElementName == "link" &&
                                 TryGetLinkedStylesheetHref(link, out var href) &&
                                 SvgCssCompatibilityProcessor.TryLoadLinkedStylesheet(href, svgDocument?.BaseUri ?? baseUri, loadOptions) is { } linkedStylesheet)
                        {
                            (styles ??= new List<SvgCssStyleSource>()).Add(linkedStylesheet);
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
                        if (elementStack.Count > 0 && ShouldPreserveWhitespaceNode(elementStack.Peek(), elementFactory))
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

                    case XmlNodeType.ProcessingInstruction:
                        if (reader.Name.Equals("xml-stylesheet", StringComparison.OrdinalIgnoreCase) &&
                            TryGetXmlStylesheetHref(reader.Value, out var stylesheetHref) &&
                            SvgCssCompatibilityProcessor.TryLoadLinkedStylesheet(stylesheetHref, svgDocument?.BaseUri ?? baseUri, loadOptions) is { } xmlStylesheet)
                        {
                            (styles ??= new List<SvgCssStyleSource>()).Add(xmlStylesheet);
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

    private static bool TryGetLinkedStylesheetHref(SvgUnknownElement link, out string href)
    {
        href = string.Empty;

        if (!TryGetAttributeIgnoreCase(link, "rel", out var rel) ||
            !ContainsStylesheetRel(rel))
        {
            return false;
        }

        if (TryGetAttributeIgnoreCase(link, "type", out var type) &&
            !IsCssStylesheetType(type))
        {
            return false;
        }

        return TryGetAttributeIgnoreCase(link, "href", out href) &&
               !string.IsNullOrWhiteSpace(href);
    }

    private static bool TryGetXmlStylesheetHref(string value, out string href)
    {
        href = string.Empty;
        var attributes = ParsePseudoAttributes(value);

        if (attributes.TryGetValue("type", out var type) &&
            !IsCssStylesheetType(type))
        {
            return false;
        }

        if (!attributes.TryGetValue("href", out var parsedHref) ||
            string.IsNullOrWhiteSpace(parsedHref))
        {
            return false;
        }

        href = parsedHref;
        return true;
    }

    private static bool TryGetAttributeIgnoreCase(SvgElement element, string name, out string value)
    {
        if (element.TryGetAttribute(name, out value) && value is not null)
        {
            return true;
        }

        foreach (var attribute in element.CustomAttributes)
        {
            if (attribute.Value is not null &&
                attribute.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsStylesheetRel(string rel)
    {
        foreach (var token in rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("stylesheet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCssStylesheetType(string type)
    {
        return string.IsNullOrWhiteSpace(type) ||
               type.Trim().Equals("text/css", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParsePseudoAttributes(string value)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        while (index < value.Length)
        {
            SkipWhitespace(value, ref index);
            var nameStart = index;
            while (index < value.Length &&
                   !char.IsWhiteSpace(value[index]) &&
                   value[index] != '=')
            {
                index++;
            }

            if (index == nameStart)
            {
                break;
            }

            var name = value.Substring(nameStart, index - nameStart);
            SkipWhitespace(value, ref index);
            if (index >= value.Length || value[index] != '=')
            {
                continue;
            }

            index++;
            SkipWhitespace(value, ref index);
            if (index >= value.Length || value[index] is not ('\'' or '"'))
            {
                continue;
            }

            var quote = value[index++];
            var valueStart = index;
            while (index < value.Length && value[index] != quote)
            {
                index++;
            }

            if (index >= value.Length)
            {
                break;
            }

            attributes[name] = value.Substring(valueStart, index - valueStart);
            index++;
        }

        return attributes;
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static bool ShouldPreserveWhitespaceNode(SvgElement element, SvgElementFactory elementFactory)
    {
        return elementFactory.PreserveJavaScriptDomState || element is SvgTextBase;
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
