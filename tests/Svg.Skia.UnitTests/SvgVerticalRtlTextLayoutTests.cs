using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using Svg;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgVerticalRtlTextLayoutTests
{
    private static readonly Type s_svgTextLayoutPlannerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLayoutPlanner")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLayoutPlanner.");
    private static readonly Type s_svgTextDirectionType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextDirection")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextDirection.");
    private static readonly Type s_svgUnicodeBidiModeType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgUnicodeBidiMode")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgUnicodeBidiMode.");
    private static readonly Type s_svgTextLayoutFlowType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLayoutFlow")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLayoutFlow.");
    private static readonly Type s_svgTextLineBreakOptionsType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextLineBreakOptions")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextLineBreakOptions.");
    private static readonly Type s_svgTextWrappingOptionsType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextWrappingOptions")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextWrappingOptions.");
    private static readonly Type s_svgTextWrappedLayoutOptionsType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgTextWrappedLayoutOptions")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgTextWrappedLayoutOptions.");

    private static readonly MethodInfo s_createTextLayoutPlanMethod = s_svgTextLayoutPlannerType
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
        {
            if (method.Name != "Create")
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 5 &&
                   parameters[0].ParameterType == typeof(string);
        });

    private static readonly MethodInfo s_createWrappedLayoutMethod = s_svgTextLayoutPlannerType
        .GetMethod("CreateWrappedLayout", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate SvgTextLayoutPlanner.CreateWrappedLayout.");

    [Fact]
    public void CreateWrappedLayout_VerticalRtlMixedDirectionWrapsPerLineInVisualOrder()
    {
        var plan = CreatePlan(
            "A\u05D0\u05D1B\u05D2\u05D3",
            direction: "RightToLeft",
            unicodeBidi: "Embed",
            lineBreakAnywhere: true);
        var clusterAdvances = CreateClusterAdvances(plan, 10f);
        var options = CreateOptions(
            flow: "VerticalRightToLeftColumns",
            origin: new SKPoint(90f, 10f),
            inlineSize: 30f,
            lineAdvance: 12f);

        var layout = CreateWrappedLayout(plan, clusterAdvances, options);
        var lines = GetItems(layout, "Lines");

        Assert.True(lines.Count >= 2, $"Expected vertical RTL text to wrap into multiple columns, but got {lines.Count}.");
        Assert.Equal("VerticalRightToLeftColumns", GetEnumName(lines[0], "Flow"));
        Assert.Equal(-1, GetProperty<int>(lines[0], "InlineProgression"));
        Assert.True(
            GetProperty<SKPoint>(lines[1], "BaselineOrigin").X < GetProperty<SKPoint>(lines[0], "BaselineOrigin").X,
            "Expected vertical-rl columns to progress right-to-left.");

        var firstLinePlacements = GetItems(lines[0], "Placements");
        var firstLineTexts = firstLinePlacements.Select(static placement => GetProperty<string>(placement, "Text")).ToArray();
        Assert.True(
            Array.IndexOf(firstLineTexts, "\u05D0") < Array.IndexOf(firstLineTexts, "A"),
            $"Expected RTL visual run before Latin in the first vertical line, but got {string.Join("|", firstLineTexts)}.");
        Assert.True(
            GetProperty<SKPoint>(firstLinePlacements[1], "Point").Y < GetProperty<SKPoint>(firstLinePlacements[0], "Point").Y,
            "Expected direction=rtl vertical inline progression to place clusters bottom-to-top.");
    }

    [Fact]
    public void CreateWrappedLayout_VerticalRtlTextLengthDistributesTargetAlongColumns()
    {
        var plan = CreatePlan(
            "ABCD",
            direction: "RightToLeft",
            unicodeBidi: "Embed",
            lineBreakAnywhere: true);
        var clusterAdvances = CreateClusterAdvances(plan, 10f);
        var options = CreateOptions(
            flow: "VerticalRightToLeftColumns",
            origin: new SKPoint(90f, 10f),
            inlineSize: 20f,
            lineAdvance: 12f,
            textLength: 80f,
            lengthAdjust: SvgTextLengthAdjust.Spacing);

        var layout = CreateWrappedLayout(plan, clusterAdvances, options);
        var lines = GetItems(layout, "Lines");

        Assert.Equal(80f, GetProperty<float>(layout, "ComputedTextLength"), 3);
        Assert.True(lines.Count >= 2, $"Expected textLength layout to wrap before adjustment, but got {lines.Count} line(s).");

        var firstLinePlacements = GetItems(lines[0], "Placements");
        Assert.Equal(2, firstLinePlacements.Count);
        Assert.Equal(1f, GetProperty<float>(firstLinePlacements[0], "ScaleX"), 3);
        Assert.True(
            GetProperty<SKPoint>(firstLinePlacements[1], "Point").Y < GetProperty<SKPoint>(firstLinePlacements[0], "Point").Y - 20f,
            "Expected vertical textLength spacing to expand the bottom-to-top inline gap.");
    }

    [Fact]
    public void CreateWrappedLayout_VerticalRtlOverflowMarkerUsesInlineEnd()
    {
        var plan = CreatePlan(
            "ABC",
            direction: "RightToLeft",
            unicodeBidi: "Embed",
            whiteSpace: SvgWhiteSpace.NoWrap);
        var clusterAdvances = CreateClusterAdvances(plan, 10f);
        var options = CreateOptions(
            flow: "VerticalRightToLeftColumns",
            origin: new SKPoint(90f, 10f),
            inlineSize: 25f,
            lineAdvance: 12f,
            overflowMarker: "\u2026",
            overflowMarkerAdvance: 5f);

        var layout = CreateWrappedLayout(plan, clusterAdvances, options);
        var line = Assert.Single(GetItems(layout, "Lines"));
        var placements = GetItems(line, "Placements");
        var marker = line.GetType().GetProperty("OverflowMarker")!.GetValue(line);

        Assert.NotNull(marker);
        Assert.Equal(2, placements.Count);
        Assert.Equal("\u2026", GetProperty<string>(marker, "Text"));
        Assert.True(
            GetProperty<SKPoint>(marker, "Point").Y < GetProperty<SKPoint>(placements[0], "Point").Y,
            "Expected the marker to sit at the vertical RTL inline end.");
        Assert.True(
            GetProperty<SKPoint>(marker, "Point").Y >= 10f,
            "Expected the marker to remain inside the vertical inline-size clip span.");
    }

    private static object CreatePlan(
        string text,
        string direction = "LeftToRight",
        string unicodeBidi = "Normal",
        SvgWhiteSpace whiteSpace = SvgWhiteSpace.Normal,
        bool overflowWrapAnywhere = false,
        bool wordBreakBreakAll = false,
        bool wordBreakKeepAll = false,
        bool lineBreakAnywhere = false,
        bool lineBreakLoose = false,
        bool strictLineBreak = false)
    {
        var lineBreakOptions = Activator.CreateInstance(
            s_svgTextLineBreakOptionsType,
            overflowWrapAnywhere,
            wordBreakBreakAll,
            wordBreakKeepAll,
            lineBreakAnywhere,
            lineBreakLoose,
            strictLineBreak)!;
        return s_createTextLayoutPlanMethod.Invoke(
            null,
            [
                text,
                Enum.Parse(s_svgTextDirectionType, direction),
                Enum.Parse(s_svgUnicodeBidiModeType, unicodeBidi),
                whiteSpace,
                lineBreakOptions
            ])!;
    }

    private static object CreateOptions(
        string flow,
        SKPoint origin,
        float inlineSize,
        float lineAdvance,
        float textLength = 0f,
        SvgTextLengthAdjust lengthAdjust = SvgTextLengthAdjust.Spacing,
        string? overflowMarker = null,
        float overflowMarkerAdvance = 0f)
    {
        var wrappingOptions = Activator.CreateInstance(
            s_svgTextWrappingOptionsType,
            32,
            false,
            0.01f)!;
        return Activator.CreateInstance(
            s_svgTextWrappedLayoutOptionsType,
            Enum.Parse(s_svgTextLayoutFlowType, flow),
            origin,
            inlineSize,
            lineAdvance,
            wrappingOptions,
            textLength,
            lengthAdjust,
            overflowMarker,
            overflowMarkerAdvance)!;
    }

    private static object CreateWrappedLayout(object plan, float[] clusterAdvances, object options)
    {
        return s_createWrappedLayoutMethod.Invoke(null, [plan, clusterAdvances, options])!;
    }

    private static float[] CreateClusterAdvances(object plan, float advance)
    {
        return Enumerable.Repeat(advance, GetItems(plan, "Clusters").Count).ToArray();
    }

    private static List<object> GetItems(object source, string propertyName)
    {
        return Assert.IsAssignableFrom<IEnumerable>(source.GetType().GetProperty(propertyName)!.GetValue(source))
            .Cast<object>()
            .ToList();
    }

    private static T GetProperty<T>(object source, string propertyName)
    {
        return Assert.IsType<T>(source.GetType().GetProperty(propertyName)!.GetValue(source));
    }

    private static string GetEnumName(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName)!.GetValue(source)!.ToString()!;
    }
}
