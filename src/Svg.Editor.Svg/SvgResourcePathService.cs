using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Svg.Editor.Svg;

/// <summary>Preserves SVG resource locations when a document is saved to a different URI.</summary>
public static class SvgResourcePathService
{
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

    /// <summary>
    /// Rebases relative hrefs, CSS URLs and stylesheet imports without loading their resources.
    /// Relative xml:base values are rebased before their descendants are visited. Absolute
    /// references, data URIs and document-local fragments retain their original spelling.
    /// </summary>
    /// <remarks>
    /// XML whitespace and CDATA are retained, but XML serialization may normalize attribute
    /// quoting and entities. External DTDs are never loaded. Invalid XML, base URIs or resource
    /// references fail the operation rather than silently saving broken relative links.
    /// </remarks>
    public static string Rebase(string xml, Uri originalDocumentUri, Uri targetDocumentUri)
    {
        if (xml is null)
            throw new ArgumentNullException(nameof(xml));
        ValidateDocumentUri(originalDocumentUri, nameof(originalDocumentUri));
        ValidateDocumentUri(targetDocumentUri, nameof(targetDocumentUri));

        if (SameUri(originalDocumentUri, targetDocumentUri))
            return xml;

        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = 1024 * 1024
        };
        using (var text = new StringReader(xml))
        using (var reader = XmlReader.Create(text, settings))
            document.Load(reader);

