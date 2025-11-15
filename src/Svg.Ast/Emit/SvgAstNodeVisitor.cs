// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace Svg.Ast.Emit;

/// <summary>
/// Base class for visitors that traverse <see cref="SvgAstNode"/> graphs.
/// </summary>
public abstract class SvgAstNodeVisitor<TState>
{
    /// <summary>
    /// Visits the supplied document.
    /// </summary>
    public virtual void VisitDocument(SvgAstDocument document, TState state)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (document.RootElement is { } root)
        {
            VisitElement(root, state);
        }
    }

    /// <summary>
    /// Visits an arbitrary SVG AST node.
    /// </summary>
    public virtual void VisitNode(SvgAstNode node, TState state)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        switch (node)
        {
            case SvgAstElement element:
                VisitElement(element, state);
                break;
            case SvgAstText text:
                VisitText(text, state);
                break;
            case SvgAstComment comment:
                VisitComment(comment, state);
                break;
            case SvgAstCData cdata:
                VisitCData(cdata, state);
                break;
            case SvgAstProcessingInstruction pi:
                VisitProcessingInstruction(pi, state);
                break;
            default:
                throw new NotSupportedException($"Unsupported node type: {node?.GetType().FullName}");
        }
    }

    /// <summary>
    /// Visits an element node.
    /// </summary>
    public virtual void VisitElement(SvgAstElement element, TState state)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        VisitAttributes(element, state);
        VisitChildren(element, state);
    }

    /// <summary>
    /// Visits the attribute nodes of the specified element.
    /// </summary>
    protected virtual void VisitAttributes(SvgAstElement element, TState state)
    {
        foreach (var attribute in element.Attributes)
        {
            VisitAttribute(attribute, state);
        }
    }

    /// <summary>
    /// Visits the child nodes of the specified element.
    /// </summary>
    protected virtual void VisitChildren(SvgAstElement element, TState state)
    {
        foreach (var child in element.Children)
        {
            VisitNode(child, state);
        }
    }

    /// <summary>
    /// Visits an attribute node.
    /// </summary>
    public virtual void VisitAttribute(SvgAstAttribute attribute, TState state)
    {
    }

    /// <summary>
    /// Visits a text node.
    /// </summary>
    public virtual void VisitText(SvgAstText text, TState state)
    {
    }

    /// <summary>
    /// Visits a comment node.
    /// </summary>
    public virtual void VisitComment(SvgAstComment comment, TState state)
    {
    }

    /// <summary>
    /// Visits a CDATA node.
    /// </summary>
    public virtual void VisitCData(SvgAstCData cdata, TState state)
    {
    }

    /// <summary>
    /// Visits a processing instruction node.
    /// </summary>
    public virtual void VisitProcessingInstruction(SvgAstProcessingInstruction processingInstruction, TState state)
    {
    }
}
