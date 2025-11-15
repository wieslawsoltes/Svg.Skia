// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Linq;
using Svg.Ast;
using Xunit;

namespace Svg.Ast.UnitTests.Symbols;

public class SvgSymbolTableTests
{
    [Fact]
    public void Collects_Definitions_By_Id()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="gradA"></linearGradient>
                <clipPath id="clipA">
                  <rect id="rect1" />
                </clipPath>
                <mask id="maskA" />
              </defs>
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var table = SvgSymbolTable.Build(document);

        static string Encode(string value) => $"{value.Length}:{string.Join('-', value.Select(ch => (int)ch))}";
        var allIds = string.Join(",", table.Ids.Keys.Select(Encode));

        Assert.True(table.TryGetElementById("rect1", out var rect), allIds);
        Assert.Equal("rect", rect!.Name.LocalName);

        Assert.True(table.TryGetGradient("gradA", out var gradient), allIds);
        Assert.Equal("linearGradient", gradient!.Name.LocalName);

        Assert.True(table.TryGetClipPath("clipA", out var clip), allIds);
        Assert.Equal("clipPath", clip!.Name.LocalName);

        Assert.True(table.TryGetMask("maskA", out var mask), allIds);
        Assert.Equal("mask", mask!.Name.LocalName);
    }
}
