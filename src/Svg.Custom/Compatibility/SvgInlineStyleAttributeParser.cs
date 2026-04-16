#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using ExCSS;

namespace Svg;

/// <summary>
/// Parses and applies inline <c>style="..."</c> attribute declarations independently from
/// element creation so the XML factory remains focused on attribute dispatch.
/// </summary>
internal sealed class SvgInlineStyleAttributeParser
{
    private const int SharedInlineStyleCacheLimit = 512;

    private readonly struct InlineStyleDeclaration
    {
        public InlineStyleDeclaration(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }
    }

    private readonly struct CachedInlineStyleDeclarations
    {
        public CachedInlineStyleDeclarations(InlineStyleDeclaration[] declarations)
        {
            Declarations = declarations;
        }

        public InlineStyleDeclaration[] Declarations { get; }
    }

    private static readonly ConcurrentDictionary<string, CachedInlineStyleDeclarations> s_sharedInlineStyleDeclarationsCache = new(StringComparer.Ordinal);
    private readonly StylesheetParser stylesheetParser = new(true, true, tolerateInvalidValues: true);

    public bool ApplyStyles(SvgElement element, string styleText)
    {
        return ApplyStyles(element, styleText, document: null, eagerApply: false, out _);
    }

    public bool ApplyStyles(
        SvgElement element,
        string styleText,
        SvgDocument? document,
        bool eagerApply,
        out bool stagedStyles)
    {
        if (string.IsNullOrWhiteSpace(styleText))
        {
            stagedStyles = false;
            return false;
        }

        if (TryGetCachedInlineDeclarations(styleText, out var cachedDeclarations))
        {
            stagedStyles = ApplyDeclarations(element, cachedDeclarations, document, eagerApply);
            return cachedDeclarations.Length > 0;
        }

        if (TryParseInlineDeclarations(styleText, out var inlineDeclarations))
        {
            CacheInlineDeclarations(styleText, inlineDeclarations);
            stagedStyles = ApplyDeclarations(element, inlineDeclarations, document, eagerApply);
            return inlineDeclarations.Length > 0;
        }

        var fallbackDeclarations = ParseInlineDeclarationsWithStylesheetParser(styleText);
        CacheInlineDeclarations(styleText, fallbackDeclarations);
        stagedStyles = ApplyDeclarations(element, fallbackDeclarations, document, eagerApply);
        return fallbackDeclarations.Length > 0;
    }

    private static bool TryGetCachedInlineDeclarations(string styleText, out InlineStyleDeclaration[] declarations)
    {
        if (s_sharedInlineStyleDeclarationsCache.TryGetValue(styleText, out var cachedDeclarations))
        {
            declarations = cachedDeclarations.Declarations;
            return true;
        }

        declarations = Array.Empty<InlineStyleDeclaration>();
        return false;
    }

    private static void CacheInlineDeclarations(string styleText, InlineStyleDeclaration[] declarations)
    {
        TrimSharedInlineStyleCacheIfNeeded();
        s_sharedInlineStyleDeclarationsCache[styleText] = new CachedInlineStyleDeclarations(declarations);
    }

    private static void TrimSharedInlineStyleCacheIfNeeded()
    {
        if (s_sharedInlineStyleDeclarationsCache.Count > SharedInlineStyleCacheLimit)
        {
            s_sharedInlineStyleDeclarationsCache.Clear();
        }
    }

    internal static void ClearSharedCacheForBenchmarks()
    {
        s_sharedInlineStyleDeclarationsCache.Clear();
    }

    private static bool ApplyDeclarations(
        SvgElement element,
        InlineStyleDeclaration[] declarations,
        SvgDocument? document,
        bool eagerApply)
    {
        var stagedStyles = false;
        for (var i = 0; i < declarations.Length; i++)
        {
            var declaration = declarations[i];
            if (eagerApply &&
                document is not null &&
                SvgElementFactory.SetPropertyValue(element, string.Empty, declaration.Name, declaration.Value, document, true))
            {
                continue;
            }

            element.AddStyleCompatibility(declaration.Name, declaration.Value, SvgElement.StyleSpecificity_InlineStyle);
            stagedStyles = true;
        }

        return stagedStyles;
    }

