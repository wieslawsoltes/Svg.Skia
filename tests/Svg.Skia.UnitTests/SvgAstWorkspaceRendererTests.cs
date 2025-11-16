// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Svg.Ast;
using Svg.Ast.Workspace;
using Svg.Model.Ast;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAstWorkspaceRendererTests
{
    [Fact]
    public void Renderer_Raises_Event_On_Document_Add()
    {
        var workspace = new SvgAstWorkspace();
        using var renderer = new SvgAstWorkspaceRenderer(workspace);

        SvgAstWorkspaceRenderEventArgs? lastArgs = null;
        renderer.DocumentRendered += (_, args) => lastArgs = args;

        workspace.AddOrUpdateDocument("doc", SvgSourceText.FromString("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\" /></svg>"));

        Assert.NotNull(lastArgs);
        Assert.NotNull(lastArgs!.RenderResult.Output);
        Assert.True(lastArgs.WorkspaceEventArgs.IsNewDocument);
    }
}
