// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.IO;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Model.Ast;
using Svg.Skia;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAstGoldenTests : SvgUnitTest
{
    private static string GetAssetPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [WindowsTheory]
    [InlineData("SvgAstGradientClip", 0.01)]
    public void Ast_Rendering_Matches_Golden_Image(string assetName, double errorThreshold)
    {
        var svgPath = GetAssetPath($"{assetName}.svg");
        var expectedPng = GetAssetPath($"{assetName}.png");
        var actualPng = GetAssetPath($"{assetName} (AST Actual).png");

        RenderAstToPng(svgPath, actualPng);

        ImageHelper.CompareImages(assetName, actualPng, expectedPng, errorThreshold);

        File.Delete(actualPng);
    }

    private void RenderAstToPng(string svgPath, string pngPath)
    {
        var svgText = File.ReadAllText(svgPath);
        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svgText, normalizeLineEndings: true));
        var renderResult = SvgAstRenderService.Render(document);
        Assert.NotNull(renderResult.Output);

        var settings = new SKSvgSettings();
        SetTypefaceProviders(settings);
        var skiaModel = new SkiaModel(settings);
        using var skPicture = skiaModel.ToSKPicture(renderResult.Output);
        Assert.NotNull(skPicture);

        var cullRect = renderResult.Output!.CullRect;
        var width = Math.Max(1, (int)Math.Ceiling(cullRect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(cullRect.Height));
        var info = new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);

        using var surface = SkiaSharp.SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.DrawPicture(skPicture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var stream = File.Open(pngPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        data.SaveTo(stream);
    }
}