        RebaseChildren(document, originalDocumentUri, targetDocumentUri);
        return document.OuterXml;
    }

    private static void ValidateDocumentUri(Uri uri, string name)
    {
        if (uri is null)
            throw new ArgumentNullException(name);
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("The document URI must be absolute.", name);
    }

    private static bool SameUri(Uri first, Uri second)
        => string.Equals(first.AbsoluteUri, second.AbsoluteUri, StringComparison.Ordinal);

    private static void RebaseChildren(XmlNode parent, Uri originalBase, Uri targetBase)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is XmlElement element)
                RebaseElement(element, originalBase, targetBase);
            else if (child is XmlProcessingInstruction instruction && instruction.Target == "xml-stylesheet")
                RebaseStylesheetInstruction(instruction, originalBase, targetBase);
        }
    }

    private static void RebaseElement(XmlElement element, Uri originalBase, Uri targetBase)
    {
        var baseAttribute = element.GetAttributeNode("base", XmlNamespace);
        if (baseAttribute is not null)
        {
            var originalElementBase = Resolve(originalBase, baseAttribute.Value);
            baseAttribute.Value = RebaseReference(baseAttribute.Value, originalBase, targetBase);
            targetBase = Resolve(targetBase, baseAttribute.Value);
            originalBase = originalElementBase;
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (attribute.LocalName == "href" && (attribute.NamespaceURI.Length == 0 || attribute.NamespaceURI == XLinkNamespace))
                attribute.Value = RebaseReference(attribute.Value, originalBase, targetBase);
            else if (attribute.NamespaceURI.Length == 0 && IsCssAttribute(attribute.LocalName))
                attribute.Value = RebaseCss(attribute.Value, originalBase, targetBase);
        }

        if (element.LocalName == "style")
        {
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlText || child is XmlCDataSection)
                    child.Value = RebaseCss(child.Value ?? string.Empty, originalBase, targetBase);
            }
        }

        RebaseChildren(element, originalBase, targetBase);
    }

    private static bool IsCssAttribute(string name)
        => name == "style" || name == "fill" || name == "stroke" || name == "filter"
            || name == "clip-path" || name == "mask" || name == "cursor" || name == "marker"
            || name == "marker-start" || name == "marker-mid" || name == "marker-end" || name == "color-profile";

    private static Uri Resolve(Uri baseUri, string reference)
    {
        if (!Uri.TryCreate(baseUri, reference.Trim(), out var resolved))
            throw new UriFormatException("A relative SVG resource or xml:base value could not be resolved.");
        return resolved;
    }

    private static string RebaseReference(string value, Uri originalBase, Uri targetBase)
    {
        var reference = value.Trim();
        if (reference.Length == 0 || reference[0] == '#' || HasScheme(reference) || SameUri(originalBase, targetBase))
            return value;

        var resource = Resolve(originalBase, reference);
        if (SameUri(resource, Resolve(targetBase, reference)))
            return value;

        var relative = targetBase.MakeRelativeUri(resource);
        var rebased = relative.IsAbsoluteUri ? relative.AbsoluteUri : relative.OriginalString;
        var start = value.IndexOf(reference, StringComparison.Ordinal);
        return value.Substring(0, start) + rebased + value.Substring(start + reference.Length);
    }

    private static bool HasScheme(string value)
    {
        if (!IsAsciiLetter(value[0]))
            return false;
        for (var i = 1; i < value.Length; i++)
        {
            var character = value[i];
            if (character == ':')
                return true;
            if (!IsAsciiLetter(character) && (character < '0' || character > '9') && character != '+' && character != '-' && character != '.')
                break;
        }
        return false;
    }

    private static bool IsAsciiLetter(char value)
        => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');

    private static void RebaseStylesheetInstruction(XmlProcessingInstruction instruction, Uri originalBase, Uri targetBase)
    {
        // Parse XML pseudo-attributes with the XML parser, so quoted titles and escaped ampersands
        // cannot be mistaken for hrefs. Unknown/malformed instructions remain verbatim.
        var attributes = new XmlDocument { XmlResolver = null };
        try
        {
            attributes.LoadXml("<stylesheet " + instruction.Data + " />");
        }
        catch (XmlException)
        {
            return;
        }
        var element = attributes.DocumentElement!;
        var href = element.GetAttributeNode("href");
        if (href is null)
            return;
        var rebased = RebaseReference(href.Value, originalBase, targetBase);
        if (rebased == href.Value)
            return;
        href.Value = rebased;
        var result = new StringBuilder();
        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (result.Length > 0)
                result.Append(' ');
            result.Append(attribute.OuterXml);
        }
        instruction.Data = result.ToString();
    }

    private static string RebaseCss(string css, Uri originalBase, Uri targetBase)
    {
        if (SameUri(originalBase, targetBase))
            return css;

        var result = new StringBuilder();
        var copied = 0;
        for (var i = 0; i < css.Length; i++)
        {
            if (IsComment(css, i))
            {
                i = SkipComment(css, i) - 1;
                continue;
            }
            if (css[i] == '\'' || css[i] == '"')
            {
                i = FindStringEnd(css, i) - 1;
                continue;
            }

            if (css[i] == '@' && MatchesIdentifier(css, i + 1, "import"))
            {
                var start = SkipCssTrivia(css, i + 7);
                if (start < css.Length && (css[start] == '\'' || css[start] == '"'))
                {
                    var end = FindStringEnd(css, start);
                    if (end <= css.Length && end > start + 1 && css[end - 1] == css[start])
                        ReplaceCssReference(css, start + 1, end - start - 2, css[start], originalBase, targetBase, result, ref copied);
                    i = end - 1;
                    continue;
                }
            }

            if ((i > 0 && IsIdentifierCharacter(css[i - 1])) || !MatchesIdentifier(css, i, "url"))
                continue;
            var open = SkipCssTrivia(css, i + 3);
            if (open >= css.Length || css[open] != '(')
                continue;
            var valueStart = open + 1;
            while (valueStart < css.Length && IsCssWhitespace(css[valueStart]))
                valueStart++;
            if (valueStart == css.Length)
                break;

            var quote = css[valueStart] == '\'' || css[valueStart] == '"' ? css[valueStart] : '\0';
            var valueEnd = valueStart;
            var close = valueStart;
            if (quote != '\0')
            {
                var end = FindStringEnd(css, valueStart);
                if (end > css.Length || end <= valueStart + 1 || css[end - 1] != quote)
                    break;
                valueStart++;
                valueEnd = end - 1;
                close = end;
                while (close < css.Length && IsCssWhitespace(css[close]))
                    close++;
            }
            else
            {
                while (close < css.Length && css[close] != ')')
                    close = css[close] == '\\' ? SkipEscape(css, close) : close + 1;
                valueEnd = close;
                while (valueEnd > valueStart && IsCssWhitespace(css[valueEnd - 1]))
                    valueEnd--;
            }

            if (close >= css.Length || css[close] != ')')
                continue;
            ReplaceCssReference(css, valueStart, valueEnd - valueStart, quote, originalBase, targetBase, result, ref copied);
            i = close;
        }

        return copied == 0 ? css : result.Append(css, copied, css.Length - copied).ToString();
    }

    private static void ReplaceCssReference(string css, int start, int length, char quote, Uri originalBase, Uri targetBase, StringBuilder result, ref int copied)
    {
        var value = DecodeCss(css.Substring(start, length));
        var rebased = RebaseReference(value, originalBase, targetBase);
        if (rebased == value)
            return;
        result.Append(css, copied, start - copied);
        foreach (var character in rebased)
        {
            if (character == '\\' || character == quote || (quote == '\0' && (character == '(' || character == ')' || character == '\'' || character == '"' || IsCssWhitespace(character))))
                result.Append('\\');
            result.Append(character);
        }
        copied = start + length;
    }

    private static bool MatchesIdentifier(string text, int start, string identifier)
        => start + identifier.Length <= text.Length
            && string.Compare(text, start, identifier, 0, identifier.Length, StringComparison.OrdinalIgnoreCase) == 0
            && (start + identifier.Length == text.Length || !IsIdentifierCharacter(text[start + identifier.Length]));

    private static bool IsIdentifierCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_' || value == '-' || value == '\\' || value >= 128;

    private static bool IsCssWhitespace(char value)
        => value == ' ' || value == '\t' || value == '\r' || value == '\n' || value == '\f';

    private static bool IsComment(string text, int index)
        => index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*';

    private static int SkipComment(string text, int index)
    {
        var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 2;
    }

    private static int SkipCssTrivia(string text, int index)
    {
        while (index < text.Length)
        {
            if (IsCssWhitespace(text[index]))
                index++;
            else if (IsComment(text, index))
                index = SkipComment(text, index);
            else
                break;
        }
        return index;
    }

    private static int FindStringEnd(string text, int start)
    {
        var quote = text[start];
        for (var i = start + 1; i < text.Length;)
        {
            if (text[i] == quote)
                return i + 1;
            i = text[i] == '\\' ? SkipEscape(text, i) : i + 1;
        }
        return text.Length + 1;
    }

    private static int SkipEscape(string text, int index)
        => index + 2 < text.Length && text[index + 1] == '\r' && text[index + 2] == '\n'
            ? index + 3 : Math.Min(index + 2, text.Length);

    private static string DecodeCss(string value)
    {
        if (value.IndexOf('\\') < 0)
            return value;
        var result = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\')
            {
                result.Append(value[i]);
                continue;
            }
            if (++i == value.Length)
                break;
            if (value[i] == '\r' || value[i] == '\n' || value[i] == '\f')
            {
                if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                    i++;
                continue;
            }
            var start = i;
            while (i < value.Length && i - start < 6 && Uri.IsHexDigit(value[i]))
                i++;
            if (i == start)
                result.Append(value[i]);
            else
            {
                var codePoint = int.Parse(value.Substring(start, i - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                result.Append(codePoint == 0 || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                    ? "\uFFFD" : char.ConvertFromUtf32(codePoint));
                if (i < value.Length && IsCssWhitespace(value[i]))
                {
                    if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                        i++;
                }
                else
                    i--;
            }
        }
        return result.ToString();
    }
}
