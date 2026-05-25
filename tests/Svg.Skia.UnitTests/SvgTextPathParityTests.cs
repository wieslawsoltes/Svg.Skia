using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgTextPathParityTests
{
    private static readonly Type s_svgTextPathLayoutPlannerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextPathLayoutPlanner")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextPathLayoutPlanner.");
    private static readonly Type s_pathSampleType =
        s_svgTextPathLayoutPlannerType.GetNestedType("PathSample", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.PathSample.");
    private static readonly Type s_mappingOptionsType =
        s_svgTextPathLayoutPlannerType.GetNestedType("MappingOptions", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.MappingOptions.");
    private static readonly Type s_linePlacementInputType =
        s_svgTextPathLayoutPlannerType.GetNestedType("LinePlacementInput", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.LinePlacementInput.");
    private static readonly MethodInfo s_tryGetCurrentPositionMethod =
        s_svgTextPathLayoutPlannerType.GetMethod("TryGetCurrentPosition", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.TryGetCurrentPosition.");
    private static readonly MethodInfo s_tryCreateLinePlacementPlanMethod =
        s_svgTextPathLayoutPlannerType.GetMethod("TryCreateLinePlacementPlan", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.TryCreateLinePlacementPlan.");
    private static readonly MethodInfo s_tryCreateFallbackStretchTextClusterRangesMethod =
        s_svgTextPathLayoutPlannerType.GetMethod("TryCreateFallbackStretchTextClusterRanges", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.TryCreateFallbackStretchTextClusterRanges.");
    private static readonly MethodInfo s_tryWarpTextOutlinePathMethod =
        s_svgTextPathLayoutPlannerType.GetMethod("TryWarpTextOutlinePath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.TryWarpTextOutlinePath.");
    private static readonly MethodInfo s_tryWarpTextOutlinePathOrFallbackMethod =
        s_svgTextPathLayoutPlannerType.GetMethod("TryWarpTextOutlinePathOrFallback", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextPathLayoutPlanner.TryWarpTextOutlinePathOrFallback.");

    [Fact]
    public void TryGetCurrentPosition_TinyScaledPathPreservesCurrentPositionTangent()
    {
        var samples = CreatePathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(0.000001f, 0f), 100f, false, false));

        var succeeded = InvokeTryGetCurrentPosition(samples, 50f, 2f, isClosedLoop: false, out var point, out var tangent);

        Assert.True(succeeded);
        Assert.Equal(0.0000005f, point.X, 9);
        Assert.Equal(2f, point.Y, 6);
        Assert.Equal(1f, tangent.X, 6);
        Assert.Equal(0f, tangent.Y, 6);
    }

    [Fact]
    public void TryCreateLinePlacementPlan_OffsetsWrappedVerticalLinesFromSamePath()
    {
        var samples = CreatePathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(100f, 0f), 100f, false, false));
        var options = CreateMappingOptions(100f, isClosedLoop: false, startOffset: 0f, baseVOffset: 1f);
        var lines = CreateLinePlacementInputs(
            (0f, 0f, 20f),
            (12f, 0f, 20f),
            (24f, 0f, 20f));

        var succeeded = InvokeTryCreateLinePlacementPlan(
            lines,
            samples,
            options,
            isVertical: true,
            lineAdvance: 5f,
            out var plan);

        Assert.True(succeeded);
        var placements = GetItems(plan!, "Placements");
        Assert.Equal(3, placements.Count);

        var firstPoint = GetProperty<SKPoint>(placements[0], "Point");
        var secondPoint = GetProperty<SKPoint>(placements[1], "Point");
        var thirdPoint = GetProperty<SKPoint>(placements[2], "Point");
        Assert.Equal(0f, firstPoint.X, 6);
        Assert.Equal(1f, firstPoint.Y, 6);
        Assert.Equal(12f, secondPoint.X, 6);
        Assert.Equal(6f, secondPoint.Y, 6);
        Assert.Equal(24f, thirdPoint.X, 6);
        Assert.Equal(11f, thirdPoint.Y, 6);
        Assert.All(placements, placement => Assert.Equal(0f, GetProperty<float>(placement, "RotationDegrees"), 6));
    }

    [Fact]
    public void TryCreateFallbackStretchTextClusterRanges_KeepsCombiningClusterTogether()
    {
        var succeeded = InvokeTryCreateFallbackStretchTextClusterRanges("A\u0301B", out var ranges);

        Assert.True(succeeded);
        Assert.Equal(2, ranges.Count);
        Assert.Equal(0, GetProperty<int>(ranges[0], "Start"));
        Assert.Equal(2, GetProperty<int>(ranges[0], "End"));
        Assert.Equal(2, GetProperty<int>(ranges[1], "Start"));
        Assert.Equal(3, GetProperty<int>(ranges[1], "End"));
    }

    [Fact]
    public void TryWarpTextOutlinePath_SupportsEllipseGlyphCommands()
    {
        var samples = CreatePathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(100f, 0f), 100f, false, false));
        var options = CreateMappingOptions(100f, isClosedLoop: false, startOffset: 0f, baseVOffset: 0f);
        var glyphPath = new SKPath();
        glyphPath.AddCircle(12f, -4f, 4f);

        var succeeded = InvokeTryWarpTextOutlinePath(glyphPath, samples, options, out var stretchedPath);

        Assert.True(succeeded);
        Assert.False(stretchedPath.IsEmpty);
        Assert.True(stretchedPath.Commands?.Count > 4);
        Assert.True(stretchedPath.Bounds.Width > 6f);
        Assert.True(stretchedPath.Bounds.Height > 6f);
    }

    [Fact]
    public void TryWarpTextOutlinePathOrFallback_UsesBoundsForUnsupportedArcGlyphCommands()
    {
        var samples = CreatePathSamples(
            (new SKPoint(0f, 0f), 0f, true, false),
            (new SKPoint(100f, 0f), 100f, false, false));
        var options = CreateMappingOptions(100f, isClosedLoop: false, startOffset: 0f, baseVOffset: 0f);
        var glyphPath = new SKPath();
        glyphPath.MoveTo(4f, -2f);
        glyphPath.ArcTo(6f, 6f, 0f, SKPathArcSize.Small, SKPathDirection.Clockwise, 20f, -2f);

        var succeeded = InvokeTryWarpTextOutlinePathOrFallback(glyphPath, glyphPath.Bounds, samples, options, out var stretchedPath);

        Assert.True(succeeded);
        Assert.False(stretchedPath.IsEmpty);
        Assert.True(stretchedPath.Bounds.Width >= glyphPath.Bounds.Width - 0.1f);
    }

    private static Array CreatePathSamples(params (SKPoint Point, float Distance, bool StartsSubpath, bool ClosesSubpath)[] samples)
    {
        var result = Array.CreateInstance(s_pathSampleType, samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            result.SetValue(
                CreateInstance(
                    s_pathSampleType,
                    samples[i].Point,
                    samples[i].Distance,
                    samples[i].StartsSubpath,
                    samples[i].ClosesSubpath),
                i);
        }

        return result;
    }

    private static Array CreateLinePlacementInputs(params (float InlineOffset, float BaseVOffset, float Advance)[] lines)
    {
        var result = Array.CreateInstance(s_linePlacementInputType, lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            result.SetValue(CreateInstance(s_linePlacementInputType, lines[i].InlineOffset, lines[i].BaseVOffset, lines[i].Advance), i);
        }

        return result;
    }

    private static object CreateMappingOptions(float pathLength, bool isClosedLoop, float startOffset, float baseVOffset)
        => CreateInstance(s_mappingOptionsType, pathLength, isClosedLoop, startOffset, baseVOffset);

    private static object CreateInstance(Type type, params object?[] args)
    {
        var constructor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == args.Length);
        return constructor.Invoke(args);
    }

    private static bool InvokeTryGetCurrentPosition(
        Array samples,
        float distance,
        float vOffset,
        bool isClosedLoop,
        out SKPoint point,
        out SKPoint tangent)
    {
        object?[] args = [samples, distance, vOffset, isClosedLoop, null, null];
        var succeeded = Assert.IsType<bool>(s_tryGetCurrentPositionMethod.Invoke(null, args));
        point = Assert.IsType<SKPoint>(args[4]);
        tangent = Assert.IsType<SKPoint>(args[5]);
        return succeeded;
    }

    private static bool InvokeTryCreateLinePlacementPlan(
        Array lines,
        Array samples,
        object options,
        bool isVertical,
        float lineAdvance,
        out object? plan)
    {
        object?[] args = [lines, samples, options, isVertical, lineAdvance, null];
        var succeeded = Assert.IsType<bool>(s_tryCreateLinePlacementPlanMethod.Invoke(null, args));
        plan = args[5];
        return succeeded;
    }

    private static bool InvokeTryCreateFallbackStretchTextClusterRanges(string text, out List<object> ranges)
    {
        object?[] args = [text, null];
        var succeeded = Assert.IsType<bool>(s_tryCreateFallbackStretchTextClusterRangesMethod.Invoke(null, args));
        ranges = args[1] is IEnumerable enumerable ? enumerable.Cast<object>().ToList() : new List<object>();
        return succeeded;
    }

    private static bool InvokeTryWarpTextOutlinePath(
        SKPath glyphPath,
        Array samples,
        object options,
        out SKPath stretchedPath)
    {
        object?[] args = [glyphPath, samples, options, null];
        var succeeded = Assert.IsType<bool>(s_tryWarpTextOutlinePathMethod.Invoke(null, args));
        stretchedPath = Assert.IsType<SKPath>(args[3]);
        return succeeded;
    }

    private static bool InvokeTryWarpTextOutlinePathOrFallback(
        SKPath glyphPath,
        SKRect fallbackBounds,
        Array samples,
        object options,
        out SKPath stretchedPath)
    {
        object?[] args = [glyphPath, fallbackBounds, samples, options, null];
        var succeeded = Assert.IsType<bool>(s_tryWarpTextOutlinePathOrFallbackMethod.Invoke(null, args));
        stretchedPath = Assert.IsType<SKPath>(args[4]);
        return succeeded;
    }

    private static List<object> GetItems(object value, string propertyName)
    {
        var propertyValue = value.GetType().GetProperty(propertyName)!.GetValue(value);
        return Assert.IsAssignableFrom<IEnumerable>(propertyValue).Cast<object>().ToList();
    }

    private static T GetProperty<T>(object value, string propertyName)
    {
        return Assert.IsType<T>(value.GetType().GetProperty(propertyName)!.GetValue(value));
    }
}
