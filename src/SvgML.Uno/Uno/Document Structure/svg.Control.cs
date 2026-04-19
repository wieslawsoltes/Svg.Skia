using System.Numerics;
using System.Text;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;
using Windows.Foundation;
using ShimPoint = ShimSkiaSharp.SKPoint;

namespace SvgML;

public partial class svg
{
    private SKPicture? _picture;
    private SKSvg? _skSvg;
    private bool _reloadQueued;

    static svg()
    {
        Initialize();
    }

    public svg()
    {
        AttachToTree(parent: null, root: this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SKPicture? Picture => _picture;

    public SKSvg? SkSvg => _skSvg;

    private static void Initialize()
    {
    }

    internal void InvalidateSvgTree()
    {
        QueueReload();
        InvalidateMeasure();
        InvalidateArrange();
    }

    public bool TryGetPicturePoint(Point point, out ShimPoint picturePoint)
    {
        picturePoint = default;

        if (!TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        if (!renderInfo.TryMapToPicture(new SvgPoint(point.X, point.Y), out var mappedPoint))
        {
            return false;
        }

        picturePoint = new ShimPoint((float)mappedPoint.X, (float)mappedPoint.Y);
        return true;
    }

    public IEnumerable<SvgElement> HitTestElements(Point point)
    {
        if (_skSvg is { } skSvg && TryGetPicturePoint(point, out var picturePoint))
        {
            return skSvg.HitTestElements(picturePoint);
        }

        return Array.Empty<SvgElement>();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = _picture;
        if (picture is null)
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(availableSize.Width, availableSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picture = _picture;
        if (picture is null)
        {
            return default;
        }

        var size = SvgRenderLayout.CalculateSize(
            new SvgSize(finalSize.Width, finalSize.Height),
            new SvgSize(picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection);

        return new Size(size.Width, size.Height);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var picture = _picture;
        if (picture is null)
        {
            return;
        }

        if (!SvgRenderLayout.TryCreateRenderInfo(
                new SvgSize(area.Width, area.Height),
                new SvgRect(
                    picture.CullRect.Left,
                    picture.CullRect.Top,
                    picture.CullRect.Width,
                    picture.CullRect.Height),
                Stretch,
                StretchDirection,
                out var renderInfo))
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(ToSKRect(renderInfo.DestinationRect));
        var matrix = ToSKMatrix(renderInfo.Matrix);
        canvas.Concat(in matrix);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private void QueueReload()
    {
        if (_reloadQueued)
        {
            return;
        }

        _reloadQueued = true;

        if (DispatcherQueue is { } dispatcherQueue)
        {
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                _reloadQueued = false;
                ReloadFromInlineTree();
                Invalidate();
            });

            return;
        }

        _reloadQueued = false;
        ReloadFromInlineTree();
        Invalidate();
    }

    private void ReloadFromInlineTree()
    {
        try
        {
            var markup = ToSvgString(this);
            using var stream = GenerateStreamFromString(markup);
            _ = LoadFromStream(stream, BuildParameters(Css, CurrentCss));
        }
        catch
        {
            ClearLoadedData();
        }
    }

    private static Stream GenerateStreamFromString(string value)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static SvgParameters? BuildParameters(string? css, string? currentCss)
    {
        var combinedCss = CombineCss(css, currentCss);
        return string.IsNullOrWhiteSpace(combinedCss) ? null : new SvgParameters(null, combinedCss);
    }

    private static string? CombineCss(params string?[] values)
    {
        var filtered = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        return filtered.Length == 0 ? null : string.Join(" ", filtered);
    }

    private bool LoadFromStream(Stream stream, SvgParameters? parameters)
    {
        var skSvg = new SKSvg();
        var picture = skSvg.Load(stream, parameters);
        if (picture is null)
        {
            skSvg.Dispose();
            ClearLoadedData();
            return false;
        }

        SKSvg? previousSvg;

        lock (this)
        {
            previousSvg = _skSvg;
            _skSvg = skSvg;
            _picture = picture;
        }

        previousSvg?.Dispose();
        return true;
    }

    private void ClearLoadedData()
    {
        SKSvg? previousSvg;

        lock (this)
        {
            previousSvg = _skSvg;
            _skSvg = null;
            _picture = null;
        }

        previousSvg?.Dispose();
    }

    private bool TryGetRenderInfo(out SvgRenderInfo renderInfo)
    {
        renderInfo = default;

        var picture = _picture;
        if (picture is null)
        {
            return false;
        }

        return SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(ActualWidth, ActualHeight),
            new SvgRect(picture.CullRect.Left, picture.CullRect.Top, picture.CullRect.Width, picture.CullRect.Height),
            Stretch,
            StretchDirection,
            out renderInfo);
    }

    private static SKRect ToSKRect(SvgRect rect)
    {
        return new SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    }

    private static SKMatrix ToSKMatrix(Matrix3x2 matrix)
    {
        return new SKMatrix
        {
            ScaleX = matrix.M11,
            SkewX = matrix.M21,
            TransX = matrix.M31,
            SkewY = matrix.M12,
            ScaleY = matrix.M22,
            TransY = matrix.M32,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateSvgTree();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _reloadQueued = false;
    }

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((svg)d).InvalidateSvgTree();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (svg)d;
        control.InvalidateMeasure();
        control.InvalidateArrange();
        control.Invalidate();
    }
}
