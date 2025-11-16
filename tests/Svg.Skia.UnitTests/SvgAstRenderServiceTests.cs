// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Linq;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Model.Ast;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAstRenderServiceTests
{
    [Fact]
    public void Renders_Rectangle_With_LinearGradient()
    {
        var svg = """
            <svg width="100" height="100" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="grad1" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stop-color="#ff0000" />
                  <stop offset="100%" stop-color="#0000ff" />
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="100" height="100" fill="url(#grad1)" />
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var gradientCommand = result.Output.Commands!
            .OfType<DrawPathCanvasCommand>()
            .FirstOrDefault(c => c.Paint?.Shader is LinearGradientShader);

        Assert.NotNull(gradientCommand);
        Assert.IsType<LinearGradientShader>(gradientCommand!.Paint!.Shader);
    }

    [Fact]
    public void Renders_Text_Element()
    {
        var svg = """
            <svg width="120" height="40" xmlns="http://www.w3.org/2000/svg">
              <text x="10" y="25" font-size="24">Hello</text>
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var textCommand = result.Output.Commands!
            .OfType<DrawTextCanvasCommand>()
            .FirstOrDefault();

        Assert.NotNull(textCommand);
        Assert.Equal("Hello", textCommand!.Text);
        Assert.Equal(10f, textCommand.X);
        Assert.Equal(25f, textCommand.Y);
    }

    [Fact]
    public void Inherits_Gradient_Stops_From_Reference()
    {
        var svg = """
            <svg width="100" height="100" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="base">
                  <stop offset="0%" stop-color="#0f0" />
                  <stop offset="100%" stop-color="#00f" />
                </linearGradient>
                <linearGradient id="derived" xlink:href="#base" x1="0" y1="0" x2="0" y2="1" />
              </defs>
              <rect x="0" y="0" width="100" height="100" fill="url(#derived)" />
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var gradientCommand = result.Output.Commands!
            .OfType<DrawPathCanvasCommand>()
            .FirstOrDefault(c => c.Paint?.Shader is LinearGradientShader);

        Assert.NotNull(gradientCommand);
    }

    [Fact]
    public void Collapses_Text_Whitespace_By_Default()
    {
        var svg = """
            <svg width="200" height="50" xmlns="http://www.w3.org/2000/svg">
              <text x="5" y="25">  Hello
                  world   </text>
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var textCommand = result.Output!.Commands!
            .OfType<DrawTextCanvasCommand>()
            .FirstOrDefault();

        Assert.NotNull(textCommand);
        Assert.Equal("Hello world", textCommand!.Text);
    }

    [Fact]
    public void Preserves_Text_When_XmlSpace_Preserve()
    {
        var svg = """
            <svg width="200" height="50" xmlns="http://www.w3.org/2000/svg">
              <text x="5" y="25" xml:space="preserve">  Hello   world  </text>
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var textCommand = result.Output!.Commands!
            .OfType<DrawTextCanvasCommand>()
            .FirstOrDefault();

        Assert.NotNull(textCommand);
        Assert.Equal("  Hello   world  ", textCommand!.Text);
    }

    [Fact]
    public void Applies_Rectangular_Mask()
    {
        var svg = """
            <svg width="50" height="50" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <mask id="clipMask" maskUnits="userSpaceOnUse" x="10" y="10" width="20" height="20" />
              </defs>
              <rect x="0" y="0" width="40" height="40" mask="url(#clipMask)" />
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var clipCommand = result.Output.Commands!
            .OfType<ClipRectCanvasCommand>()
            .FirstOrDefault();

        Assert.NotNull(clipCommand);
        Assert.Equal(10f, clipCommand!.Rect.Left);
        Assert.Equal(10f, clipCommand.Rect.Top);
        Assert.Equal(20f, clipCommand.Rect.Width);
        Assert.Equal(20f, clipCommand.Rect.Height);
    }

    [Fact]
    public void Applies_Clip_Path()
    {
        var svg = """
            <svg width="100" height="100" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <clipPath id="clip">
                  <rect x="0" y="0" width="40" height="40" />
                </clipPath>
              </defs>
              <rect x="0" y="0" width="80" height="80" clip-path="url(#clip)" />
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Commands);

        var clipCommand = result.Output.Commands!
            .OfType<ClipPathCanvasCommand>()
            .FirstOrDefault();

        Assert.NotNull(clipCommand);
        Assert.NotNull(clipCommand!.ClipPath);
        Assert.False(clipCommand.ClipPath!.IsEmpty);
    }

    [Fact]
    public void ValidatorDiagnostics_Surface_In_Render_Result()
    {
        var svg = """
            <svg width="10" height="10" xmlns="http://www.w3.org/2000/svg">
              <rect width="10" height="10" fill="url(#missing)" />
            </svg>
            """;

        var document = BuildDocument(svg);
        var result = SvgAstRenderService.Render(document);

        Assert.Contains(result.Diagnostics, d => d.Code == "SVGASTREF001");
    }

    private static SvgAstDocument BuildDocument(string svg)
    {
        var source = SvgSourceText.FromString(svg);
        return SvgAstBuilder.Build(source);
    }
}
