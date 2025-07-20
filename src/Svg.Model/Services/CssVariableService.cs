using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Svg.Model.Services;

public static class CssVariableService
{
    private static readonly Regex s_varRegex = new(@"var\((--[^,\s]+)(?:,\s*([^)]+))?\)", RegexOptions.Compiled);

    public static string Preprocess(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
            return svg;
        var document = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        if (document.Root is not null)
        {
            ResolveElement(document.Root, new Dictionary<string, string>());
        }
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void ResolveElement(XElement element, Dictionary<string, string> vars)
    {
        var local = new Dictionary<string, string>(vars);
        var styleAttr = element.Attribute("style");
        if (styleAttr != null)
        {
            var styles = ParseStyle(styleAttr.Value);
            foreach (var kv in styles)
            {
                if (kv.Key.StartsWith("--", StringComparison.Ordinal))
                    local[kv.Key] = kv.Value;
            }
            var resolved = new List<string>();
            foreach (var kv in styles)
            {
                if (kv.Key.StartsWith("--", StringComparison.Ordinal))
                    continue;
                var val = ResolveValue(kv.Value, local);
                resolved.Add($"{kv.Key}:{val}");
            }
            if (resolved.Count > 0)
                styleAttr.Value = string.Join(";", resolved);
            else
                styleAttr.Remove();
        }
        foreach (var child in element.Elements())
        {
            ResolveElement(child, local);
        }
    }

    private static Dictionary<string, string> ParseStyle(string style)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in style.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf(':');
            if (idx > -1)
            {
                var key = part.Substring(0, idx).Trim();
                var val = part.Substring(idx + 1).Trim();
                result[key] = val;
            }
        }
        return result;
    }

    private static string ResolveValue(string value, Dictionary<string, string> vars)
    {
        return s_varRegex.Replace(value, m =>
        {
            var name = m.Groups[1].Value;
            if (vars.TryGetValue(name, out var val))
                return val;
            return m.Groups[2].Success ? m.Groups[2].Value : string.Empty;
        });
    }
}
