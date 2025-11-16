// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace Svg.Ast.Emit;

/// <summary>
/// Helper base class for emission visitors that walk the AST.
/// </summary>
public abstract class SvgAstVisitorEmitter<TResult> : SvgAstNodeVisitor<SvgAstEmissionContext>, ISvgAstEmitter<TResult>
{
    /// <inheritdoc />
    public TResult Emit(SvgAstEmissionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ThrowIfCancellationRequested();
        VisitDocument(context.Document, context);
        context.ThrowIfCancellationRequested();
        return GetResult(context);
    }

    /// <inheritdoc />
    public override void VisitDocument(SvgAstDocument document, SvgAstEmissionContext context)
    {
        if (context.Options.VisitPrologNodes)
        {
            VisitMiscNodes(document.PrologNodes, context);
        }

        base.VisitDocument(document, context);

        if (context.Options.VisitEpilogNodes)
        {
            VisitMiscNodes(document.EpilogNodes, context);
        }
    }

    /// <inheritdoc />
    public override void VisitNode(SvgAstNode node, SvgAstEmissionContext context)
    {
        if (!context.Options.ShouldVisit(node))
        {
            return;
        }

        base.VisitNode(node, context);
    }

    /// <summary>
    /// Visits miscellaneous nodes (prolog/epilog) honoring the configured options.
    /// </summary>
    protected virtual void VisitMiscNodes(IEnumerable<SvgAstNode> nodes, SvgAstEmissionContext context)
    {
        foreach (var node in nodes)
        {
            VisitNode(node, context);
        }
    }

    /// <summary>
    /// Gets the emission result once traversal completes.
    /// </summary>
    protected abstract TResult GetResult(SvgAstEmissionContext context);
}
