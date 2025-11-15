// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Immutable;

namespace Svg.Ast.Emit;

/// <summary>
/// Options controlling how the emission pipeline traverses the AST.
/// </summary>
public sealed class SvgAstEmissionOptions
{
    /// <summary>
    /// Gets the default set of emission options.
    /// </summary>
    public static SvgAstEmissionOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether a <see cref="SvgSymbolTable"/> should be created.
    /// </summary>
    public bool BuildSymbolTable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether miscellaneous nodes preceding the root element should be visited.
    /// </summary>
    public bool VisitPrologNodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether miscellaneous nodes that follow the root element should be visited.
    /// </summary>
    public bool VisitEpilogNodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether text nodes should be visited.
    /// </summary>
    public bool IncludeTextNodes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether whitespace-only text nodes should be visited.
    /// </summary>
    public bool IncludeWhitespaceTextNodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether comment nodes should be included in the traversal.
    /// </summary>
    public bool IncludeComments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CDATA sections should be included in the traversal.
    /// </summary>
    public bool IncludeCDataSections { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether processing instructions should be included in the traversal.
    /// </summary>
    public bool IncludeProcessingInstructions { get; set; }

    /// <summary>
    /// Gets optional user-provided items that will flow into the emission context.
    /// </summary>
    public IImmutableDictionary<string, object?> Items { get; set; } = ImmutableDictionary<string, object?>.Empty;

    internal bool ShouldVisit(SvgAstNode node)
    {
        return node switch
        {
            SvgAstText text => IncludeTextNodes && (IncludeWhitespaceTextNodes || !text.IsWhitespace),
            SvgAstComment => IncludeComments,
            SvgAstCData => IncludeCDataSections,
            SvgAstProcessingInstruction => IncludeProcessingInstructions,
            _ => true
        };
    }
}
