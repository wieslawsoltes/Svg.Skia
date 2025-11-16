// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Language.Xml;
using System.Reflection;
using Svg.Ast.Metadata;

namespace Svg.Ast;

/// <summary>
/// Creates <see cref="SvgAstDocument"/> instances by traversing <see cref="XmlDocumentSyntax"/>.
/// </summary>
public sealed class SvgAstBuilder
{
    private readonly SvgSourceText _sourceText;
    private readonly XmlDocumentSyntax _xmlDocument;

    private readonly SvgNamespaceResolver _namespaceResolver = new();
    private readonly ImmutableArray<SvgDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<SvgDiagnostic>();
    private static readonly Func<DiagnosticInfo, TextSpan> s_diagnosticSpanAccessor = CreateDiagnosticSpanAccessor();

    private SvgAstBuilder(SvgSourceText sourceText, XmlDocumentSyntax xmlDocument)
    {
        _sourceText = sourceText;
        _xmlDocument = xmlDocument;
    }

    /// <summary>
    /// Builds an <see cref="SvgAstDocument"/> from the specified <see cref="SvgSourceText"/>.
    /// </summary>
    public static SvgAstDocument Build(SvgSourceText sourceText)
    {
        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        var xmlSyntax = sourceText.ParseXmlDocument();
        return Build(sourceText, xmlSyntax);
    }

    /// <summary>
    /// Builds an <see cref="SvgAstDocument"/> from an existing <see cref="XmlDocumentSyntax"/>.
    /// </summary>
    public static SvgAstDocument Build(SvgSourceText sourceText, XmlDocumentSyntax xmlDocument)
    {
        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        if (xmlDocument is null)
        {
            throw new ArgumentNullException(nameof(xmlDocument));
        }

        var builder = new SvgAstBuilder(sourceText, xmlDocument);
        return builder.BuildDocument();
    }

