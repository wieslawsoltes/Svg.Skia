// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Microsoft.Language.Xml;
using Svg.Ast.Workspace;
using Xunit;

namespace Svg.Ast.UnitTests.Workspace;

public class SvgAstWorkspaceTests
{
    [Fact]
    public void AddDocument_BuildsSymbolTable()
    {
        var workspace = new SvgAstWorkspace();
        var source = SvgSourceText.FromString("<svg xmlns=\"http://www.w3.org/2000/svg\"><defs><rect id=\"shape\" /></defs></svg>");

        var document = workspace.AddOrUpdateDocument("doc", source);

        var symbolTable = document.GetOrCreateSymbolTable();
        Assert.NotNull(symbolTable);
        Assert.True(document.TryGetCachedSymbolTable(out var cached));
        Assert.Same(symbolTable, cached);
    }

    [Fact]
    public void ApplyChanges_UsesIncremental_And_UpdatesDocument()
    {
        var workspace = new SvgAstWorkspace();
        var original = SvgSourceText.FromString("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"5\" /></svg>");
        var document = workspace.AddOrUpdateDocument("doc", original);
        var originalSymbolTable = document.GetOrCreateSymbolTable();

        var updatedText = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"15\" height=\"5\" /></svg>";
        var changeStart = original.ToString().IndexOf("10", StringComparison.Ordinal);
        var changes = new[]
        {
            new TextChangeRange(new TextSpan(changeStart, 2), 2)
        };
        var updateResult = workspace.ApplyChanges("doc", SvgSourceText.FromString(updatedText), changes);

        Assert.True(updateResult.UsedIncrementalParser);
        var updatedDocument = updateResult.Document.Document;
        var rect = Assert.IsType<SvgAstElement>(updatedDocument.RootElement!.Children[0]);
        var widthAttribute = Assert.Single(rect.Attributes, a => a.Name.LocalName == "width");
        Assert.Equal("15", widthAttribute.GetValueText());

        Assert.False(updateResult.Document.TryGetCachedSymbolTable(out _));
        var newSymbolTable = updateResult.Document.GetOrCreateSymbolTable();
        Assert.NotSame(originalSymbolTable, newSymbolTable);
    }

    [Fact]
    public void DocumentChanged_Event_Fires_On_Add_And_Update()
    {
        var workspace = new SvgAstWorkspace();
        SvgAstWorkspaceDocumentChangedEventArgs? lastArgs = null;
        var eventCount = 0;
        workspace.DocumentChanged += (_, args) =>
        {
            eventCount++;
            lastArgs = args;
        };

        var addSource = SvgSourceText.FromString("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"20\" /></svg>");
        workspace.AddOrUpdateDocument("doc", addSource);

        Assert.Equal(1, eventCount);
        Assert.NotNull(lastArgs);
        Assert.True(lastArgs!.IsNewDocument);
        Assert.False(lastArgs.UsedIncrementalParser);
        Assert.Equal("doc", lastArgs.DocumentId);
        Assert.Empty(lastArgs.Diagnostics);

        var updateSource = SvgSourceText.FromString("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"15\" height=\"20\" /></svg>");
        workspace.ApplyChanges("doc", updateSource);

        Assert.Equal(2, eventCount);
        Assert.NotNull(lastArgs);
        Assert.False(lastArgs!.IsNewDocument);
        Assert.Equal("doc", lastArgs.DocumentId);
        Assert.Empty(lastArgs.Diagnostics);
    }
}
