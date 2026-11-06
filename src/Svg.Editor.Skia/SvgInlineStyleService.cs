using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Svg.Editor.Skia;

/// <summary>Edits one declaration without splitting quoted strings, URLs or nested functions.</summary>
public static class SvgInlineStyleService
{
    public static string SetProperty(string? style, string property, string? value, bool important = false)
    {
        if (string.IsNullOrWhiteSpace(property) || property.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("A CSS property name is required.", nameof(property));
        var declarations = Split(style ?? string.Empty);
        declarations.RemoveAll(declaration =>
        {
            var colon = declaration.IndexOf(':');
            if (colon < 0) return false;
            var name = Regex.Replace(declaration.Substring(0, colon), @"/\*.*?\*/", "", RegexOptions.Singleline).Trim();
            return string.Equals(name, property, StringComparison.OrdinalIgnoreCase);
        });
        if (!string.IsNullOrWhiteSpace(value))
            declarations.Add(property + ":" + value + (important ? " !important" : string.Empty));
        return string.Join(";", declarations.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static List<string> Split(string text)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        var comment = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (comment)
            {
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '/') { comment = false; i++; }
                continue;
            }
            if (c == '\\') { i++; continue; }
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*') { comment = true; i++; continue; }
            if (c is '\'' or '"') { quote = c; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth = Math.Max(0, depth - 1);
            else if (c == ';' && depth == 0) { parts.Add(text.Substring(start, i - start)); start = i + 1; }
        }
        parts.Add(text.Substring(start));
        return parts;
    }
}
