using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Svg.Model.Services;
using Svg.Skia;
using ShimSkiaSharp;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class SkiaModelApiBenchmarks
{
    private SkiaModel _skiaModel = null!;
    private ISvgAssetLoader _assetLoader = null!;
    private SKPicture _model = null!;
    private SKPaint[] _paints = Array.Empty<SKPaint>();
    private SKPath[] _paths = Array.Empty<SKPath>();
    private SKImage[] _images = Array.Empty<SKImage>();
    private ClipPath[] _clipPaths = Array.Empty<ClipPath>();
    private SKShader[] _shaders = Array.Empty<SKShader>();
    private SKImageFilter[] _imageFilters = Array.Empty<SKImageFilter>();
    private SKColorFilter[] _colorFilters = Array.Empty<SKColorFilter>();
    private SKPathEffect[] _pathEffects = Array.Empty<SKPathEffect>();
    private TextBlobEntry[] _textBlobs = Array.Empty<TextBlobEntry>();

    [ParamsSource(nameof(SvgNames))]
    public string SvgName { get; set; } = string.Empty;

    public IEnumerable<string> SvgNames => BenchmarkAssets.SvgNames;

    [GlobalSetup]
    public void Setup()
    {
        _skiaModel = new SkiaModel(new SKSvgSettings());
        _assetLoader = new SkiaSvgAssetLoader(_skiaModel);

        var svgText = BenchmarkAssets.GetSvgText(SvgName);
        var document = SvgService.FromSvg(svgText) ?? throw new InvalidOperationException("Failed to parse SVG document.");
        _model = SvgService.ToModel(document, _assetLoader, out _, out _) ?? throw new InvalidOperationException("Failed to build SVG model.");

        var collector = new ModelObjectCollector();
        collector.Collect(_model);
        var objects = collector.Freeze();
        _paints = objects.Paints;
        _paths = objects.Paths;
        _images = objects.Images;
        _clipPaths = objects.ClipPaths;
        _shaders = objects.Shaders;
        _imageFilters = objects.ImageFilters;
        _colorFilters = objects.ColorFilters;
        _pathEffects = objects.PathEffects;
        _textBlobs = objects.TextBlobs;
    }

    [Benchmark]
    public void ConvertPaints()
    {
        foreach (var paint in _paints)
        {
            using var skPaint = _skiaModel.ToSKPaint(paint);
        }
    }

    [Benchmark]
    public void ConvertPaths()
    {
        foreach (var path in _paths)
        {
            using var skPath = _skiaModel.ToSKPath(path);
        }
    }

    [Benchmark]
    public void ConvertImages()
    {
        foreach (var image in _images)
        {
            using var skImage = _skiaModel.ToSKImage(image);
        }
    }

    [Benchmark]
    public void ConvertClipPaths()
    {
        foreach (var clipPath in _clipPaths)
        {
            using var skPath = _skiaModel.ToSKPath(clipPath);
        }
    }

    [Benchmark]
    public void ConvertShaders()
    {
        foreach (var shader in _shaders)
        {
            using var skShader = _skiaModel.ToSKShader(shader);
        }
    }

    [Benchmark]
    public void ConvertImageFilters()
    {
        foreach (var imageFilter in _imageFilters)
        {
            using var skImageFilter = _skiaModel.ToSKImageFilter(imageFilter);
        }
    }

    [Benchmark]
    public void ConvertColorFilters()
    {
        foreach (var colorFilter in _colorFilters)
        {
            using var skColorFilter = _skiaModel.ToSKColorFilter(colorFilter);
        }
    }

    [Benchmark]
    public void ConvertPathEffects()
    {
        foreach (var pathEffect in _pathEffects)
        {
            using var skPathEffect = _skiaModel.ToSKPathEffect(pathEffect);
        }
    }

    [Benchmark]
    public void ConvertTextBlobs()
    {
        foreach (var entry in _textBlobs)
        {
            if (entry.TextBlob?.Points is null || string.IsNullOrEmpty(entry.TextBlob.Text))
            {
                continue;
            }

            using var skPaint = _skiaModel.ToSKPaint(entry.Paint);
            if (skPaint is null)
            {
                continue;
            }

            var points = _skiaModel.ToSKPoints(entry.TextBlob.Points);
            using var font = skPaint.ToFont();
            using var textBlob = SkiaSharp.SKTextBlob.CreatePositioned(entry.TextBlob.Text, font, points);
        }
    }

    private sealed class ModelObjectCollector
    {
        private readonly HashSet<SKPicture> _pictures = new(ReferenceEqualityComparer<SKPicture>.Instance);
        private readonly HashSet<SKPaint> _paints = new(ReferenceEqualityComparer<SKPaint>.Instance);
        private readonly HashSet<SKPath> _paths = new(ReferenceEqualityComparer<SKPath>.Instance);
        private readonly HashSet<SKImage> _images = new(ReferenceEqualityComparer<SKImage>.Instance);
        private readonly HashSet<ClipPath> _clipPaths = new(ReferenceEqualityComparer<ClipPath>.Instance);
        private readonly HashSet<SKShader> _shaders = new(ReferenceEqualityComparer<SKShader>.Instance);
        private readonly HashSet<SKImageFilter> _imageFilters = new(ReferenceEqualityComparer<SKImageFilter>.Instance);
        private readonly HashSet<SKColorFilter> _colorFilters = new(ReferenceEqualityComparer<SKColorFilter>.Instance);
        private readonly HashSet<SKPathEffect> _pathEffects = new(ReferenceEqualityComparer<SKPathEffect>.Instance);
        private readonly List<TextBlobEntry> _textBlobs = new();

        public void Collect(SKPicture picture)
        {
            if (!_pictures.Add(picture))
            {
                return;
            }

            if (picture.Commands is null)
            {
                return;
            }

            foreach (var command in picture.Commands)
            {
                switch (command)
                {
                    case ClipPathCanvasCommand clipPathCommand:
                        AddClipPath(clipPathCommand.ClipPath);
                        break;
                    case SaveLayerCanvasCommand saveLayerCommand:
                        AddPaint(saveLayerCommand.Paint);
                        break;
                    case DrawImageCanvasCommand drawImageCommand:
                        AddImage(drawImageCommand.Image);
                        AddPaint(drawImageCommand.Paint);
                        break;
                    case DrawPathCanvasCommand drawPathCommand:
                        AddPath(drawPathCommand.Path);
                        AddPaint(drawPathCommand.Paint);
                        break;
                    case DrawTextBlobCanvasCommand drawTextBlobCommand:
                        AddTextBlob(drawTextBlobCommand.TextBlob, drawTextBlobCommand.Paint);
                        break;
                    case DrawTextCanvasCommand drawTextCommand:
                        AddPaint(drawTextCommand.Paint);
                        break;
                    case DrawTextOnPathCanvasCommand drawTextOnPathCommand:
                        AddPath(drawTextOnPathCommand.Path);
                        AddPaint(drawTextOnPathCommand.Paint);
                        break;
                }
            }
        }

        public ModelObjectCollection Freeze()
        {
            return new ModelObjectCollection(
                _paints.ToArray(),
                _paths.ToArray(),
                _images.ToArray(),
                _clipPaths.ToArray(),
                _shaders.ToArray(),
                _imageFilters.ToArray(),
                _colorFilters.ToArray(),
                _pathEffects.ToArray(),
                _textBlobs.ToArray());
        }

        private void AddPaint(SKPaint? paint)
        {
            if (paint is null)
            {
                return;
            }

            if (!_paints.Add(paint))
            {
                return;
            }

            AddShader(paint.Shader);
            AddColorFilter(paint.ColorFilter);
            AddImageFilter(paint.ImageFilter);
            AddPathEffect(paint.PathEffect);
        }

        private void AddPath(SKPath? path)
        {
            if (path is null)
            {
                return;
            }

            _paths.Add(path);
        }

        private void AddImage(SKImage? image)
        {
            if (image is null)
            {
                return;
            }

            _images.Add(image);
        }

        private void AddClipPath(ClipPath? clipPath)
        {
            if (clipPath is null || !_clipPaths.Add(clipPath))
            {
                return;
            }

            if (clipPath.Clips is { })
            {
                foreach (var clip in clipPath.Clips)
                {
                    AddPath(clip.Path);
                    AddClipPath(clip.Clip);
                }
            }

            AddClipPath(clipPath.Clip);
        }

        private void AddTextBlob(SKTextBlob? textBlob, SKPaint? paint)
        {
            if (textBlob is null)
            {
                return;
            }

            AddPaint(paint);
            _textBlobs.Add(new TextBlobEntry(textBlob, paint));
        }

        private void AddShader(SKShader? shader)
        {
            if (shader is null || !_shaders.Add(shader))
            {
                return;
            }

            if (shader is PictureShader pictureShader && pictureShader.Src is { })
            {
                Collect(pictureShader.Src);
            }
        }

        private void AddColorFilter(SKColorFilter? colorFilter)
        {
            if (colorFilter is null)
            {
                return;
            }

            _colorFilters.Add(colorFilter);
        }

        private void AddImageFilter(SKImageFilter? imageFilter)
        {
            if (imageFilter is null || !_imageFilters.Add(imageFilter))
            {
                return;
            }

            switch (imageFilter)
            {
                case ArithmeticImageFilter arithmetic:
                    AddImageFilter(arithmetic.Background);
                    AddImageFilter(arithmetic.Foreground);
                    break;
                case BlendModeImageFilter blendMode:
                    AddImageFilter(blendMode.Background);
                    AddImageFilter(blendMode.Foreground);
                    break;
                case BlurImageFilter blur:
                    AddImageFilter(blur.Input);
                    break;
                case ColorFilterImageFilter colorFilter:
                    AddColorFilter(colorFilter.ColorFilter);
                    AddImageFilter(colorFilter.Input);
                    break;
                case DilateImageFilter dilate:
                    AddImageFilter(dilate.Input);
                    break;
                case DisplacementMapEffectImageFilter displacement:
                    AddImageFilter(displacement.Displacement);
                    AddImageFilter(displacement.Input);
                    break;
                case DistantLitDiffuseImageFilter distantDiffuse:
                    AddImageFilter(distantDiffuse.Input);
                    break;
                case DistantLitSpecularImageFilter distantSpecular:
                    AddImageFilter(distantSpecular.Input);
                    break;
                case ErodeImageFilter erode:
                    AddImageFilter(erode.Input);
                    break;
                case ImageImageFilter image:
                    AddImage(image.Image);
                    break;
                case MatrixConvolutionImageFilter matrix:
                    AddImageFilter(matrix.Input);
                    break;
                case MergeImageFilter merge:
                    if (merge.Filters is { })
                    {
                        foreach (var filter in merge.Filters)
                        {
                            AddImageFilter(filter);
                        }
                    }
                    break;
                case OffsetImageFilter offset:
                    AddImageFilter(offset.Input);
                    break;
                case PaintImageFilter paint:
                    AddPaint(paint.Paint);
                    break;
                case ShaderImageFilter shaderFilter:
                    AddShader(shaderFilter.Shader);
                    break;
                case PictureImageFilter picture:
                    if (picture.Picture is { })
                    {
                        Collect(picture.Picture);
                    }
                    break;
                case PointLitDiffuseImageFilter pointDiffuse:
                    AddImageFilter(pointDiffuse.Input);
                    break;
                case PointLitSpecularImageFilter pointSpecular:
                    AddImageFilter(pointSpecular.Input);
                    break;
                case SpotLitDiffuseImageFilter spotDiffuse:
                    AddImageFilter(spotDiffuse.Input);
                    break;
                case SpotLitSpecularImageFilter spotSpecular:
                    AddImageFilter(spotSpecular.Input);
                    break;
                case TileImageFilter tile:
                    AddImageFilter(tile.Input);
                    break;
            }
        }

        private void AddPathEffect(SKPathEffect? pathEffect)
        {
            if (pathEffect is null)
            {
                return;
            }

            _pathEffects.Add(pathEffect);
        }
    }

    private sealed class ModelObjectCollection
    {
        public ModelObjectCollection(
            SKPaint[] paints,
            SKPath[] paths,
            SKImage[] images,
            ClipPath[] clipPaths,
            SKShader[] shaders,
            SKImageFilter[] imageFilters,
            SKColorFilter[] colorFilters,
            SKPathEffect[] pathEffects,
            TextBlobEntry[] textBlobs)
        {
            Paints = paints;
            Paths = paths;
            Images = images;
            ClipPaths = clipPaths;
            Shaders = shaders;
            ImageFilters = imageFilters;
            ColorFilters = colorFilters;
            PathEffects = pathEffects;
            TextBlobs = textBlobs;
        }

        public SKPaint[] Paints { get; }
        public SKPath[] Paths { get; }
        public SKImage[] Images { get; }
        public ClipPath[] ClipPaths { get; }
        public SKShader[] Shaders { get; }
        public SKImageFilter[] ImageFilters { get; }
        public SKColorFilter[] ColorFilters { get; }
        public SKPathEffect[] PathEffects { get; }
        public TextBlobEntry[] TextBlobs { get; }
    }

    private readonly record struct TextBlobEntry(SKTextBlob? TextBlob, SKPaint? Paint);

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
