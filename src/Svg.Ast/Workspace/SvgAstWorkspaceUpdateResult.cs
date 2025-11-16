// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Svg.Ast.Workspace;

using System.Collections.Immutable;

/// <summary>
/// Represents the outcome of applying edits to a workspace document.
/// </summary>
public sealed class SvgAstWorkspaceUpdateResult
{
    internal SvgAstWorkspaceUpdateResult(SvgAstDocument previousDocument, SvgAstWorkspaceDocument currentDocument, bool usedIncremental)
    {
        PreviousDocument = previousDocument;
        Document = currentDocument;
        UsedIncrementalParser = usedIncremental;
    }

    /// <summary>
    /// Gets the document prior to applying the edits.
    /// </summary>
    public SvgAstDocument PreviousDocument { get; }

    /// <summary>
    /// Gets the updated workspace document.
    /// </summary>
    public SvgAstWorkspaceDocument Document { get; }

    /// <summary>
    /// Gets a value indicating whether incremental parsing was used.
    /// </summary>
    public bool UsedIncrementalParser { get; }

    /// <summary>
    /// Gets the diagnostics emitted by the updated document.
    /// </summary>
    public ImmutableArray<SvgDiagnostic> Diagnostics => Document.Document.Diagnostics;
}
