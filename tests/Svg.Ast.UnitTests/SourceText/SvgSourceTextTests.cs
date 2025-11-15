// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Svg.Ast;
using Xunit;

namespace Svg.Ast.UnitTests.SourceText;

public class SvgSourceTextTests
{
    [Fact]
    public void FromString_Normalizes_LineEndings_By_Default()
    {
        var input = "line1\r\nline2\rline3\nline4";
        var source = SvgSourceText.FromString(input);

        var text = source.ToString();
        Assert.Equal("line1\nline2\nline3\nline4", text);
    }

    [Fact]
    public void Slice_Returns_Spans_Without_Copying()
    {
        var source = SvgSourceText.FromString("<svg>\n  <rect />\n</svg>");
        var span = source.Slice(8, 8);

        Assert.Equal("<rect />", span.ToString());
    }
}
