#nullable enable
using System;
using System.Collections.Generic;
using ExCSS;

namespace Svg;

/// <summary>
/// Checks input-controlled CSS recursion before handing text or selectors to ExCSS.
/// This is a resource limit, not a replacement CSS parser: malformed input still goes
/// through the existing error-recovery rules after it has passed these checks.
/// </summary>
internal static class SvgCssInputGuard
{
    internal static void ValidateStylesheet(string css)
    {
        ValidateNesting(css, validateRuleSelectors: true);
    }

    internal static void ValidateDeclarationList(string css)
    {
        ValidateNesting(css, validateRuleSelectors: false);
    }

    private static void ValidateNesting(string css, bool validateRuleSelectors)
    {
        if (string.IsNullOrEmpty(css))
        {
            return;
        }

        // The stack is deliberately fixed-size. Mismatched closers do not pop an
        // unrelated opener, which prevents malformed CSS from hiding deeper nesting.
        Span<char> delimiters = stackalloc char[SvgProcessingLimits.MaximumCssDepth];
        Span<bool> ruleLists = stackalloc bool[SvgProcessingLimits.MaximumCssDepth + 1];
        Span<int> statementStarts = stackalloc int[SvgProcessingLimits.MaximumCssDepth + 1];
        ruleLists[0] = validateRuleSelectors;
        statementStarts[0] = 0;
        var depth = 0;

        for (var index = 0; index < css.Length; index++)
        {
            var character = css[index];
            if (TrySkipCommentOrString(css, ref index))
            {
                continue;
            }

            if (character == '\\')
            {
                SkipEscape(css, ref index);
                continue;
            }

            if (character is '(' or '[' or '{')
            {
                SvgProcessingLimits.CheckDepth(depth + 1, SvgProcessingLimits.MaximumCssDepth, "CSS nesting");
                var isRuleList = ruleLists[depth];
                var statementStart = statementStarts[depth];
                if (character == '{' && isRuleList)
                {
                    ValidateRulePrelude(css, statementStart, index);
                }

                delimiters[depth++] = character;
                // A style rule contains declarations; grouping at-rules contain rules.
                // Parentheses/brackets are component values, never new selector lists.
                ruleLists[depth] = character == '{' && isRuleList &&
                                   IsGroupingRule(css, statementStart, index);
                statementStarts[depth] = index + 1;
                continue;
            }

            if (character is ')' or ']' or '}')
            {
                if (depth > 0 && Matches(delimiters[depth - 1], character))
                {
                    depth--;
                    if (character == '}')
                    {
                        statementStarts[depth] = index + 1;
                    }
                }

                continue;
            }

            if (character == ';')
            {
                if (ruleLists[depth])
                {
                    ValidateRulePrelude(css, statementStarts[depth], index);
                }

                statementStarts[depth] = index + 1;
            }
        }

        // ExCSS also constructs selectors while recovering an unterminated rule.
        if (ruleLists[depth])
        {
            ValidateRulePrelude(css, statementStarts[depth], css.Length);
        }
    }

    private static bool Matches(char opener, char closer)
    {
        return opener == '(' && closer == ')' ||
               opener == '[' && closer == ']' ||
               opener == '{' && closer == '}';
    }

    private static void ValidateRulePrelude(string css, int start, int end)
    {
        start = SkipWhitespaceAndComments(css, start, end);
        if (start < end && css[start] != '@')
        {
            ValidateSelector(css, start, end);
        }
    }

