// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Svg.Ast.Emit;
using Svg.Ast.Workspace;

namespace Svg.Model.Ast;

/// <summary>
/// Provides data for <see cref="SvgAstWorkspaceRenderer.DocumentRendered"/>.
/// </summary>
public sealed class SvgAstWorkspaceRenderEventArgs : EventArgs
{
    public SvgAstWorkspaceRenderEventArgs(
        SvgAstWorkspaceDocumentChangedEventArgs workspaceEventArgs,
        SvgAstEmissionResult<ShimSkiaSharp.SKPicture?> renderResult)
    {
        WorkspaceEventArgs = workspaceEventArgs ?? throw new ArgumentNullException(nameof(workspaceEventArgs));
        RenderResult = renderResult ?? throw new ArgumentNullException(nameof(renderResult));
    }

    public SvgAstWorkspaceDocumentChangedEventArgs WorkspaceEventArgs { get; }

    public SvgAstEmissionResult<ShimSkiaSharp.SKPicture?> RenderResult { get; }
}