    private InlineStyleDeclaration[] ParseInlineDeclarationsWithStylesheetParser(string styleText)
    {
        var inlineSheet = stylesheetParser.Parse("#a{" + styleText + "}");
        List<InlineStyleDeclaration> declarations = null!;
        foreach (var rule in inlineSheet.StyleRules)
        {
            foreach (var declaration in rule.Style)
            {
                declarations ??= new List<InlineStyleDeclaration>();
                declarations.Add(new InlineStyleDeclaration(declaration.Name, declaration.Original));
            }
        }

        return declarations is null ? Array.Empty<InlineStyleDeclaration>() : declarations.ToArray();
    }

    private static bool TryParseInlineDeclarations(string styleText, out InlineStyleDeclaration[] declarations)
    {
        var parsedDeclarations = new List<InlineStyleDeclaration>();
        var index = 0;
        while (true)
        {
            if (!SkipIgnorableStyleContent(styleText, ref index))
            {
                declarations = Array.Empty<InlineStyleDeclaration>();
                return false;
            }

            if (index >= styleText.Length)
            {
                declarations = parsedDeclarations.Count == 0 ? Array.Empty<InlineStyleDeclaration>() : parsedDeclarations.ToArray();
                return true;
            }

            if (!TryReadInlineDeclaration(styleText, ref index, out var name, out var value))
            {
                declarations = Array.Empty<InlineStyleDeclaration>();
                return false;
            }

            parsedDeclarations.Add(new InlineStyleDeclaration(name, value));
        }
    }

    private static bool TryReadInlineDeclaration(string styleText, ref int index, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        var declarationStart = index;
        if (!TryFindInlineDeclarationEnd(styleText, ref index, out var declarationEnd))
        {
            return false;
        }

        if (!TryFindInlineDeclarationSeparator(styleText, declarationStart, declarationEnd, out var separatorIndex))
        {
            return false;
        }

        name = NormalizeInlineDeclarationName(styleText, declarationStart, separatorIndex - declarationStart);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        value = NormalizeInlineDeclarationSegment(styleText, separatorIndex + 1, declarationEnd - separatorIndex - 1);
        return true;
    }

    private static bool TryFindInlineDeclarationEnd(string styleText, ref int index, out int declarationEnd)
    {
        var quote = '\0';
        var escape = false;
        var parentheses = 0;
        var current = index;

        while (current < styleText.Length)
        {
            var character = styleText[current];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (character == '/' && current + 1 < styleText.Length && styleText[current + 1] == '*')
            {
                current += 2;
                if (!TrySkipStyleComment(styleText, ref current))
                {
                    declarationEnd = 0;
                    return false;
                }

                continue;
            }

            switch (character)
            {
                case '\'':
                case '"':
                    quote = character;
                    break;
                case '(':
                    parentheses++;
                    break;
                case ')':
                    if (parentheses > 0)
                    {
                        parentheses--;
                    }
                    break;
                case ';' when parentheses == 0:
                    declarationEnd = current;
                    current++;
                    index = current;
                    return true;
            }

            current++;
        }

        declarationEnd = current;
        index = current;
        return quote == '\0' && parentheses == 0;
    }

    private static bool TryFindInlineDeclarationSeparator(string styleText, int startIndex, int endIndex, out int separatorIndex)
    {
        var quote = '\0';
        var escape = false;
        var parentheses = 0;

        for (var i = startIndex; i < endIndex; i++)
        {
            var character = styleText[i];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character == '/' && i + 1 < endIndex && styleText[i + 1] == '*')
            {
                i += 2;
                if (!TrySkipStyleComment(styleText, ref i, endIndex))
                {
                    separatorIndex = -1;
                    return false;
                }

                i--;
                continue;
            }

            switch (character)
            {
                case '\'':
                case '"':
                    quote = character;
                    break;
                case '(':
                    parentheses++;
                    break;
                case ')':
                    if (parentheses > 0)
                    {
                        parentheses--;
                    }
                    break;
                case ':' when parentheses == 0:
                    separatorIndex = i;
                    return true;
            }
        }

