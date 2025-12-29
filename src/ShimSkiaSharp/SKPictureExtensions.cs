// Copyright (c) Stefan Koell. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ShimSkiaSharp;

public static class SKPictureExtensions
{
    public static SKPicture Clone(this SKPicture picture)
    {
        var cache = new Dictionary<SKPicture, SKPicture>(new ReferenceComparer());
        return ClonePicture(picture, cache);
    }

    private static SKPicture ClonePicture(SKPicture picture, Dictionary<SKPicture, SKPicture> cache)
    {
        if (cache.TryGetValue(picture, out var cached))
        {
            return cached;
        }

        var commands = picture.Commands is null ? null : new List<CanvasCommand>(picture.Commands.Count);
        var clone = new SKPicture(picture.CullRect, commands);
        cache[picture] = clone;

        if (commands is null)
        {
            return clone;
        }

        foreach (var command in picture.Commands!)
        {
            commands.Add(CloneCommand(command, cache));
        }

        return clone;
    }

    private static CanvasCommand CloneCommand(CanvasCommand command, Dictionary<SKPicture, SKPicture> cache)
    {
        return command switch
        {
            ClipPathCanvasCommand clipPathCommand => new ClipPathCanvasCommand(CloneClipPath(clipPathCommand.ClipPath, cache), clipPathCommand.Operation, clipPathCommand.Antialias),
            ClipRectCanvasCommand clipRectCommand => new ClipRectCanvasCommand(clipRectCommand.Rect, clipRectCommand.Operation, clipRectCommand.Antialias),
            DrawImageCanvasCommand drawImageCommand => new DrawImageCanvasCommand(CloneImage(drawImageCommand.Image), drawImageCommand.Source, drawImageCommand.Dest, ClonePaint(drawImageCommand.Paint, cache)),
            DrawPathCanvasCommand drawPathCommand => new DrawPathCanvasCommand(ClonePath(drawPathCommand.Path), ClonePaint(drawPathCommand.Paint, cache)),
            DrawTextBlobCanvasCommand drawTextBlobCommand => new DrawTextBlobCanvasCommand(CloneTextBlob(drawTextBlobCommand.TextBlob), drawTextBlobCommand.X, drawTextBlobCommand.Y, ClonePaint(drawTextBlobCommand.Paint, cache)),
            DrawTextCanvasCommand drawTextCommand => new DrawTextCanvasCommand(drawTextCommand.Text, drawTextCommand.X, drawTextCommand.Y, ClonePaint(drawTextCommand.Paint, cache)),
            DrawTextOnPathCanvasCommand drawTextOnPathCommand => new DrawTextOnPathCanvasCommand(drawTextOnPathCommand.Text, ClonePath(drawTextOnPathCommand.Path), drawTextOnPathCommand.HOffset, drawTextOnPathCommand.VOffset, ClonePaint(drawTextOnPathCommand.Paint, cache)),
            RestoreCanvasCommand restoreCommand => new RestoreCanvasCommand(restoreCommand.Count),
            SaveCanvasCommand saveCommand => new SaveCanvasCommand(saveCommand.Count),
            SaveLayerCanvasCommand saveLayerCommand => new SaveLayerCanvasCommand(saveLayerCommand.Count, ClonePaint(saveLayerCommand.Paint, cache)),
            SetMatrixCanvasCommand setMatrixCommand => new SetMatrixCanvasCommand(setMatrixCommand.DeltaMatrix, setMatrixCommand.TotalMatrix),
            _ => command
        };
    }

    private static SKPaint? ClonePaint(SKPaint? paint, Dictionary<SKPicture, SKPicture> cache)
    {
        if (paint is null)
        {
            return null;
        }

        var clone = paint.Clone();
        clone.Shader = CloneShader(paint.Shader, cache);
        clone.ColorFilter = CloneColorFilter(paint.ColorFilter);
        clone.ImageFilter = CloneImageFilter(paint.ImageFilter, cache);
        clone.PathEffect = ClonePathEffect(paint.PathEffect);
        return clone;
    }

    private static SKPath? ClonePath(SKPath? path)
    {
        if (path is null)
        {
            return null;
        }

        var clone = new SKPath
        {
            FillType = path.FillType
        };

        if (path.Commands is null)
        {
            return clone;
        }

        foreach (var command in path.Commands)
        {
            ClonePathCommand(clone, command);
        }

        return clone;
    }

