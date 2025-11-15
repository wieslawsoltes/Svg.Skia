// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Svg.Ast;
using Svg.Ast.Emit;
using Svg.Model.Ast;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAstSkiaPathCodeEmitterTests
{
    [Fact]
    public void Emits_Rectangle_Code()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"40\" height=\"20\" x=\"5\" y=\"10\" /></svg>";
        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var pipeline = new SvgAstEmissionPipeline(new[] { (ISvgAstEmissionStage)new SvgAstValidationStage() });
        var result = pipeline.Emit(document, new SvgAstSkiaPathCodeEmitter("path"));

        Assert.Contains("path.AddRect", result.Output);
        Assert.Contains("new SKRect(5", result.Output);
    }
}