        separatorIndex = -1;
        return false;
    }

    private static bool SkipIgnorableStyleContent(string styleText, ref int index)
    {
        while (index < styleText.Length)
        {
            var character = styleText[index];
            if (char.IsWhiteSpace(character) || character == ';')
            {
                index++;
                continue;
            }

            if (character == '/' && index + 1 < styleText.Length && styleText[index + 1] == '*')
            {
                index += 2;
                if (!TrySkipStyleComment(styleText, ref index))
                {
                    return false;
                }

                continue;
            }

            break;
        }

        return true;
    }

    private static bool TrySkipStyleComment(string styleText, ref int index)
    {
        return TrySkipStyleComment(styleText, ref index, styleText.Length);
    }

    private static bool TrySkipStyleComment(string styleText, ref int index, int endIndex)
    {
        while (index + 1 < endIndex)
        {
            if (styleText[index] == '*' && styleText[index + 1] == '/')
            {
                index += 2;
                return true;
            }

            index++;
        }

        return false;
    }

    private static string NormalizeInlineDeclarationSegment(string styleText, int startIndex, int length)
    {
        if (!TryTrimSegment(styleText, startIndex, length, out var trimmedStart, out var trimmedLength))
        {
            return string.Empty;
        }

        StringBuilder? builder = null;
        var quote = '\0';
        var escape = false;
        var segmentEnd = trimmedStart + trimmedLength;

        for (var i = trimmedStart; i < segmentEnd; i++)
        {
            var character = styleText[i];
            if (quote != '\0')
            {
                builder?.Append(character);
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character == '/' && i + 1 < segmentEnd && styleText[i + 1] == '*')
            {
                builder ??= new StringBuilder(trimmedLength);
                if (builder.Length == 0 && i > trimmedStart)
                {
                    builder.Append(styleText, trimmedStart, i - trimmedStart);
                }

                i += 2;
                if (!TrySkipStyleComment(styleText, ref i, segmentEnd))
                {
                    return styleText.Substring(trimmedStart, trimmedLength);
                }

                i--;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }

            builder?.Append(character);
        }

        return builder is null
            ? styleText.Substring(trimmedStart, trimmedLength)
            : builder.ToString().Trim();
    }

    private static string NormalizeInlineDeclarationName(string styleText, int startIndex, int length)
    {
        var normalized = NormalizeInlineDeclarationSegment(styleText, startIndex, length);
        if (string.IsNullOrEmpty(normalized))
        {
            return normalized;
        }

        StringBuilder? builder = null;
        for (var i = 0; i < normalized.Length; i++)
        {
            var character = normalized[i];
            if (character is < 'A' or > 'Z')
            {
                if (builder is not null)
                {
                    builder.Append(character);
                }

                continue;
            }

            builder ??= new StringBuilder(normalized.Length).Append(normalized, 0, i);
            builder.Append((char)(character + ('a' - 'A')));
        }

        return builder?.ToString() ?? normalized;
    }

    private static bool TryTrimSegment(string styleText, int startIndex, int length, out int trimmedStart, out int trimmedLength)
    {
        if (length <= 0)
        {
            trimmedStart = 0;
            trimmedLength = 0;
            return false;
        }

        var endIndex = startIndex + length - 1;
        while (startIndex <= endIndex && char.IsWhiteSpace(styleText[startIndex]))
        {
            startIndex++;
        }

        while (endIndex >= startIndex && char.IsWhiteSpace(styleText[endIndex]))
        {
            endIndex--;
        }

        if (startIndex > endIndex)
        {
            trimmedStart = 0;
            trimmedLength = 0;
            return false;
        }

        trimmedStart = startIndex;
        trimmedLength = endIndex - startIndex + 1;
        return true;
    }
}