    private static void ClonePathCommand(SKPath target, PathCommand command)
    {
        switch (command)
        {
            case AddCirclePathCommand addCircle:
                target.AddCircle(addCircle.X, addCircle.Y, addCircle.Radius);
                return;
            case AddOvalPathCommand addOval:
                target.AddOval(addOval.Rect);
                return;
            case AddPolyPathCommand addPoly:
                target.AddPoly(ClonePoints(addPoly.Points), addPoly.Close);
                return;
            case AddRectPathCommand addRect:
                target.AddRect(addRect.Rect);
                return;
            case AddRoundRectPathCommand addRoundRect:
                target.AddRoundRect(addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry);
                return;
            case ArcToPathCommand arcTo:
                target.ArcTo(arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, arcTo.X, arcTo.Y);
                return;
            case ClosePathCommand:
                target.Close();
                return;
            case CubicToPathCommand cubicTo:
                target.CubicTo(cubicTo.X0, cubicTo.Y0, cubicTo.X1, cubicTo.Y1, cubicTo.X2, cubicTo.Y2);
                return;
            case LineToPathCommand lineTo:
                target.LineTo(lineTo.X, lineTo.Y);
                return;
            case MoveToPathCommand moveTo:
                target.MoveTo(moveTo.X, moveTo.Y);
                return;
            case QuadToPathCommand quadTo:
                target.QuadTo(quadTo.X0, quadTo.Y0, quadTo.X1, quadTo.Y1);
                return;
        }
    }

