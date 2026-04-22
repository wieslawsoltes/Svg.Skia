#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Svg;

internal static class SvgCssVariableResolver
{
    private const int MaxResolutionDepth = 32;
    private static readonly ConditionalWeakTable<SvgElement, Dictionary<string, SortedDictionary<int, string>>> s_customPropertyRules = new();

    public static bool IsCustomPropertyName(string? name)
    {
        return name is { Length: > 2 } && name.StartsWith("--", StringComparison.Ordinal);
    }

    public static void AddCustomProperty(SvgElement element, string name, string value, int specificity)
    {
        if (!IsCustomPropertyName(name))
        {
            return;
        }

        var propertyRules = s_customPropertyRules.GetOrCreateValue(element);
        if (!propertyRules.TryGetValue(name, out var rules))
        {
            rules = new SortedDictionary<int, string>();
            propertyRules[name] = rules;
        }

        while (rules.ContainsKey(specificity))
        {
            specificity++;
        }

        rules[specificity] = value;
        element.CustomAttributes[name] = rules.Last().Value;
    }

    public static bool TryResolveValue(SvgElement element, string value, out string resolved)
    {
        resolved = value;
        if (!TryFindNextVarFunction(value, 0, out _, out _, out _))
        {
            return false;
        }

        return TryResolveValueCore(element, value, new HashSet<string>(StringComparer.Ordinal), 0, out resolved);
    }

    public static bool TryResolveFallbacks(string value, out string resolved)
    {
        resolved = value;
        if (!TryFindNextVarFunction(value, 0, out _, out _, out _))
        {
            return false;
        }

        return TryResolveValueCore(null, value, new HashSet<string>(StringComparer.Ordinal), 0, out resolved);
    }

    private static bool TryResolveValueCore(
        SvgElement? element,
        string value,
        HashSet<string> resolvingProperties,
        int depth,
        out string resolved)
    {
        resolved = value;
        if (depth > MaxResolutionDepth)
        {
            return false;
        }

        StringBuilder? builder = null;
        var index = 0;
        while (TryFindNextVarFunction(value, index, out var functionStart, out var contentStart, out var functionEnd))
        {
            builder ??= new StringBuilder(value.Length);
            builder.Append(value, index, functionStart - index);

            var contentLength = functionEnd - contentStart;
            if (!TryResolveVariableFunction(
                    element,
                    value.Substring(contentStart, contentLength),
                    resolvingProperties,
                    depth + 1,
                    out var replacement))
            {
                return false;
            }

            builder.Append(replacement);
            index = functionEnd + 1;
        }

        if (builder is null)
        {
            return true;
        }

        builder.Append(value, index, value.Length - index);
        resolved = builder.ToString();
        return true;
    }

    private static bool TryResolveVariableFunction(
        SvgElement? element,
        string content,
        HashSet<string> resolvingProperties,
        int depth,
        out string replacement)
    {
        replacement = string.Empty;
        if (!TryFindTopLevelComma(content, out var commaIndex))
        {
            commaIndex = content.Length;
        }

        var name = content.Substring(0, commaIndex).Trim();
        if (!IsCustomPropertyName(name))
        {
            return false;
        }

        if (TryGetCustomPropertyValue(element, name, resolvingProperties, depth, out replacement))
        {
            return true;
        }

        if (commaIndex >= content.Length)
        {
            return false;
        }

        var fallback = content.Substring(commaIndex + 1).Trim();
        return TryResolveValueCore(element, fallback, resolvingProperties, depth + 1, out replacement);
    }

    private static bool TryGetCustomPropertyValue(
        SvgElement? element,
        string name,
        HashSet<string> resolvingProperties,
        int depth,
        out string value)
    {
        value = string.Empty;
        if (element is null || !resolvingProperties.Add(name))
        {
            return false;
        }

        try
        {
            foreach (var current in element.ParentsAndSelf)
            {
                if (!current.CustomAttributes.TryGetValue(name, out var rawValue))
                {
                    continue;
                }

                return TryResolveValueCore(current, rawValue, resolvingProperties, depth + 1, out value);
            }

            return false;
        }
        finally
        {
            resolvingProperties.Remove(name);
        }
    }

    private static bool TryFindNextVarFunction(string value, int startIndex, out int functionStart, out int contentStart, out int functionEnd)
    {
        functionStart = -1;
        contentStart = -1;
        functionEnd = -1;

        var quote = '\0';
        var escape = false;
        for (var i = startIndex; i < value.Length; i++)
        {
            var character = value[i];
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

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if ((i > 0 && IsCssIdentifierCharacter(value[i - 1])) ||
                i + 4 > value.Length ||
                !value.AsSpan(i, 3).Equals("var".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                value[i + 3] != '(')
            {
                continue;
            }

            if (!TryFindFunctionEnd(value, i + 3, out functionEnd))
            {
                return false;
            }

            functionStart = i;
            contentStart = i + 4;
            return true;
        }

        return false;
    }

    private static bool TryFindFunctionEnd(string value, int openParenthesisIndex, out int closeParenthesisIndex)
    {
        closeParenthesisIndex = -1;
        var quote = '\0';
        var escape = false;
        var parentheses = 0;

        for (var i = openParenthesisIndex; i < value.Length; i++)
        {
            var character = value[i];
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
                    parentheses--;
                    if (parentheses == 0)
                    {
                        closeParenthesisIndex = i;
                        return true;
                    }

                    if (parentheses < 0)
                    {
                        return false;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool IsCssIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '-' or '_';
    }

    private static bool TryFindTopLevelComma(string value, out int commaIndex)
    {
        commaIndex = -1;
        var quote = '\0';
        var escape = false;
        var parentheses = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
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
                case ',' when parentheses == 0:
                    commaIndex = i;
                    return true;
            }
        }

        return false;
    }
}
