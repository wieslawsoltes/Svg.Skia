// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Svg.Ast.Metadata;

namespace Svg.Ast;

/// <summary>
/// Represents xml:space handling for AST nodes.
/// </summary>
public enum SvgXmlSpace
{
    Default,
    Preserve
}

/// <summary>
/// Flags describing additional attributes characteristics.
/// </summary>
[Flags]
public enum SvgAstAttributeFlags
{
    None = 0,
    Presentation = 1 << 0,
    Animatable = 1 << 1,
    CssStyle = 1 << 2
}

/// <summary>
/// Flags describing element characteristics.
/// </summary>
[Flags]
public enum SvgAstElementFlags
{
    None = 0,
    SelfClosing = 1 << 0,
    HasPresentationAttributes = 1 << 1,
    HasStyleAttribute = 1 << 2,
    HasId = 1 << 3
}

/// <summary>
/// Represents a qualified SVG name.
/// </summary>
public readonly struct SvgQualifiedName : IEquatable<SvgQualifiedName>
{
    public SvgQualifiedName(string? prefix, string namespaceUri, string localName)
    {
        Prefix = prefix;
        NamespaceUri = namespaceUri ?? string.Empty;
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
    }

    public string? Prefix { get; }

    public string NamespaceUri { get; }

    public string LocalName { get; }

    public override string ToString() =>
        string.IsNullOrEmpty(Prefix) ? LocalName : $"{Prefix}:{LocalName}";

    public bool Equals(SvgQualifiedName other) =>
        string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
        string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SvgQualifiedName other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 23) + (Prefix is null ? 0 : StringComparer.Ordinal.GetHashCode(Prefix));
            hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(NamespaceUri);
            hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(LocalName);
            return hash;
        }
    }
}

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract class SvgAstNode
{
    protected SvgAstNode(SvgSourceText sourceText, int start, int length, SvgXmlSpace xmlSpace)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        Start = start;
        Length = length;
        XmlSpace = xmlSpace;
    }

    public SvgSourceText SourceText { get; }

    public int Start { get; }

    public int Length { get; }

    public SvgLinePosition StartPosition => SourceText.GetLinePosition(Start);

    public SvgLinePosition EndPosition => SourceText.GetLinePosition(Start + Math.Max(Length - 1, 0));

    public SvgXmlSpace XmlSpace { get; }

    public ReadOnlySpan<char> AsSpan() => SourceText.Slice(Start, Length);

    public override string ToString() => SourceText.SliceToString(Start, Length);
}

/// <summary>
/// Represents the root SVG document node.
/// </summary>
public sealed class SvgAstDocument
{
    public SvgAstDocument(
        SvgSourceText sourceText,
        SvgAstElement? rootElement,
        IReadOnlyList<SvgAstNode>? prologNodes = null,
        IReadOnlyList<SvgAstNode>? epilogNodes = null,
        IReadOnlyList<SvgDiagnostic>? diagnostics = null)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        RootElement = rootElement;
        PrologNodes = prologNodes switch
        {
            null => ImmutableArray<SvgAstNode>.Empty,
            ImmutableArray<SvgAstNode> immutable => immutable,
            _ => ImmutableArray.CreateRange(prologNodes)
        };
        EpilogNodes = epilogNodes switch
        {
            null => ImmutableArray<SvgAstNode>.Empty,
            ImmutableArray<SvgAstNode> immutable => immutable,
            _ => ImmutableArray.CreateRange(epilogNodes)
        };
        Diagnostics = diagnostics switch
        {
            null => ImmutableArray<SvgDiagnostic>.Empty,
            ImmutableArray<SvgDiagnostic> immutable => immutable,
            _ => ImmutableArray.CreateRange(diagnostics)
        };
    }

    public SvgSourceText SourceText { get; }

    public SvgAstElement? RootElement { get; }

    public ImmutableArray<SvgAstNode> PrologNodes { get; }

    public ImmutableArray<SvgAstNode> EpilogNodes { get; }

    public ImmutableArray<SvgDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Represents an SVG element.
/// </summary>
public sealed class SvgAstElement : SvgAstNode
{
    public SvgAstElement(
        SvgSourceText sourceText,
        int start,
        int length,
        SvgXmlSpace xmlSpace,
        SvgQualifiedName name,
        IReadOnlyList<SvgAstAttribute>? attributes = null,
        IReadOnlyList<SvgAstNode>? children = null,
        SvgAstElementFlags flags = SvgAstElementFlags.None)
        : base(sourceText, start, length, xmlSpace)
    {
        Name = name;
        Attributes = attributes switch
        {
            null => ImmutableArray<SvgAstAttribute>.Empty,
            ImmutableArray<SvgAstAttribute> immutable => immutable,
            _ => ImmutableArray.CreateRange(attributes)
        };
        Children = children switch
        {
            null => ImmutableArray<SvgAstNode>.Empty,
            ImmutableArray<SvgAstNode> immutable => immutable,
            _ => ImmutableArray.CreateRange(children)
        };
        Flags = flags;
    }

