using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

/// <summary>Copies authoring resources while reserving IDs against the destination snapshot.</summary>
internal static class SvgXmlIdRemapper
{
    public static XElement Clone(XElement source, IEnumerable<string> reservedIds)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (reservedIds is null) throw new ArgumentNullException(nameof(reservedIds));
        var copy = new XElement(source);
        var used = new HashSet<string>(reservedIds, StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in copy.DescendantsAndSelf())
        {
            if (element.Attribute("id") is not { } id || id.Value.Length == 0) continue;
            var original = id.Value;
            if (mapping.ContainsKey(original)) throw new InvalidOperationException("The copied resource contains duplicate XML IDs.");
            var prefix = original + "-copy";
            var candidate = prefix;
            for (var suffix = 2; !used.Add(candidate); suffix++)
                candidate = prefix + suffix.ToString(CultureInfo.InvariantCulture);
            mapping.Add(original, candidate);
            id.Value = candidate;
        }
        if (mapping.Count == 0) return copy;
        var alternatives = string.Join("|", mapping.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape));
        var options = RegexOptions.CultureInvariant;
        var timeout = TimeSpan.FromSeconds(1);
        string Urls(string value) => Regex.Replace(value, @"(url\(\s*['"" ]?#)([^\s'""()]+)(['"" ]?\s*\))",
            match => mapping.TryGetValue(match.Groups[2].Value, out var replacement)
                ? match.Groups[1].Value + replacement + match.Groups[3].Value : match.Value, options, timeout);
        foreach (var element in copy.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.LocalName != "id"))
            {
                var value = attribute.Value;
                if (attribute.Name.LocalName == "href" && value.StartsWith("#", StringComparison.Ordinal)
                    && mapping.TryGetValue(value.Substring(1), out var target)) value = "#" + target;
                if (attribute.Name.LocalName is "begin" or "end")
                    value = Regex.Replace(value, @"(^|[;\s])(" + alternatives + @")(?=\.)",
                        match => match.Groups[1].Value + mapping[match.Groups[2].Value], options, timeout);
                attribute.Value = Urls(value);
            }
            if (element.Name == SvgFilterCatalog.Namespace + "style")
            {
                var value = Urls(element.Value);
                element.Value = Regex.Replace(value, @"([^{}]+)\{", rule =>
                    Regex.Replace(rule.Groups[1].Value, @"#([\w-]+)", selector =>
                        mapping.TryGetValue(selector.Groups[1].Value, out var id) ? "#" + id : selector.Value,
                        options, timeout) + "{", options, timeout);
            }
        }
        return copy;
    }
}
