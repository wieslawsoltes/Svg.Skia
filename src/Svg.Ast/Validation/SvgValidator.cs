// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Svg.Ast.Metadata;

namespace Svg.Ast.Validation;

/// <summary>
/// Provides basic validation helpers for svg AST documents.
/// </summary>
public static class SvgValidator
{
    private static readonly Regex s_lengthRegex = new(@"^[+-]?(\d+(\.\d+)?|\.\d+)(px|pt|pc|mm|cm|in|em|ex|%)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_urlReferenceRegex = new(@"^url\(#(?<id>[^)]+)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_simpleReferenceRegex = new(@"^#(?<id>.+)$", RegexOptions.Compiled);
    private static readonly HashSet<string> s_referenceAttributes = new(StringComparer.Ordinal)
    {
        "href",
        "xlink:href",
        "clip-path",
        "mask",
        "filter",
        "fill",
        "stroke",
        "marker-start",
        "marker-mid",
        "marker-end",
        "color-profile",
        "cursor"
    };
    private static readonly HashSet<string> s_nonNegativeLengthAttributes = new(StringComparer.Ordinal)
    {
        "width",
        "height",
        "rx",
        "ry",
        "r"
    };

    /// <summary>
    /// Validates a document and returns collected diagnostics.
    /// </summary>
    public static ImmutableArray<SvgDiagnostic> Validate(SvgAstDocument document)
    {
        var builder = ImmutableArray.CreateBuilder<SvgDiagnostic>();
        var idMap = new Dictionary<string, SvgAstElement>(StringComparer.Ordinal);
        CollectIds(document.RootElement, idMap, builder);
        ValidateElement(document.RootElement, idMap, builder);
        return builder.ToImmutable();
    }

    private static void CollectIds(SvgAstElement? element, Dictionary<string, SvgAstElement> idMap, ImmutableArray<SvgDiagnostic>.Builder builder)
    {
        if (element is null)
        {
            return;
        }

        if (element.TryGetAttribute("id", out var idAttribute))
        {
            var idValue = idAttribute.GetValueText();
            if (!string.IsNullOrEmpty(idValue))
            {
                if (idMap.ContainsKey(idValue))
                {
                    builder.Add(new SvgDiagnostic(
                        "SVGASTID001",
                        $"Duplicate id '{idValue}' detected.",
                        SvgDiagnosticSeverity.Error,
                        idAttribute.Start,
                        idAttribute.Length));
                }
                else
                {
                    idMap[idValue] = element;
                }
            }
        }

        foreach (var child in element.Children)
        {
            if (child is SvgAstElement childElement)
            {
                CollectIds(childElement, idMap, builder);
            }
        }
    }

    private static void ValidateElement(SvgAstElement? element, Dictionary<string, SvgAstElement> idMap, ImmutableArray<SvgDiagnostic>.Builder builder)
    {
        if (element is null)
        {
            return;
        }

        if (SvgMetadata.TryGetElement(element.Name.LocalName, out var metadata))
        {
            foreach (var attribute in element.Attributes)
            {
                ValidateAttributeValue(element, attribute, metadata, builder);
                ValidateReferenceAttribute(element, attribute, idMap, builder);
            }
        }

        foreach (var child in element.Children)
        {
            if (child is SvgAstElement childElement)
            {
                ValidateElement(childElement, idMap, builder);
            }
        }
    }

    private static void ValidateAttributeValue(SvgAstElement owner, SvgAstAttribute attribute, SvgElementMetadata metadata, ImmutableArray<SvgDiagnostic>.Builder builder)
    {
        if (!SvgMetadata.TryGetAttribute(attribute.Name.LocalName, out var attrMetadata))
        {
            return;
        }

        var value = attribute.GetValueText();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (attrMetadata.DataType == "Length" && !s_lengthRegex.IsMatch(value))
        {
            builder.Add(new SvgDiagnostic(
                "SVGASTVAL001",
                $"Attribute '{attribute.Name}' on '<{owner.Name.LocalName}>' is not a valid length.",
                SvgDiagnosticSeverity.Warning,
                attribute.Start,
                attribute.Length));
            return;
        }

        if (attrMetadata.DataType == "Length" &&
            s_nonNegativeLengthAttributes.Contains(attribute.Name.LocalName) &&
            TryParseNumericValue(value, out var numericValue) &&
            numericValue < 0)
        {
            builder.Add(new SvgDiagnostic(
                "SVGASTVAL002",
                $"Attribute '{attribute.Name}' on '<{owner.Name.LocalName}>' must be non-negative.",
                SvgDiagnosticSeverity.Error,
                attribute.Start,
                attribute.Length));
        }
    }

    private static void ValidateReferenceAttribute(SvgAstElement owner, SvgAstAttribute attribute, Dictionary<string, SvgAstElement> idMap, ImmutableArray<SvgDiagnostic>.Builder builder)
    {
        if (!s_referenceAttributes.Contains(attribute.Name.LocalName))
        {
            return;
        }

        var referencedId = ExtractReferenceId(attribute.GetValueText());
        if (referencedId is null)
        {
            return;
        }

        if (!idMap.ContainsKey(referencedId))
        {
            builder.Add(new SvgDiagnostic(
                "SVGASTREF001",
                $"Attribute '{attribute.Name}' on '<{owner.Name.LocalName}>' references undefined id '{referencedId}'.",
                SvgDiagnosticSeverity.Error,
                attribute.Start,
                attribute.Length));
        }
        else if (owner.TryGetAttribute("id", out var idAttribute) && string.Equals(idAttribute.GetValueText(), referencedId, StringComparison.Ordinal))
        {
            builder.Add(new SvgDiagnostic(
                "SVGASTREF002",
                $"Attribute '{attribute.Name}' on '<{owner.Name.LocalName}>' references itself.",
                SvgDiagnosticSeverity.Warning,
                attribute.Start,
                attribute.Length));
        }
    }

    private static string? ExtractReferenceId(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var match = s_urlReferenceRegex.Match(value);
        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        match = s_simpleReferenceRegex.Match(value);
        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        return null;
    }

    private static bool TryParseNumericValue(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        var end = span.Length;
        while (end > 0 && (char.IsLetter(span[end - 1]) || span[end - 1] == '%'))
        {
            end--;
        }

        if (end <= 0)
        {
            return false;
        }

        var numericSpan = span.Slice(0, end);
#if NETSTANDARD2_0
        var numericText = numericSpan.ToString();
        return double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
        return double.TryParse(numericSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
    }
}
