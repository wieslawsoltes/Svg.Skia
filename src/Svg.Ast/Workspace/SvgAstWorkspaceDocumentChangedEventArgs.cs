// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Immutable;

namespace Svg.Ast.Workspace;

/// <summary>
/// Provides data for <see cref="SvgAstWorkspace.DocumentChanged"/>.
/// </summary>
public sealed class SvgAstWorkspaceDocumentChangedEventArgs : EventArgs
{
    public SvgAstWorkspaceDocumentChangedEventArgs(
        string documentId,
        SvgAstWorkspaceDocument document,
        SvgAstDocument? previousDocument,
        bool isNewDocument,
        bool usedIncrementalParser)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        PreviousDocument = previousDocument;
        IsNewDocument = isNewDocument;
        UsedIncrementalParser = usedIncrementalParser;
        Diagnostics = document.Document.Diagnostics;
    }

    public string DocumentId { get; }

    public SvgAstWorkspaceDocument Document { get; }

    public SvgAstDocument? PreviousDocument { get; }

    public bool IsNewDocument { get; }

    public bool UsedIncrementalParser { get; }

    public ImmutableArray<SvgDiagnostic> Diagnostics { get; }
}
