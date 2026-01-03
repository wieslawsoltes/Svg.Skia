using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.UnitTests;

internal static class DrawableCloneTestData
{
    public static SKPaint CreatePaint(byte alpha)
    {
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            Color = new SKColor(10, 20, 30, alpha),
            StrokeWidth = 2,
            StrokeMiter = 3
        };
    }

    public static SKPath CreatePath()
    {
        var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        path.MoveTo(1, 2);
        path.LineTo(3, 4);
        return path;
    }

    public static ClipPath CreateClipPath()
    {
        var clipPath = new ClipPath
        {
            Transform = SKMatrix.CreateScale(2, 3),
            Clip = new ClipPath()
        };

        clipPath.Clips!.Add(new PathClip
        {
            Path = CreatePath(),
            Transform = SKMatrix.CreateTranslation(1, 2)
        });

        return clipPath;
    }

    public static SKImage CreateImage()
        => new() { Data = new byte[] { 1, 2, 3 }, Width = 10, Height = 20 };

    public static HashSet<Uri> CreateReferences()
        => new() { new Uri("https://example.test") };
}
