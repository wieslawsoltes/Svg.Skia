// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run --project tools/Svg.Metadata.Generator -- <path-to-svg11.dtd> <output-cs-path>");
    return;
}

var dtdPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(dtdPath))
{
    Console.Error.WriteLine($"DTD file not found: {dtdPath}");
    return;
}

var parser = new SvgDtdParser();
parser.Parse(await File.ReadAllTextAsync(dtdPath));

var generator = new MetadataGenerator(parser);
var generated = generator.Generate();
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, generated, Encoding.UTF8);
Console.WriteLine($"Generated metadata written to {outputPath}");

sealed class SvgDtdParser
{
    private readonly Dictionary<string, string> _entityDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _entityCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _elementAttributes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _attributeTypes = new(StringComparer.Ordinal);

    private static readonly Regex EntityRegex = new(@"<!ENTITY\s+%\s*(?<name>[\w\.\-]+)\s+(?<value>""[^""]*""|'[^']*')\s*>", RegexOptions.Singleline);
    private static readonly Regex EntityRefRegex = new(@"%([\w\.\-]+);", RegexOptions.Compiled);
    private static readonly Regex AttlistRegex = new(@"<!ATTLIST\s+(?<name>[^\s>]+)\s+(?<body>.*?)>", RegexOptions.Singleline);
    private static readonly Regex AttributeDefinitionRegex = new(
        @"(?<name>[A-Za-z_:][\w:\.-]*)\s+(?<type>\([^)]*\)|[^\s]+)\s+(?<rest>.*?)(?=(\s+[A-Za-z_:][\w:\.-]*\s+|\s*%|\s*$))",
        RegexOptions.Singleline);

    public IReadOnlyDictionary<string, HashSet<string>> ElementAttributes => _elementAttributes;
    public IReadOnlyDictionary<string, string> AttributeTypes => _attributeTypes;

    public void Parse(string text)
    {
        ParseEntities(text);
        ParseAttlists(text);
    }

    private void ParseEntities(string text)
    {
        foreach (Match match in EntityRegex.Matches(text))
        {
            var name = match.Groups["name"].Value;
            var rawValue = match.Groups["value"].Value;
            if (_entityDefinitions.ContainsKey(name))
            {
                continue;
            }

            var value = Unquote(rawValue);
            _entityDefinitions[name] = value;
        }
    }

    private static string Unquote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private void ParseAttlists(string text)
    {
        foreach (Match match in AttlistRegex.Matches(text))
        {
            var rawName = match.Groups["name"].Value.Trim();
            var resolvedName = ResolveName(rawName);
            if (string.IsNullOrEmpty(resolvedName))
            {
                continue;
            }

            var body = match.Groups["body"].Value;
            var expandedBody = Expand(body);

            foreach (Match attr in AttributeDefinitionRegex.Matches(expandedBody))
            {
                var attrName = attr.Groups["name"].Value;
                if (string.IsNullOrEmpty(attrName) || attrName.StartsWith("%", StringComparison.Ordinal))
                {
                    continue;
                }

                var typeToken = attr.Groups["type"].Value.Trim();
                if (string.IsNullOrEmpty(typeToken))
                {
                    typeToken = "CDATA";
                }

                if (!_elementAttributes.TryGetValue(resolvedName, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    _elementAttributes[resolvedName] = set;
                }

                set.Add(attrName);
                if (!_attributeTypes.ContainsKey(attrName))
                {
                    _attributeTypes[attrName] = typeToken;
                }
            }
        }
    }

    private string ResolveName(string token)
    {
        token = token.Trim();
        if (token.StartsWith("%", StringComparison.Ordinal) && token.EndsWith(";", StringComparison.Ordinal))
        {
            var name = token[1..^1];
            var expanded = ExpandEntity(name);
            return expanded.Trim();
        }

        return token;
    }

    private string Expand(string text)
    {
        return EntityRefRegex.Replace(
            text,
            match => ExpandEntity(match.Groups[1].Value));
    }

    private string ExpandEntity(string name)
    {
        if (_entityCache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (!_entityDefinitions.TryGetValue(name, out var value))
        {
            return string.Empty;
        }

        var expanded = EntityRefRegex.Replace(
            value,
            match =>
            {
                var innerName = match.Groups[1].Value;
                if (innerName == name)
                {
                    return string.Empty;
                }

                return ExpandEntity(innerName);
            });

        _entityCache[name] = expanded;
        return expanded;
    }
}

sealed class MetadataGenerator(SvgDtdParser parser)
{
    private static string Literal(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    public string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated from svg11.dtd. Do not edit manually.");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine();
        sb.AppendLine("namespace Svg.Ast.Metadata;");
        sb.AppendLine();
        sb.AppendLine("public static partial class SvgMetadata");
        sb.AppendLine("{");
        sb.AppendLine("    private static ImmutableDictionary<string, SvgAttributeMetadata> LoadGeneratedAttributeMetadata()");
        sb.AppendLine("    {");
        sb.AppendLine("        var builder = ImmutableDictionary.CreateBuilder<string, SvgAttributeMetadata>(StringComparer.Ordinal);");
        foreach (var attribute in parser.AttributeTypes.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"        builder[\"{attribute.Key}\"] = new SvgAttributeMetadata(\"{attribute.Key}\", \"{Literal(attribute.Value)}\", false, false);");
        }

        sb.AppendLine("        return builder.ToImmutable();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static ImmutableDictionary<string, SvgElementMetadata> LoadGeneratedElementMetadata()");
        sb.AppendLine("    {");
        sb.AppendLine("        var builder = ImmutableDictionary.CreateBuilder<string, SvgElementMetadata>(StringComparer.Ordinal);");

        foreach (var element in parser.ElementAttributes.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var attributes = element.Value.OrderBy(a => a, StringComparer.Ordinal).ToArray();
            var attrArray = string.Join(", ", attributes.Select(a => $"\"{Literal(a)}\""));
            sb.AppendLine($"        builder[\"{element.Key}\"] = new SvgElementMetadata(\"{element.Key}\", string.Empty, false, ImmutableArray<string>.Empty, ImmutableArray.Create({attrArray}));");
        }

        sb.AppendLine("        return builder.ToImmutable();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
