// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Drawing;
using System.Linq;
using Svg;
using Svg.Ast;
using Svg.Ast.Emit;
using Svg.Model.Ast;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAstDomEmitterTests
{
    [Fact]
    public void Emits_Rectangle_With_Presentation_Attributes()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"shape\" width=\"100\" height=\"50\" fill=\"#ff0000\" /></svg>";
        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var pipeline = new SvgAstEmissionPipeline(new[] { (ISvgAstEmissionStage)new SvgAstValidationStage() });
        var result = pipeline.Emit(document, new SvgAstDomEmitter());

        Assert.NotNull(result.Output);
        var rectangle = result.Output!.Children.OfType<SvgRectangle>().Single();
        Assert.Equal("shape", rectangle.ID);
        Assert.Equal(100f, rectangle.Width.Value);
        Assert.Equal(50f, rectangle.Height.Value);
        var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), fill.Colour);
    }

    [Fact]
    public void Inline_Style_Is_Parsed()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect style=\"fill:#00ff00;stroke:#0000ff;stroke-width:4\" width=\"20\" height=\"10\" /></svg>";
        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var pipeline = new SvgAstEmissionPipeline(new[] { (ISvgAstEmissionStage)new SvgAstValidationStage() });
        var result = pipeline.Emit(document, new SvgAstDomEmitter());

        var rectangle = result.Output!.Children.OfType<SvgRectangle>().Single();
        var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
        Assert.Equal(Color.FromArgb(255, 0, 255, 0), fill.Colour);
        var stroke = Assert.IsType<SvgColourServer>(rectangle.Stroke);
        Assert.Equal(Color.FromArgb(255, 0, 0, 255), stroke.Colour);
        Assert.Equal(4f, rectangle.StrokeWidth.Value);
    }
}
