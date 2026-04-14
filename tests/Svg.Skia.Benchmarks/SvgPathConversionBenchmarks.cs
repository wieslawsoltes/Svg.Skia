using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Pathing;

namespace Svg.Skia.Benchmarks;

public class SvgPathConversionBenchmarks
{
    private static readonly Type s_pathingServiceType = typeof(DrawAttributes).Assembly.GetType("Svg.Model.Services.PathingService", throwOnError: true)!;

    private delegate SKPath? PathSegmentListToPathDelegate(SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule);
    private delegate SKPath? PointCollectionToPathDelegate(SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, SKRect viewport);
    private delegate SKPath? RectangleToPathDelegate(SvgRectangle svgRectangle, SvgFillRule svgFillRule, SKRect viewport);
    private delegate SKPath? CircleToPathDelegate(SvgCircle svgCircle, SvgFillRule svgFillRule, SKRect viewport);
    private delegate SKPath? EllipseToPathDelegate(SvgEllipse svgEllipse, SvgFillRule svgFillRule, SKRect viewport);
    private delegate SKPath? LineToPathDelegate(SvgLine svgLine, SvgFillRule svgFillRule, SKRect viewport);

    private sealed record PathConversionWorkItem(Func<SKPath?> Convert);

    private static readonly PathSegmentListToPathDelegate s_pathSegmentListToPath =
        CreateDelegate<PathSegmentListToPathDelegate>("ToPath", typeof(SvgPathSegmentList), typeof(SvgFillRule));

    private static readonly PointCollectionToPathDelegate s_pointCollectionToPath =
        CreateDelegate<PointCollectionToPathDelegate>("ToPath", typeof(SvgPointCollection), typeof(SvgFillRule), typeof(bool), typeof(SKRect));

    private static readonly RectangleToPathDelegate s_rectangleToPath =
        CreateDelegate<RectangleToPathDelegate>("ToPath", typeof(SvgRectangle), typeof(SvgFillRule), typeof(SKRect));

    private static readonly CircleToPathDelegate s_circleToPath =
        CreateDelegate<CircleToPathDelegate>("ToPath", typeof(SvgCircle), typeof(SvgFillRule), typeof(SKRect));

    private static readonly EllipseToPathDelegate s_ellipseToPath =
        CreateDelegate<EllipseToPathDelegate>("ToPath", typeof(SvgEllipse), typeof(SvgFillRule), typeof(SKRect));

    private static readonly LineToPathDelegate s_lineToPath =
        CreateDelegate<LineToPathDelegate>("ToPath", typeof(SvgLine), typeof(SvgFillRule), typeof(SKRect));

    private PathConversionWorkItem[] svgPathItems = Array.Empty<PathConversionWorkItem>();
    private PathConversionWorkItem[] primitiveShapeItems = Array.Empty<PathConversionWorkItem>();

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names
        .Where(static name =>
            name.Contains("shape", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("smooth-path", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("file:", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);
        var viewport = SvgBenchmarkHelpers.GetDocumentViewport(document);

        svgPathItems = document
            .Descendants()
            .OfType<SvgPath>()
            .Where(static svgPath => svgPath.PathData is { Count: > 0 })
            .Select(static svgPath => new PathConversionWorkItem(() => s_pathSegmentListToPath(svgPath.PathData, svgPath.FillRule)))
            .ToArray();

        primitiveShapeItems = document
            .Descendants()
            .SelectMany(element => CreatePrimitiveShapeWorkItems(element, viewport))
            .ToArray();

        if (svgPathItems.Length == 0 && primitiveShapeItems.Length == 0)
        {
            throw new InvalidOperationException($"Scenario '{ScenarioName}' did not contain any direct visual shapes.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Paths", "Conversion", "SvgPath")]
    public int ConvertSvgPathsOnly()
    {
        return ConvertAll(svgPathItems);
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Paths", "Conversion", "Primitive")]
    public int ConvertPrimitiveShapesOnly()
    {
        return ConvertAll(primitiveShapeItems);
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Paths", "Conversion", "All")]
    public int ConvertAllVisualPaths()
    {
        return ConvertAll(svgPathItems) + ConvertAll(primitiveShapeItems);
    }

    private static IEnumerable<PathConversionWorkItem> CreatePrimitiveShapeWorkItems(SvgElement element, SKRect viewport)
    {
        return element switch
        {
            SvgRectangle svgRectangle => [new PathConversionWorkItem(() => s_rectangleToPath(svgRectangle, svgRectangle.FillRule, viewport))],
            SvgCircle svgCircle => [new PathConversionWorkItem(() => s_circleToPath(svgCircle, svgCircle.FillRule, viewport))],
            SvgEllipse svgEllipse => [new PathConversionWorkItem(() => s_ellipseToPath(svgEllipse, svgEllipse.FillRule, viewport))],
            SvgLine svgLine => [new PathConversionWorkItem(() => s_lineToPath(svgLine, svgLine.FillRule, viewport))],
            SvgPolyline svgPolyline when svgPolyline.Points?.Count > 0
                => [new PathConversionWorkItem(() => s_pointCollectionToPath(svgPolyline.Points, svgPolyline.FillRule, false, viewport))],
            SvgPolygon svgPolygon when svgPolygon.Points?.Count > 0
                => [new PathConversionWorkItem(() => s_pointCollectionToPath(svgPolygon.Points, svgPolygon.FillRule, true, viewport))],
            _ => []
        };
    }

    private static int ConvertAll(PathConversionWorkItem[] items)
    {
        var totalCount = 0;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Convert() is { Commands: { } commands })
            {
                totalCount += commands.Count;
            }
        }

        return totalCount;
    }

    private static TDelegate CreateDelegate<TDelegate>(string methodName, params Type[] parameterTypes)
        where TDelegate : Delegate
    {
        var method = s_pathingServiceType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null);
        if (method is null)
        {
            throw new InvalidOperationException($"Could not locate PathingService.{methodName}({string.Join(", ", parameterTypes.Select(static type => type.Name))}).");
        }

        return method.CreateDelegate<TDelegate>();
    }
}
