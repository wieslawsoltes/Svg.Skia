#nullable enable

using System;

namespace Svg;

internal static class SvgCssDeclarationPriority
{
    private const string ImportantKeyword = "important";
    internal const int ImportantSpecificityOffset = 1 << 24;

    public static int NormalizePriority(ref string value, int specificity, bool important = false)
    {
        if (!TryFindTrailingImportant(value, out var importantStartIndex))
        {
            return important ? specificity + ImportantSpecificityOffset : specificity;
        }

        value = value.Substring(0, importantStartIndex).TrimEnd();
        return specificity + ImportantSpecificityOffset;
    }

    public static bool TryFindTrailingImportant(string value, out int importantStartIndex)
    {
        importantStartIndex = -1;
        var index = value.Length - 1;
        if (!MoveBeforeTrailingWhitespaceAndComments(value, ref index))
        {
            return false;
        }

        var keywordStartIndex = index - ImportantKeyword.Length + 1;
        if (keywordStartIndex < 0 ||
            !value.AsSpan(keywordStartIndex, ImportantKeyword.Length)
                .Equals(ImportantKeyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        index = keywordStartIndex - 1;
        while (index >= 0 && char.IsWhiteSpace(value[index]))
        {
            index--;
        }

        if (index < 0 || value[index] != '!' || IsEscaped(value, index))
        {
            return false;
        }

        importantStartIndex = index;
        return true;
    }

    private static bool MoveBeforeTrailingWhitespaceAndComments(string value, ref int index)
    {
        while (index >= 0)
        {
            while (index >= 0 && char.IsWhiteSpace(value[index]))
            {
                index--;
            }

            if (index <= 0 || value[index] != '/' || value[index - 1] != '*')
            {
                return index >= 0;
            }

            var commentStart = value.LastIndexOf("/*", index - 2, StringComparison.Ordinal);
            if (commentStart < 0)
            {
                return false;
            }

            index = commentStart - 1;
        }

        return false;
    }

    private static bool IsEscaped(string value, int index)
    {
        var backslashCount = 0;
        for (var i = index - 1; i >= 0 && value[i] == '\\'; i--)
        {
            backslashCount++;
        }

        return backslashCount % 2 != 0;
    }
}