    public SvgQualifiedName Name { get; }

    public ImmutableArray<SvgAstAttribute> Attributes { get; }

    public ImmutableArray<SvgAstNode> Children { get; }

    public SvgAstElementFlags Flags { get; }

    public bool TryGetAttribute(string name, out SvgAstAttribute attribute)
    {
        if (string.IsNullOrEmpty(name))
        {
            attribute = null!;
            return false;
        }

        foreach (var attr in Attributes)
        {
            if (string.Equals(attr.Name.LocalName, name, StringComparison.Ordinal))
            {
                attribute = attr;
                return true;
            }
        }

        attribute = null!;
        return false;
    }

    public IEnumerable<string> EnumeratePresentationAttributes()
    {
        var localName = Name.LocalName;
        if (string.IsNullOrEmpty(localName))
        {
            yield break;
        }

        if (!SvgMetadata.TryGetElement(localName, out var metadata))
        {
            yield break;
        }

        foreach (var attrName in metadata.PresentationAttributes)
        {
            if (TryGetAttribute(attrName, out var attribute))
            {
                yield return attribute.GetValueText();
            }
        }
    }
}

/// <summary>
/// Represents an SVG attribute.
/// </summary>
public sealed class SvgAstAttribute : SvgAstNode
{
    public SvgAstAttribute(
        SvgSourceText sourceText,
        int start,
        int length,
        SvgXmlSpace xmlSpace,
        SvgQualifiedName name,
        int valueStart,
        int valueLength,
        SvgAstAttributeFlags flags = SvgAstAttributeFlags.None)
        : base(sourceText, start, length, xmlSpace)
    {
        Name = name;
        ValueStart = valueStart;
        ValueLength = valueLength;
        Flags = flags;
    }

    public SvgQualifiedName Name { get; }

    public int ValueStart { get; }

    public int ValueLength { get; }

    public SvgAstAttributeFlags Flags { get; }

    public ReadOnlySpan<char> GetValueSpan() => SourceText.Slice(ValueStart, ValueLength);

    public string GetValueText()
    {
        var text = SourceText.SliceToString(ValueStart, ValueLength);
        if (text.Length >= 2)
        {
            var first = text[0];
            var last = text[text.Length - 1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return text.Substring(1, text.Length - 2);
            }
        }

        return text;
    }

    public bool IsLength(out double value)
    {
        if (SvgMetadata.TryGetAttribute(Name.LocalName, out var metadata) &&
            string.Equals(metadata.DataType, "Length", StringComparison.Ordinal))
        {
            if (double.TryParse(GetValueText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }
}

/// <summary>
/// Represents a text node.
/// </summary>
public sealed class SvgAstText : SvgAstNode
{
    public SvgAstText(SvgSourceText sourceText, int start, int length, SvgXmlSpace xmlSpace, bool isWhitespace)
        : base(sourceText, start, length, xmlSpace)
    {
        IsWhitespace = isWhitespace;
    }

    public bool IsWhitespace { get; }
}

/// <summary>
/// Represents an XML comment.
/// </summary>
public sealed class SvgAstComment : SvgAstNode
{
    public SvgAstComment(SvgSourceText sourceText, int start, int length, SvgXmlSpace xmlSpace)
        : base(sourceText, start, length, xmlSpace)
    {
    }
}

/// <summary>
/// Represents a CDATA section.
/// </summary>
public sealed class SvgAstCData : SvgAstNode
{
    public SvgAstCData(SvgSourceText sourceText, int start, int length, SvgXmlSpace xmlSpace)
        : base(sourceText, start, length, xmlSpace)
    {
    }
}

/// <summary>
/// Represents an XML processing instruction.
/// </summary>
public sealed class SvgAstProcessingInstruction : SvgAstNode
{
    public SvgAstProcessingInstruction(
        SvgSourceText sourceText,
        int start,
        int length,
        SvgXmlSpace xmlSpace,
        SvgQualifiedName target,
        int valueStart,
        int valueLength)
        : base(sourceText, start, length, xmlSpace)
    {
        Target = target;
        ValueStart = valueStart;
        ValueLength = valueLength;
    }

    public SvgQualifiedName Target { get; }

    public int ValueStart { get; }

    public int ValueLength { get; }

    public ReadOnlySpan<char> GetValueSpan() => SourceText.Slice(ValueStart, ValueLength);

    public string GetValueText() => SourceText.SliceToString(ValueStart, ValueLength);
}