    /// <summary>
    /// Builds an <see cref="SvgAstDocument"/> using incremental parsing data.
    /// </summary>
    public static SvgAstDocument BuildIncremental(
        SvgSourceText sourceText,
        XmlDocumentSyntax previousDocument,
        TextChangeRange[] changes)
    {
        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        if (previousDocument is null)
        {
            throw new ArgumentNullException(nameof(previousDocument));
        }

        if (changes is null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        var xmlDocument = sourceText.ParseXmlDocumentIncremental(previousDocument, changes);
        return Build(sourceText, xmlDocument);
    }

    private SvgAstDocument BuildDocument()
    {
        CollectParserDiagnostics(_xmlDocument);

        SvgAstElement? rootElement = null;
        switch (_xmlDocument.Body)
        {
            case XmlElementSyntax elementSyntax:
                rootElement = BuildElement(elementSyntax, SvgXmlSpace.Default);
                break;
            case XmlEmptyElementSyntax emptyElement:
                rootElement = BuildEmptyElement(emptyElement, SvgXmlSpace.Default);
                break;
        }

        var prologNodes = ImmutableArray<SvgAstNode>.Empty;
        var epilogNodes = ImmutableArray<SvgAstNode>.Empty;

        if (_xmlDocument.PrecedingMisc.Count > 0)
        {
            prologNodes = ImmutableArray.CreateRange(TransformMisc(_xmlDocument.PrecedingMisc));
        }

        if (_xmlDocument.FollowingMisc.Count > 0)
        {
            epilogNodes = ImmutableArray.CreateRange(TransformMisc(_xmlDocument.FollowingMisc));
        }

        return new SvgAstDocument(_sourceText, rootElement, prologNodes, epilogNodes, _diagnostics.ToImmutable());
    }

    private IEnumerable<SvgAstNode> TransformMisc(SyntaxList<SyntaxNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XmlCommentSyntax comment)
            {
                yield return CreateComment(comment);
            }
            else if (node is XmlCDataSectionSyntax cdata)
            {
                yield return CreateCData(cdata);
            }
            else if (node is XmlProcessingInstructionSyntax pi)
            {
                yield return CreateProcessingInstruction(pi);
            }
            else if (node is XmlTextSyntax text)
            {
                yield return CreateText(text, SvgXmlSpace.Default);
            }
        }
    }

    private SvgAstElement BuildElement(XmlElementSyntax elementSyntax, SvgXmlSpace inheritedSpace)
    {
        var start = elementSyntax.Start;
        var length = elementSyntax.FullWidth;
        var xmlSpace = inheritedSpace;
        var startTag = elementSyntax.StartTag;
        var qualifiedName = CreateQualifiedName(startTag?.NameNode);
        var elementName = qualifiedName.LocalName;

        var attributes = ImmutableArray.CreateBuilder<SvgAstAttribute>();
        var attributeList = startTag?.AttributesNode ?? default;
        if (attributeList.Count > 0)
        {
            foreach (var attribute in attributeList)
            {
                if (attribute is XmlAttributeSyntax xmlAttribute)
                {
                    var svgAttribute = CreateAttribute(xmlAttribute, inheritedSpace, elementName);
                    attributes.Add(svgAttribute);

                    var isXmlSpaceAttribute =
                        string.Equals(svgAttribute.Name.NamespaceUri, SvgNamespaces.XmlNamespace, StringComparison.Ordinal) ||
                        string.Equals(svgAttribute.Name.Prefix, "xml", StringComparison.Ordinal) ||
                        string.Equals(svgAttribute.Name.ToString(), "xml:space", StringComparison.Ordinal);

                    if (isXmlSpaceAttribute &&
                        string.Equals(svgAttribute.Name.LocalName, "space", StringComparison.Ordinal))
                    {
                        xmlSpace = ParseXmlSpace(svgAttribute.GetValueText());
                    }
                }
            }
        }

        _namespaceResolver.PushScope();
        var children = ImmutableArray.CreateBuilder<SvgAstNode>();
        foreach (var child in elementSyntax.Content)
        {
            switch (child)
            {
                case XmlElementSyntax childElement:
                    children.Add(BuildElement(childElement, xmlSpace));
                    break;
                case XmlEmptyElementSyntax emptyElement:
                    children.Add(BuildEmptyElement(emptyElement, xmlSpace));
                    break;
                case XmlTextSyntax text:
                    children.Add(CreateText(text, xmlSpace));
                    break;
                case XmlCommentSyntax comment:
                    children.Add(CreateComment(comment));
                    break;
                case XmlCDataSectionSyntax cdata:
                    children.Add(CreateCData(cdata));
                    break;
                case XmlProcessingInstructionSyntax pi:
                    children.Add(CreateProcessingInstruction(pi));
                    break;
            }
        }

        var flags = SvgAstElementFlags.None;
        var result = new SvgAstElement(_sourceText, start, length, xmlSpace, qualifiedName, attributes.ToImmutable(), children.ToImmutable(), flags);
        _namespaceResolver.PopScope();
        return result;
    }

    private SvgAstElement BuildEmptyElement(XmlEmptyElementSyntax elementSyntax, SvgXmlSpace xmlSpace)
    {
        _namespaceResolver.PushScope();
        var start = elementSyntax.Start;
        var length = elementSyntax.FullWidth;
        var qualifiedName = CreateQualifiedName(elementSyntax.NameNode);
        var elementName = qualifiedName.LocalName;
        var attributes = ImmutableArray.CreateBuilder<SvgAstAttribute>();
        var attributeList = elementSyntax.AttributesNode;
        if (attributeList.Count > 0)
        {
            foreach (var attribute in attributeList)
            {
                if (attribute is XmlAttributeSyntax xmlAttribute)
                {
                    attributes.Add(CreateAttribute(xmlAttribute, xmlSpace, elementName));
                }
            }
        }

        var flags = SvgAstElementFlags.SelfClosing;
        var result = new SvgAstElement(_sourceText, start, length, xmlSpace, qualifiedName, attributes.ToImmutable(), ImmutableArray<SvgAstNode>.Empty, flags);
        _namespaceResolver.PopScope();
        return result;
    }

    private SvgAstAttribute CreateAttribute(XmlAttributeSyntax attributeSyntax, SvgXmlSpace xmlSpace, string ownerElementName)
    {
        var start = attributeSyntax.Start;
        var length = attributeSyntax.FullWidth;
        var valueSpan = attributeSyntax.ValueNode?.Span;
        int valueStart;
        int valueLength;
        if (valueSpan.HasValue && valueSpan.Value.Length > 0)
        {
            valueStart = valueSpan.Value.Start;
            valueLength = valueSpan.Value.Length;
        }
        else
        {
            valueStart = start;
            valueLength = 0;
        }
        var qualifiedName = CreateQualifiedName(attributeSyntax.NameNode);
        ValidateAttribute(ownerElementName, qualifiedName, attributeSyntax);
        DeclareNamespaceIfNeeded(attributeSyntax, qualifiedName);
        return new SvgAstAttribute(_sourceText, start, length, xmlSpace, qualifiedName, valueStart, valueLength);
    }

    private SvgAstText CreateText(XmlTextSyntax textSyntax, SvgXmlSpace xmlSpace)
    {
        var text = textSyntax.ToFullString();
        var isWhitespace = string.IsNullOrWhiteSpace(text);
        return new SvgAstText(_sourceText, textSyntax.Start, textSyntax.FullWidth, xmlSpace, isWhitespace);
    }

    private SvgAstComment CreateComment(XmlCommentSyntax commentSyntax)
    {
        return new SvgAstComment(_sourceText, commentSyntax.Start, commentSyntax.FullWidth, SvgXmlSpace.Default);
    }

    private SvgAstCData CreateCData(XmlCDataSectionSyntax cdataSyntax)
    {
        return new SvgAstCData(_sourceText, cdataSyntax.Start, cdataSyntax.FullWidth, SvgXmlSpace.Preserve);
    }

    private SvgAstProcessingInstruction CreateProcessingInstruction(XmlProcessingInstructionSyntax piSyntax)
    {
        var targetText = piSyntax.Name?.Text ?? string.Empty;
        var target = new SvgQualifiedName(null, string.Empty, targetText);
        var textTokens = piSyntax.TextTokens;
        int valueStart;
        int valueLength;
        if (textTokens.Count > 0)
        {
            var first = textTokens[0];
            var last = textTokens[textTokens.Count - 1];
            valueStart = first.Start;
            valueLength = last.End - first.Start;
        }
        else
        {
            valueStart = piSyntax.Name?.End ?? piSyntax.Start;
            valueLength = 0;
        }

        return new SvgAstProcessingInstruction(_sourceText, piSyntax.Start, piSyntax.FullWidth, SvgXmlSpace.Default, target, valueStart, valueLength);
    }

    private SvgQualifiedName CreateQualifiedName(XmlNameSyntax? nameSyntax)
    {
        var prefix = nameSyntax?.Prefix;
        var local = nameSyntax?.LocalName ?? string.Empty;
        if (!string.IsNullOrEmpty(prefix) && nameSyntax is { } named && !_namespaceResolver.TryResolve(prefix, out var resolvedNamespace))
        {
            ReportDiagnostic(
                "SVGASTNS001",
                SvgDiagnosticSeverity.Error,
                $"Namespace prefix '{prefix}' is not defined.",
                named.Span);
            return new SvgQualifiedName(prefix, resolvedNamespace, local);
        }

        _ = _namespaceResolver.TryResolve(prefix, out var namespaceUri);
        return new SvgQualifiedName(prefix, namespaceUri, local);
    }

    private SvgXmlSpace ParseXmlSpace(string value)
    {
        if (string.Equals(value, "preserve", StringComparison.OrdinalIgnoreCase))
        {
            return SvgXmlSpace.Preserve;
        }

        return SvgXmlSpace.Default;
    }

    private void ValidateAttribute(string elementName, SvgQualifiedName attributeName, XmlAttributeSyntax attributeSyntax)
    {
        if (string.IsNullOrEmpty(elementName))
        {
            return;
        }

        if (IsNamespaceDeclaration(attributeName))
        {
            return;
        }

        if (!SvgMetadata.TryGetElement(elementName, out var metadata))
        {
            return;
        }

        if (!metadata.AllowsAttribute(attributeName.LocalName))
        {
            var message = $"Attribute '{attributeName}' is not valid on '<{elementName}>' elements.";
            ReportDiagnostic("SVGASTATTR001", SvgDiagnosticSeverity.Warning, message, attributeSyntax.Span);
        }
    }

    private static bool IsNamespaceDeclaration(SvgQualifiedName attributeName)
    {
        if (string.Equals(attributeName.LocalName, "xmlns", StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(attributeName.Prefix) &&
            string.Equals(attributeName.Prefix, "xmlns", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private void CollectParserDiagnostics(SyntaxNode node)
    {
        if (node.ContainsDiagnostics)
        {
            var diagnostics = node.GetDiagnostics();
            if (diagnostics is { Length: > 0 })
            {
                foreach (var diagnostic in diagnostics)
                {
                    var span = s_diagnosticSpanAccessor(diagnostic);
                    if (span.Length == 0 && span.Start == 0)
                    {
                        span = node.Span;
                    }

                    ReportDiagnostic(
                        diagnostic.ErrorID.ToString(),
                        ConvertSeverity(diagnostic.Severity),
                        diagnostic.GetDescription(),
                        span);
                }
            }
        }

        foreach (var child in node.ChildNodes)
        {
            CollectParserDiagnostics(child);
        }

        if (node is SyntaxToken token)
        {
            foreach (var trivia in token.GetLeadingTrivia())
            {
                CollectParserDiagnostics(trivia);
            }

            foreach (var trivia in token.GetTrailingTrivia())
            {
                CollectParserDiagnostics(trivia);
            }
        }
    }

    private void ReportDiagnostic(string code, SvgDiagnosticSeverity severity, string message, TextSpan span)
    {
        _diagnostics.Add(new SvgDiagnostic(code, message, severity, span.Start, span.Length));
    }

    private static SvgDiagnosticSeverity ConvertSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => SvgDiagnosticSeverity.Info,
        DiagnosticSeverity.Warning => SvgDiagnosticSeverity.Warning,
        _ => SvgDiagnosticSeverity.Error
    };

    private void DeclareNamespaceIfNeeded(XmlAttributeSyntax attributeSyntax, SvgQualifiedName qualifiedName)
    {
        var isDefault = string.IsNullOrEmpty(qualifiedName.Prefix) && string.Equals(qualifiedName.LocalName, "xmlns", StringComparison.Ordinal);
        var isPrefixed = string.Equals(qualifiedName.Prefix, "xmlns", StringComparison.Ordinal);
        if (!isDefault && !isPrefixed)
        {
            return;
        }

        var prefix = isDefault ? string.Empty : qualifiedName.LocalName;
        var value = attributeSyntax.Value ?? string.Empty;
        _namespaceResolver.Declare(prefix, value);
    }

    private static Func<DiagnosticInfo, TextSpan> CreateDiagnosticSpanAccessor()
    {
        var diagnosticType = typeof(DiagnosticInfo);
        var spanProperty = diagnosticType.GetProperty("Span", BindingFlags.Public | BindingFlags.Instance);
        if (spanProperty is not null && spanProperty.PropertyType == typeof(TextSpan))
        {
            return diag => (TextSpan)(spanProperty.GetValue(diag) ?? default);
        }

        var getSpanMethod = diagnosticType.GetMethod("GetSpan", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (getSpanMethod is not null && getSpanMethod.ReturnType == typeof(TextSpan))
        {
            return diag => (TextSpan)(getSpanMethod.Invoke(diag, null) ?? default);
        }

        return _ => default;
    }
}
