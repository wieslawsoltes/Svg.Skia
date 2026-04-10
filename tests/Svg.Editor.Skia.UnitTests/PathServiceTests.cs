using System.Collections.Generic;
using System.Linq;
using Svg;
using Svg.Editor.Skia;
using Svg.Pathing;
using Svg.Skia;
using Xunit;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class PathServiceTests
{
    [Fact]
    public void MakeSmooth_CreatesMoveAndLineSegments()
    {
        var segments = PathService.MakeSmooth(new List<Shim.SKPoint>
        {
            new(0, 0),
            new(5, 5),
            new(10, 0)
        });

        Assert.NotNull(segments);
        Assert.IsType<SvgMoveToSegment>(segments[0]);
        Assert.True(segments.Count >= 2);
    }

    [Fact]
    public void ElementToPath_ConvertsRectangleGeometry()
    {
        var rectangle = new SvgRectangle
        {
            X = 1,
            Y = 2,
            Width = 10,
            Height = 20
        };

        var path = PathService.ElementToPath(rectangle);

        Assert.NotNull(path);
        var svgPath = PathService.ToSvgPathData(path!);
        Assert.Contains("M", svgPath);
        Assert.Contains("L", svgPath);
    }

    [Fact]
    public void Start_UsesRetainedSceneNodeTransformForPathEditing()
    {
        const string svgMarkup = "<svg width=\"64\" height=\"64\"><g transform=\"translate(15,7)\"><path id=\"path1\" d=\"M 0 0 L 10 0\" /></g></svg>";

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var path = Assert.IsType<SvgPath>(svg.SourceDocument!.Children.OfType<SvgGroup>().Single().Children.Single());
        Assert.True(svg.TryGetRetainedSceneNodeById("path1", out var sceneNode));
        Assert.NotNull(sceneNode);

        var service = new PathService();
        service.Start(path, sceneNode!);

        Assert.Same(path, service.EditPath);
        Assert.Same(sceneNode, service.EditSceneNode);
        Assert.Equal(new Shim.SKPoint(15, 7), service.PathMatrix.MapPoint(new Shim.SKPoint(0, 0)));
        Assert.Equal(new Shim.SKPoint(0, 0), service.PathInverse.MapPoint(new Shim.SKPoint(15, 7)));
    }
}
