using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneCycleDetectionTests
{
    [Fact]
    public void SKImageFilter_DeepClone_ThrowsOnSelfReferentialFilterArray()
    {
        var filters = new SKImageFilter[1];
        var merge = new MergeImageFilter(filters, null);
        filters[0] = merge;

        Assert.Throws<NotSupportedException>(() => merge.DeepClone());
    }

    [Fact]
    public void SKPicture_DeepClone_ThrowsOnSelfReferentialShaderGraph()
    {
        var paint = new SKPaint();
        var commands = new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(CloneTestData.CreatePath(), paint)
        };
        var picture = new SKPicture(SKRect.Create(0, 0, 10, 10), commands);

        paint.Shader = SKShader.CreatePicture(
            picture,
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            SKMatrix.Identity,
            SKRect.Create(0, 0, 10, 10));

        Assert.Throws<NotSupportedException>(() => picture.DeepClone());
    }
}
