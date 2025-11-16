// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using Microsoft.Language.Xml;

namespace Svg.Ast.Workspace;

/// <summary>
/// Tracks parsed SVG documents and coordinates incremental updates.
/// </summary>
public sealed class SvgAstWorkspace
{
    private readonly Dictionary<string, SvgAstWorkspaceDocument> _documents = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised whenever a document is added or updated.
    /// </summary>
    public event EventHandler<SvgAstWorkspaceDocumentChangedEventArgs>? DocumentChanged;

    /// <summary>
    /// Adds or replaces a document inside the workspace.
    /// </summary>
    public SvgAstWorkspaceDocument AddOrUpdateDocument(string id, SvgSourceText sourceText)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("Document id must be provided.", nameof(id));
        }

        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        var xmlDocument = sourceText.ParseXmlDocument();
        var document = SvgAstBuilder.Build(sourceText, xmlDocument);

        var workspaceDocument = new SvgAstWorkspaceDocument(id, sourceText, xmlDocument, document);
        _documents[id] = workspaceDocument;
        OnDocumentChanged(new SvgAstWorkspaceDocumentChangedEventArgs(id, workspaceDocument, previousDocument: null, isNewDocument: true, usedIncrementalParser: false));
        return workspaceDocument;
    }

    /// <summary>
    /// Gets a previously added document.
    /// </summary>
    public bool TryGetDocument(string id, out SvgAstWorkspaceDocument? document)
        => _documents.TryGetValue(id, out document);

    /// <summary>
    /// Applies edits to an existing document.
    /// </summary>
    public SvgAstWorkspaceUpdateResult ApplyChanges(string id, SvgSourceText newSourceText, TextChangeRange[]? changes = null)
    {
        if (!_documents.TryGetValue(id, out var existing))
        {
            throw new InvalidOperationException($"Document '{id}' is not tracked by the workspace.");
        }

        if (newSourceText is null)
        {
            throw new ArgumentNullException(nameof(newSourceText));
        }

        var previousDocument = existing.Document;
        XmlDocumentSyntax xmlDocument;
        var usedIncremental = changes is { Length: > 0 };

        if (usedIncremental)
        {
            xmlDocument = newSourceText.ParseXmlDocumentIncremental(existing.XmlDocument, changes!);
        }
        else
        {
            xmlDocument = newSourceText.ParseXmlDocument();
        }

        var document = SvgAstBuilder.Build(newSourceText, xmlDocument);
        existing.Update(newSourceText, xmlDocument, document);
        var result = new SvgAstWorkspaceUpdateResult(previousDocument, existing, usedIncremental);
        OnDocumentChanged(new SvgAstWorkspaceDocumentChangedEventArgs(id, existing, previousDocument, isNewDocument: false, usedIncrementalParser: usedIncremental));
        return result;
    }

    private void OnDocumentChanged(SvgAstWorkspaceDocumentChangedEventArgs args)
        => DocumentChanged?.Invoke(this, args);
}