    private static bool IsGroupingRule(string css, int start, int end)
    {
        start = SkipWhitespaceAndComments(css, start, end);
        if (start >= end || css[start] != '@')
        {
            return false;
        }

        // Unknown at-rules are treated as grouping rules for the safety check. The
        // parser decides whether they are supported; a nested selector cannot evade
        // the check by using an unfamiliar rule name.
        var nameStart = ++start;
        while (start < end && IsNameCharacter(css[start]))
        {
            start++;
        }

        var name = css.AsSpan(nameStart, start - nameStart);
        return !name.Equals("font-face".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("page".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("counter-style".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("property".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    internal static void ValidateSelector(string selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return;
        }

        ValidateNesting(selector, validateRuleSelectors: false);
        ValidateSelector(selector, 0, selector.Length);
    }

    private static void ValidateSelector(string selector, int start, int end)
    {
        var components = 0;
        var parentheses = 0;
        var brackets = 0;
        for (var index = start; index < end; index++)
        {
            var character = selector[index];
            if (TrySkipCommentOrString(selector, ref index))
            {
                continue;
            }

            if (character == '[')
            {
                if (brackets++ == 0)
                {
                    CheckSelectorComponents(++components);
                }

                continue;
            }

            if (character == ']')
            {
                if (brackets > 0)
                {
                    brackets--;
                }

                continue;
            }

            if (brackets > 0)
            {
                if (character == '\\')
                {
                    SkipEscape(selector, ref index);
                }

                continue;
            }

            if (character == '(')
            {
                parentheses++;
            }
            else if (character == ')')
            {
                if (parentheses > 0)
                {
                    parentheses--;
                }
            }
            else if (character == ',' && parentheses == 0)
            {
                // Independent selector-list branches are evaluated independently;
                // their count does not increase the depth of an individual match.
                components = 0;
            }
            else if (character is '*' or '&' or '>' or '+' or '~' or '|')
            {
                CheckSelectorComponents(++components);
            }
            else if (IsNameCharacter(character) || character == '\\')
            {
                CheckSelectorComponents(++components);
                while (index < end)
                {
                    if (selector[index] == '\\')
                    {
                        SkipEscape(selector, ref index);
                    }
                    else if (!IsNameCharacter(selector[index]))
                    {
                        break;
                    }

                    index++;
                }

                index--;
            }
        }
    }

    internal static void ValidateSelector(ISelector selector)
    {
        // Do not read Text or Specificity until the selector tree is checked: both
        // properties can themselves recurse inside ExCSS.
        var branches = new Stack<(ISelector Selector, int Depth)>();
        var pending = new Stack<(ISelector Selector, int Depth)>();
        branches.Push((selector, 1));
        while (branches.Count > 0)
        {
            pending.Push(branches.Pop());
            var components = 0;
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                SvgProcessingLimits.CheckDepth(current.Depth, SvgProcessingLimits.MaximumCssDepth, "CSS selector nesting");
                CheckSelectorComponents(++components);
                switch (current.Selector)
                {
                    case ListSelector list:
                        foreach (var branch in list)
                        {
                            branches.Push((branch, current.Depth + 1));
                        }

                        break;
                    case CompoundSelector compound:
                        foreach (var part in compound)
                        {
                            pending.Push((part, current.Depth + 1));
                        }

                        break;
                    case ComplexSelector complex:
                        foreach (var part in complex)
                        {
                            pending.Push((part.Selector, current.Depth + 1));
                        }

                        break;
                    case PseudoClassSelector pseudo:
                        ValidateSelector(pseudo.Class);
                        break;
                }
            }
        }
    }

    private static void CheckSelectorComponents(int count)
    {
        SvgProcessingLimits.CheckDepth(count, SvgProcessingLimits.MaximumCssSelectorComplexity, "CSS selector complexity");
    }

    private static bool IsNameCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character is '-' or '_' || character >= '\u0080';
    }

    private static bool TrySkipCommentOrString(string text, ref int index)
    {
        var character = text[index];
        if (character == '/' && index + 1 < text.Length && text[index + 1] == '*')
        {
            var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
            index = end < 0 ? text.Length : end + 1;
            return true;
        }

        if (character is not ('\'' or '"'))
        {
            return false;
        }

        var quote = character;
        while (++index < text.Length)
        {
            character = text[index];
            if (character == '\\')
            {
                SkipEscape(text, ref index);
            }
            else if (character == quote || character is '\r' or '\n' or '\f')
            {
                // An unescaped newline ends a CSS bad-string token. Treating the
                // remainder as quoted would let malformed text conceal nesting.
                break;
            }
        }

        return true;
    }

    private static int SkipWhitespaceAndComments(string text, int start, int end)
    {
        while (start < end)
        {
            if (char.IsWhiteSpace(text[start]))
            {
                start++;
            }
            else if (text[start] == '/' && start + 1 < end && text[start + 1] == '*')
            {
                var commentEnd = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
                if (commentEnd < 0 || commentEnd >= end)
                {
                    return end;
                }

                start = commentEnd + 2;
            }
            else
            {
                break;
            }
        }

        return start;
    }

    private static void SkipEscape(string text, ref int index)
    {
        if (++index >= text.Length)
        {
            return;
        }

        var hexDigits = 0;
        while (index < text.Length && hexDigits < 6 && IsHexDigit(text[index]))
        {
            index++;
            hexDigits++;
        }

        if (hexDigits == 0)
        {
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            return;
        }

        if (index < text.Length && text[index] is ' ' or '\t' or '\r' or '\n' or '\f')
        {
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }
        }
        else
        {
            index--;
        }
    }

    private static bool IsHexDigit(char character)
    {
        return character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
