// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Svg.Ast.Emit;
using Svg.Ast.Workspace;

namespace Svg.Model.Ast;

/// <summary>
/// Bridges <see cref="SvgAstWorkspace"/> document changes with <see cref="SvgAstRenderService"/>.
/// </summary>
public sealed class SvgAstWorkspaceRenderer : IDisposable
{
    private readonly SvgAstWorkspace _workspace;
    private readonly SvgAstRenderOptions? _renderOptions;
    private readonly SvgAstEmissionOptions? _emissionOptions;
    private bool _disposed;

    public SvgAstWorkspaceRenderer(
        SvgAstWorkspace workspace,
        SvgAstRenderOptions? renderOptions = null,
        SvgAstEmissionOptions? emissionOptions = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _renderOptions = renderOptions;
        _emissionOptions = emissionOptions;
        _workspace.DocumentChanged += OnWorkspaceDocumentChanged;
    }

    /// <summary>
    /// Raised whenever a workspace document is rendered.
    /// </summary>
    public event EventHandler<SvgAstWorkspaceRenderEventArgs>? DocumentRendered;

    private void OnWorkspaceDocumentChanged(object? sender, SvgAstWorkspaceDocumentChangedEventArgs e)
    {
        var renderResult = SvgAstRenderService.Render(e.Document.Document, _renderOptions, _emissionOptions);
        DocumentRendered?.Invoke(this, new SvgAstWorkspaceRenderEventArgs(e, renderResult));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workspace.DocumentChanged -= OnWorkspaceDocumentChanged;
    }
}
