// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Microsoft.Language.Xml;

namespace Svg.Ast.Workspace;

/// <summary>
/// Represents a tracked document inside <see cref="SvgAstWorkspace"/>.
/// </summary>
public sealed class SvgAstWorkspaceDocument
{
    private SvgSymbolTable? _symbolTable;

    internal SvgAstWorkspaceDocument(string id, SvgSourceText sourceText, XmlDocumentSyntax xmlDocument, SvgAstDocument document)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        XmlDocument = xmlDocument ?? throw new ArgumentNullException(nameof(xmlDocument));
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets the unique identifier of the document.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the current source text.
    /// </summary>
    public SvgSourceText SourceText { get; private set; }

    /// <summary>
    /// Gets the last parsed XML document.
    /// </summary>
    public XmlDocumentSyntax XmlDocument { get; private set; }

    /// <summary>
    /// Gets the AST document produced from <see cref="SourceText"/>.
    /// </summary>
    public SvgAstDocument Document { get; private set; }

    /// <summary>
    /// Gets or builds the cached symbol table.
    /// </summary>
    public SvgSymbolTable GetOrCreateSymbolTable()
    {
        return _symbolTable ??= SvgSymbolTable.Build(Document);
    }

    /// <summary>
    /// Attempts to get the cached symbol table without building it.
    /// </summary>
    public bool TryGetCachedSymbolTable(out SvgSymbolTable? symbolTable)
    {
        symbolTable = _symbolTable;
        return symbolTable is not null;
    }

    internal void Update(SvgSourceText sourceText, XmlDocumentSyntax xmlDocument, SvgAstDocument document)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        XmlDocument = xmlDocument ?? throw new ArgumentNullException(nameof(xmlDocument));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _symbolTable = null;
    }
}
