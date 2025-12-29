using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Svg;
using ShimSkiaSharp;

namespace AvaloniaSvgSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        svgSvgDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgSvgDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgExtensionDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgExtensionDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgSourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgSourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgResourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgResourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        stringTextBox.Text =
            """
            <svg width="100" height="100">
               <circle cx="50" cy="50" r="40" stroke="green" stroke-width="4" fill="yellow" />
            </svg>
            """;

        var svgImage = svgCloningOriginal.Source as SvgImage;
        var clone = new SvgImage
        {
            Source =
                new SvgSource
                {
                    Picture = new SKPicture(svgImage.Source.Picture.CullRect, svgImage.Source.Picture.Commands?
                        .Select(CloneCommand)
                        .OfType<CanvasCommand>()
                        .ToList())
                }
        };

        foreach (var cmd in clone.Source.Picture.Commands?.OfType<DrawPathCanvasCommand>() ?? [])
        {
            var paint = cmd.Paint;
            if (paint?.Color is not null)
            {
                paint.Color = ToGrayscale(paint.Color.Value);
            }

            if (paint?.Shader is ColorShader shader)
            {
                paint.Shader = SKShader.CreateColor(ToGrayscale(paint.Color.Value), shader.ColorSpace);
            }
        }

        svgCloningClone.Source = clone;
    }

    private static SKColor ToGrayscale(SKColor color)
    {
        var luminance = (byte)(0.2126f * color.Red + 0.7152f * color.Green + 0.0722f * color.Blue);
        return new SKColor(luminance, luminance, luminance, color.Alpha);
    }

        private static CanvasCommand? CloneCommand<T>(T arg) where T : CanvasCommand => arg switch
    {
        ClipPathCanvasCommand command => CloneCanvasCommand(command),
        ClipRectCanvasCommand command => CloneCanvasCommand(command),
        DrawImageCanvasCommand command => CloneCanvasCommand(command),
        DrawPathCanvasCommand command => CloneCanvasCommand(command),
        DrawTextBlobCanvasCommand command => CloneCanvasCommand(command),
        DrawTextCanvasCommand command => CloneCanvasCommand(command),
        DrawTextOnPathCanvasCommand command => CloneCanvasCommand(command),
        RestoreCanvasCommand command => CloneCanvasCommand(command),
        SaveCanvasCommand command => CloneCanvasCommand(command),
        SaveLayerCanvasCommand command => CloneCanvasCommand(command),
        SetMatrixCanvasCommand command => CloneCanvasCommand(command),
        _ => null
    };


    private static CanvasCommand CloneCanvasCommand(ClipRectCanvasCommand command)
        => new ClipRectCanvasCommand(command.Rect, command.Operation, command.Antialias);

    private static CanvasCommand CloneCanvasCommand(ClipPathCanvasCommand command)
        => new ClipPathCanvasCommand(CloneClipPath(command.ClipPath), command.Operation, command.Antialias);

    private static CanvasCommand CloneCanvasCommand(DrawImageCanvasCommand command)
        => new DrawImageCanvasCommand(CloneImage(command.Image), command.Source, command.Dest,
            ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(DrawPathCanvasCommand command)
        => new DrawPathCanvasCommand(command.Path, ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(DrawTextBlobCanvasCommand command)
        => new DrawTextBlobCanvasCommand(command.TextBlob is not null
                ? SKTextBlob.CreatePositioned(command.TextBlob.Text, command.TextBlob.Points)
                : null,
            command.X,
            command.Y,
            ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(DrawTextCanvasCommand command)
        => new DrawTextCanvasCommand(command.Text, command.X, command.Y, ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(DrawTextOnPathCanvasCommand command)
        => new DrawTextOnPathCanvasCommand(command.Text, command.Path, command.HOffset, command.VOffset,
            ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(RestoreCanvasCommand command)
        => new RestoreCanvasCommand(command.Count);

    private static CanvasCommand CloneCanvasCommand(SaveCanvasCommand command)
        => new SaveCanvasCommand(command.Count);

    private static CanvasCommand CloneCanvasCommand(SaveLayerCanvasCommand command)
        => new SaveLayerCanvasCommand(command.Count, ClonePaint(command.Paint));

    private static CanvasCommand CloneCanvasCommand(SetMatrixCanvasCommand command)
        => new SetMatrixCanvasCommand(command.DeltaMatrix, command.TotalMatrix);

    private static SKColorFilter? CloneColorFilter(SKColorFilter? filter)
    {
        if (filter is null)
            return null;

        return filter switch
        {
            BlendModeColorFilter bc => SKColorFilter.CreateBlendMode(bc.Color, bc.Mode),
            ColorMatrixColorFilter cm => cm.Matrix is not null ? SKColorFilter.CreateColorMatrix(cm.Matrix) : null,
            LumaColorColorFilter => SKColorFilter.CreateLumaColor(),
            TableColorFilter tf => SKColorFilter.CreateTable(tf.TableA, tf.TableR, tf.TableG, tf.TableB),
            _ => null
        };
    }

    private static ClipPath? CloneClipPath(ClipPath? clipPath)
    {
        if (clipPath is null)
            return null;

        return new ClipPath
        {
            Clips = clipPath.Clips?.Select(p => new PathClip
            {
                Path = p.Path,
                Transform = p.Transform,
                Clip = CloneClipPath(p.Clip)
            }).ToList(),
            Transform = clipPath.Transform,
            Clip = CloneClipPath(clipPath.Clip)
        };
    }

    private static SKImage? CloneImage(SKImage? image)
    {
        if (image is null)
            return null;

        return new SKImage
        {
            Data = image.Data,
            Width = image.Width,
            Height = image.Height
        };
    }

    private static SKImageFilter? CloneImageFilter(SKImageFilter? filter)
    {
        if (filter is null)
            return null;

        return filter switch
        {
            ArithmeticImageFilter arithmeticFilter =>
                SKImageFilter.CreateArithmetic(
                    arithmeticFilter.K1,
                    arithmeticFilter.K2,
                    arithmeticFilter.K3,
                    arithmeticFilter.K4,
                    arithmeticFilter.EforcePMColor,
                    CloneImageFilter(arithmeticFilter.Background)!,
                    CloneImageFilter(arithmeticFilter.Foreground),
                    arithmeticFilter.Clip),
            BlendModeImageFilter blendFilter => SKImageFilter.CreateBlendMode(
                blendFilter.Mode,
                CloneImageFilter(blendFilter.Background)!,
                CloneImageFilter(blendFilter.Foreground)!,
                blendFilter.Clip),
            BlurImageFilter blurFilter => SKImageFilter.CreateBlur(
                blurFilter.SigmaX,
                blurFilter.SigmaY,
                CloneImageFilter(blurFilter.Input),
                blurFilter.Clip),
            ColorFilterImageFilter colorFilter => SKImageFilter.CreateColorFilter(
                CloneColorFilter(colorFilter.ColorFilter)!,
                CloneImageFilter(colorFilter.Input),
                colorFilter.Clip),
            DilateImageFilter dilateFilter => SKImageFilter.CreateDilate(
                dilateFilter.RadiusX,
                dilateFilter.RadiusY,
                CloneImageFilter(dilateFilter.Input),
                dilateFilter.Clip),
            DisplacementMapEffectImageFilter displaceMapFilter => SKImageFilter.CreateDisplacementMapEffect(
                displaceMapFilter.XChannelSelector,
                displaceMapFilter.YChannelSelector,
                displaceMapFilter.Scale,
                CloneImageFilter(displaceMapFilter.Displacement)!,
                CloneImageFilter(displaceMapFilter.Input),
                displaceMapFilter.Clip),
            DistantLitDiffuseImageFilter diffuseFilter => SKImageFilter.CreateDistantLitDiffuse(
                diffuseFilter.Direction,
                diffuseFilter.LightColor,
                diffuseFilter.SurfaceScale,
                diffuseFilter.Kd,
                CloneImageFilter(diffuseFilter.Input),
                diffuseFilter.Clip),
            DistantLitSpecularImageFilter specularFilter => SKImageFilter.CreateDistantLitSpecular(
                specularFilter.Direction,
                specularFilter.LightColor,
                specularFilter.SurfaceScale,
                specularFilter.Ks,
                specularFilter.Shininess,
                CloneImageFilter(specularFilter.Input),
                specularFilter.Clip),
            ErodeImageFilter erodeFilter => SKImageFilter.CreateErode(
                erodeFilter.RadiusX,
                erodeFilter.RadiusY,
                CloneImageFilter(erodeFilter.Input),
                erodeFilter.Clip),
            ImageImageFilter imageFilter => SKImageFilter.CreateImage(
                CloneImage(imageFilter.Image)!,
                imageFilter.Src,
                imageFilter.Dst,
                imageFilter.FilterQuality),
            MatrixConvolutionImageFilter matrixConvFilter => SKImageFilter.CreateMatrixConvolution(
                matrixConvFilter.KernelSize,
                matrixConvFilter.Kernel ?? Array.Empty<float>(),
                matrixConvFilter.Gain,
                matrixConvFilter.Bias,
                matrixConvFilter.KernelOffset,
                matrixConvFilter.TileMode,
                matrixConvFilter.ConvolveAlpha,
                CloneImageFilter(matrixConvFilter.Input),
                matrixConvFilter.Clip),
            MergeImageFilter mergeFilter => SKImageFilter.CreateMerge(
                mergeFilter.Filters?.Select(CloneImageFilter).OfType<SKImageFilter>().ToArray() ??
                Array.Empty<SKImageFilter>(),
                mergeFilter.Clip),
            OffsetImageFilter offsetFilter => SKImageFilter.CreateOffset(
                offsetFilter.Dx,
                offsetFilter.Dy,
                CloneImageFilter(offsetFilter.Input),
                offsetFilter.Clip),
            PaintImageFilter paintFilter => SKImageFilter.CreatePaint(
                ClonePaint(paintFilter.Paint)!,
                paintFilter.Clip),
            PictureImageFilter pictureFilter => SKImageFilter.CreatePicture(
                ClonePicture(pictureFilter.Picture)!,
                pictureFilter.Clip.GetValueOrDefault()),
            PointLitDiffuseImageFilter pointDiffuseFilter => SKImageFilter.CreatePointLitDiffuse(
                pointDiffuseFilter.Location,
                pointDiffuseFilter.LightColor,
                pointDiffuseFilter.SurfaceScale,
                pointDiffuseFilter.Kd,
                CloneImageFilter(pointDiffuseFilter.Input),
                pointDiffuseFilter.Clip),
            PointLitSpecularImageFilter pointSpecularFilter => SKImageFilter.CreatePointLitSpecular(
                pointSpecularFilter.Location,
                pointSpecularFilter.LightColor,
                pointSpecularFilter.SurfaceScale,
                pointSpecularFilter.Ks,
                pointSpecularFilter.Shininess,
                CloneImageFilter(pointSpecularFilter.Input),
                pointSpecularFilter.Clip),
            SpotLitDiffuseImageFilter spotDiffuseFilter => SKImageFilter.CreateSpotLitDiffuse(
                spotDiffuseFilter.Location,
                spotDiffuseFilter.Target,
                spotDiffuseFilter.SpecularExponent,
                spotDiffuseFilter.CutoffAngle,
                spotDiffuseFilter.LightColor,
                spotDiffuseFilter.SurfaceScale,
                spotDiffuseFilter.Kd,
                CloneImageFilter(spotDiffuseFilter.Input),
                spotDiffuseFilter.Clip),
            SpotLitSpecularImageFilter spotSpecularFilter => SKImageFilter.CreateSpotLitSpecular(
                spotSpecularFilter.Location,
                spotSpecularFilter.Target,
                spotSpecularFilter.SpecularExponent,
                spotSpecularFilter.CutoffAngle,
                spotSpecularFilter.LightColor,
                spotSpecularFilter.SurfaceScale,
                spotSpecularFilter.Ks,
                spotSpecularFilter.Shininess,
                CloneImageFilter(spotSpecularFilter.Input),
                spotSpecularFilter.Clip),
            TileImageFilter tileFilter => SKImageFilter.CreateTile(
                tileFilter.Src,
                tileFilter.Dst,
                CloneImageFilter(tileFilter.Input)),
            _ => null
        };
    }

    private static SKPaint? ClonePaint(SKPaint? paint)
    {
        if (paint is null)
            return null;

        return new SKPaint
        {
            Style = paint.Style,
            IsAntialias = paint.IsAntialias,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = paint.StrokeCap,
            StrokeJoin = paint.StrokeJoin,
            StrokeMiter = paint.StrokeMiter,
            Typeface = paint.Typeface?.FamilyName != null
                ? SKTypeface.FromFamilyName(paint.Typeface.FamilyName,
                    paint.Typeface.FontWeight,
                    paint.Typeface.FontWidth,
                    paint.Typeface.FontSlant)
                : null,
            TextSize = paint.TextSize,
            TextAlign = paint.TextAlign,
            LcdRenderText = paint.LcdRenderText,
            SubpixelText = paint.SubpixelText,
            TextEncoding = paint.TextEncoding,
            Color = paint.Color,
            Shader = CloneShader(paint.Shader),
            ColorFilter = CloneColorFilter(paint.ColorFilter),
            ImageFilter = CloneImageFilter(paint.ImageFilter),
            PathEffect = paint.PathEffect is DashPathEffect dashPathEffect
                ? new DashPathEffect(dashPathEffect.Intervals, dashPathEffect.Phase)
                : null,
            BlendMode = paint.BlendMode,
            FilterQuality = paint.FilterQuality,
        };
    }

    private static SKPicture? ClonePicture(SKPicture? picture)
    {
        if (picture is null)
            return picture;

        return new SKPicture(picture.CullRect, picture.Commands?.Select(CloneCommand).OfType<CanvasCommand>().ToList());
    }

    private static SKShader? CloneShader(SKShader? shader)
    {
        if (shader is null)
            return shader;

        return shader switch
        {
            ColorShader color => new ColorShader(color.Color, color.ColorSpace),
            LinearGradientShader l => new LinearGradientShader(l.Start, l.End, l.Colors, l.ColorSpace, l.ColorPos,
                l.Mode, l.LocalMatrix),
            PerlinNoiseFractalNoiseShader fractal => new PerlinNoiseFractalNoiseShader(fractal.BaseFrequencyX,
                fractal.BaseFrequencyY, fractal.NumOctaves, fractal.Seed, fractal.TileSize),
            PerlinNoiseTurbulenceShader turbulence => new PerlinNoiseTurbulenceShader(turbulence.BaseFrequencyX,
                turbulence.BaseFrequencyY, turbulence.NumOctaves, turbulence.Seed, turbulence.TileSize),
            PictureShader picture => new PictureShader(ClonePicture(picture.Src), picture.TmX, picture.TmY,
                picture.LocalMatrix, picture.Tile),
            RadialGradientShader radial => new RadialGradientShader(radial.Center, radial.Radius, radial.Colors,
                radial.ColorSpace, radial.ColorPos, radial.Mode, radial.LocalMatrix),
            TwoPointConicalGradientShader conical => new TwoPointConicalGradientShader(conical.Start,
                conical.StartRadius, conical.End, conical.EndRadius, conical.Colors, conical.ColorSpace,
                conical.ColorPos, conical.Mode, conical.LocalMatrix),
            _ => null
        };
    }

    public void SvgSvgStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgSvg is { })
        {
            var comboBox = (ComboBox)sender;
            svgSvg.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgExtensionStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgExtensionImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgExtensionImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgSourceStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgSourceImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgSourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgResourceStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgResourceImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgResourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgStringStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgString is { })
        {
            var comboBox = (ComboBox)sender;
            svgString.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    private void DragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

        if (!e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var fileName = e.Data.GetFileNames()?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                if (sender == svgSvgDockPanel)
                {
                    svgSvg.Path = fileName;
                }
                else if (sender == svgExtensionDockPanel)
                {
                    svgExtensionImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
                }
                else if (sender == svgSourceDockPanel)
                {
                    svgSourceImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
                }
                else if (sender == svgResourceDockPanel)
                {
                    svgResourceImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
                }
                else if (sender == stringTextBox || sender == svgString)
                {
                    var source = File.ReadAllText(fileName);
                    stringTextBox.Text = source;
                }
            }
        }
    }
}