    private static SKPoint[]? ClonePoints(IList<SKPoint>? points)
    {
        if (points is null)
        {
            return null;
        }

        var copy = new SKPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            copy[i] = points[i];
        }
        return copy;
    }

    private static SKImage? CloneImage(SKImage? image)
    {
        if (image is null)
        {
            return null;
        }

        return new SKImage
        {
            Data = image.Data is null ? null : (byte[])image.Data.Clone(),
            Width = image.Width,
            Height = image.Height
        };
    }

    private static SKTextBlob? CloneTextBlob(SKTextBlob? textBlob)
    {
        if (textBlob is null)
        {
            return null;
        }

        var points = textBlob.Points is null ? null : (SKPoint[])textBlob.Points.Clone();
        return SKTextBlob.CreatePositioned(textBlob.Text, points);
    }

    private static ClipPath? CloneClipPath(ClipPath? clipPath, Dictionary<SKPicture, SKPicture> cache)
    {
        if (clipPath is null)
        {
            return null;
        }

        var clone = new ClipPath
        {
            Transform = clipPath.Transform,
            Clip = CloneClipPath(clipPath.Clip, cache)
        };

        if (clipPath.Clips is null)
        {
            return clone;
        }

        var clips = new List<PathClip>(clipPath.Clips.Count);
        foreach (var clip in clipPath.Clips)
        {
            clips.Add(ClonePathClip(clip, cache));
        }
        clone.Clips = clips;

        return clone;
    }

    private static PathClip ClonePathClip(PathClip pathClip, Dictionary<SKPicture, SKPicture> cache)
    {
        return new PathClip
        {
            Path = ClonePath(pathClip.Path),
            Transform = pathClip.Transform,
            Clip = CloneClipPath(pathClip.Clip, cache)
        };
    }

    private static SKShader? CloneShader(SKShader? shader, Dictionary<SKPicture, SKPicture> cache)
    {
        switch (shader)
        {
            case null:
                return null;
            case ColorShader colorShader:
                return new ColorShader(colorShader.Color, colorShader.ColorSpace);
            case LinearGradientShader linearGradient:
                return new LinearGradientShader(
                    linearGradient.Start,
                    linearGradient.End,
                    linearGradient.Colors is null ? null : (SKColorF[])linearGradient.Colors.Clone(),
                    linearGradient.ColorSpace,
                    linearGradient.ColorPos is null ? null : (float[])linearGradient.ColorPos.Clone(),
                    linearGradient.Mode,
                    linearGradient.LocalMatrix);
            case PerlinNoiseFractalNoiseShader perlinFractal:
                return new PerlinNoiseFractalNoiseShader(
                    perlinFractal.BaseFrequencyX,
                    perlinFractal.BaseFrequencyY,
                    perlinFractal.NumOctaves,
                    perlinFractal.Seed,
                    perlinFractal.TileSize);
            case PerlinNoiseTurbulenceShader perlinTurbulence:
                return new PerlinNoiseTurbulenceShader(
                    perlinTurbulence.BaseFrequencyX,
                    perlinTurbulence.BaseFrequencyY,
                    perlinTurbulence.NumOctaves,
                    perlinTurbulence.Seed,
                    perlinTurbulence.TileSize);
            case PictureShader pictureShader:
                return new PictureShader(
                    pictureShader.Src is null ? null : ClonePicture(pictureShader.Src, cache),
                    pictureShader.TmX,
                    pictureShader.TmY,
                    pictureShader.LocalMatrix,
                    pictureShader.Tile);
            case RadialGradientShader radialGradient:
                return new RadialGradientShader(
                    radialGradient.Center,
                    radialGradient.Radius,
                    radialGradient.Colors is null ? null : (SKColorF[])radialGradient.Colors.Clone(),
                    radialGradient.ColorSpace,
                    radialGradient.ColorPos is null ? null : (float[])radialGradient.ColorPos.Clone(),
                    radialGradient.Mode,
                    radialGradient.LocalMatrix);
            case TwoPointConicalGradientShader twoPoint:
                return new TwoPointConicalGradientShader(
                    twoPoint.Start,
                    twoPoint.StartRadius,
                    twoPoint.End,
                    twoPoint.EndRadius,
                    twoPoint.Colors is null ? null : (SKColorF[])twoPoint.Colors.Clone(),
                    twoPoint.ColorSpace,
                    twoPoint.ColorPos is null ? null : (float[])twoPoint.ColorPos.Clone(),
                    twoPoint.Mode,
                    twoPoint.LocalMatrix);
            default:
                return shader;
        }
    }

    private static SKColorFilter? CloneColorFilter(SKColorFilter? colorFilter)
    {
        return colorFilter switch
        {
            null => null,
            BlendModeColorFilter blendMode => new BlendModeColorFilter(blendMode.Color, blendMode.Mode),
            ColorMatrixColorFilter matrix => new ColorMatrixColorFilter(matrix.Matrix is null ? null : (float[])matrix.Matrix.Clone()),
            LumaColorColorFilter => new LumaColorColorFilter(),
            TableColorFilter table => new TableColorFilter(
                table.TableA is null ? null : (byte[])table.TableA.Clone(),
                table.TableR is null ? null : (byte[])table.TableR.Clone(),
                table.TableG is null ? null : (byte[])table.TableG.Clone(),
                table.TableB is null ? null : (byte[])table.TableB.Clone()),
            _ => colorFilter
        };
    }

    private static SKImageFilter? CloneImageFilter(SKImageFilter? imageFilter, Dictionary<SKPicture, SKPicture> cache)
    {
        switch (imageFilter)
        {
            case null:
                return null;
            case ArithmeticImageFilter arithmetic:
                return new ArithmeticImageFilter(
                    arithmetic.K1,
                    arithmetic.K2,
                    arithmetic.K3,
                    arithmetic.K4,
                    arithmetic.EforcePMColor,
                    CloneImageFilter(arithmetic.Background, cache),
                    CloneImageFilter(arithmetic.Foreground, cache),
                    arithmetic.Clip);
            case BlendModeImageFilter blendMode:
                return new BlendModeImageFilter(
                    blendMode.Mode,
                    CloneImageFilter(blendMode.Background, cache),
                    CloneImageFilter(blendMode.Foreground, cache),
                    blendMode.Clip);
            case BlurImageFilter blur:
                return new BlurImageFilter(
                    blur.SigmaX,
                    blur.SigmaY,
                    CloneImageFilter(blur.Input, cache),
                    blur.Clip);
            case ColorFilterImageFilter colorFilter:
                return new ColorFilterImageFilter(
                    CloneColorFilter(colorFilter.ColorFilter),
                    CloneImageFilter(colorFilter.Input, cache),
                    colorFilter.Clip);
            case DilateImageFilter dilate:
                return new DilateImageFilter(
                    dilate.RadiusX,
                    dilate.RadiusY,
                    CloneImageFilter(dilate.Input, cache),
                    dilate.Clip);
            case DisplacementMapEffectImageFilter displacement:
                return new DisplacementMapEffectImageFilter(
                    displacement.XChannelSelector,
                    displacement.YChannelSelector,
                    displacement.Scale,
                    CloneImageFilter(displacement.Displacement, cache),
                    CloneImageFilter(displacement.Input, cache),
                    displacement.Clip);
            case DistantLitDiffuseImageFilter distantDiffuse:
                return new DistantLitDiffuseImageFilter(
                    distantDiffuse.Direction,
                    distantDiffuse.LightColor,
                    distantDiffuse.SurfaceScale,
                    distantDiffuse.Kd,
                    CloneImageFilter(distantDiffuse.Input, cache),
                    distantDiffuse.Clip);
            case DistantLitSpecularImageFilter distantSpecular:
                return new DistantLitSpecularImageFilter(
                    distantSpecular.Direction,
                    distantSpecular.LightColor,
                    distantSpecular.SurfaceScale,
                    distantSpecular.Ks,
                    distantSpecular.Shininess,
                    CloneImageFilter(distantSpecular.Input, cache),
                    distantSpecular.Clip);
            case ErodeImageFilter erode:
                return new ErodeImageFilter(
                    erode.RadiusX,
                    erode.RadiusY,
                    CloneImageFilter(erode.Input, cache),
                    erode.Clip);
            case ImageImageFilter image:
                return new ImageImageFilter(
                    CloneImage(image.Image),
                    image.Src,
                    image.Dst,
                    image.FilterQuality);
            case MatrixConvolutionImageFilter matrix:
                return new MatrixConvolutionImageFilter(
                    matrix.KernelSize,
                    matrix.Kernel is null ? null : (float[])matrix.Kernel.Clone(),
                    matrix.Gain,
                    matrix.Bias,
                    matrix.KernelOffset,
                    matrix.TileMode,
                    matrix.ConvolveAlpha,
                    CloneImageFilter(matrix.Input, cache),
                    matrix.Clip);
            case MergeImageFilter merge:
                return new MergeImageFilter(CloneImageFilters(merge.Filters, cache), merge.Clip);
            case OffsetImageFilter offset:
                return new OffsetImageFilter(
                    offset.Dx,
                    offset.Dy,
                    CloneImageFilter(offset.Input, cache),
                    offset.Clip);
            case PaintImageFilter paint:
                return new PaintImageFilter(ClonePaint(paint.Paint, cache), paint.Clip);
            case ShaderImageFilter shader:
                return new ShaderImageFilter(CloneShader(shader.Shader, cache), shader.Dither, shader.Clip);
            case PictureImageFilter picture:
                return new PictureImageFilter(picture.Picture is null ? null : ClonePicture(picture.Picture, cache), picture.Clip);
            case PointLitDiffuseImageFilter pointDiffuse:
                return new PointLitDiffuseImageFilter(
                    pointDiffuse.Location,
                    pointDiffuse.LightColor,
                    pointDiffuse.SurfaceScale,
                    pointDiffuse.Kd,
                    CloneImageFilter(pointDiffuse.Input, cache),
                    pointDiffuse.Clip);
            case PointLitSpecularImageFilter pointSpecular:
                return new PointLitSpecularImageFilter(
                    pointSpecular.Location,
                    pointSpecular.LightColor,
                    pointSpecular.SurfaceScale,
                    pointSpecular.Ks,
                    pointSpecular.Shininess,
                    CloneImageFilter(pointSpecular.Input, cache),
                    pointSpecular.Clip);
            case SpotLitDiffuseImageFilter spotDiffuse:
                return new SpotLitDiffuseImageFilter(
                    spotDiffuse.Location,
                    spotDiffuse.Target,
                    spotDiffuse.SpecularExponent,
                    spotDiffuse.CutoffAngle,
                    spotDiffuse.LightColor,
                    spotDiffuse.SurfaceScale,
                    spotDiffuse.Kd,
                    CloneImageFilter(spotDiffuse.Input, cache),
                    spotDiffuse.Clip);
            case SpotLitSpecularImageFilter spotSpecular:
                return new SpotLitSpecularImageFilter(
                    spotSpecular.Location,
                    spotSpecular.Target,
                    spotSpecular.SpecularExponent,
                    spotSpecular.CutoffAngle,
                    spotSpecular.LightColor,
                    spotSpecular.SurfaceScale,
                    spotSpecular.Ks,
                    spotSpecular.Shininess,
                    CloneImageFilter(spotSpecular.Input, cache),
                    spotSpecular.Clip);
            case TileImageFilter tile:
                return new TileImageFilter(tile.Src, tile.Dst, CloneImageFilter(tile.Input, cache));
            default:
                return imageFilter;
        }
    }

    private static SKImageFilter[]? CloneImageFilters(SKImageFilter[]? filters, Dictionary<SKPicture, SKPicture> cache)
    {
        if (filters is null)
        {
            return null;
        }

        var clone = new SKImageFilter[filters.Length];
        for (var i = 0; i < filters.Length; i++)
        {
            clone[i] = CloneImageFilter(filters[i], cache)!;
        }
        return clone;
    }

    private static SKPathEffect? ClonePathEffect(SKPathEffect? pathEffect)
    {
        return pathEffect switch
        {
            null => null,
            DashPathEffect dash => new DashPathEffect(dash.Intervals is null ? null : (float[])dash.Intervals.Clone(), dash.Phase),
            _ => pathEffect
        };
    }

    private sealed class ReferenceComparer : IEqualityComparer<SKPicture>
    {
        public bool Equals(SKPicture? x, SKPicture? y) => ReferenceEquals(x, y);

        public int GetHashCode(SKPicture obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
