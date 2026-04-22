using System;
using System.Linq;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgControlTests
{
    private const string CurrentColorSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
          <rect x="0" y="0" width="10" height="10" fill="currentColor" />
        </svg>
        """;

    [AvaloniaFact]
    public void AnimationPlaybackRate_NormalizesNonFiniteValues()
    {
        var svg = new Svg(new System.Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        svg.AnimationPlaybackRate = double.NaN;
        Assert.Equal(0d, svg.AnimationPlaybackRate);

        svg.AnimationPlaybackRate = double.PositiveInfinity;
        Assert.Equal(0d, svg.AnimationPlaybackRate);

        svg.AnimationPlaybackRate = -1d;
        Assert.Equal(0d, svg.AnimationPlaybackRate);

        svg.AnimationPlaybackRate = 1.5d;
        Assert.Equal(1.5d, svg.AnimationPlaybackRate);
    }

    [AvaloniaFact]
    public void StaleAnimationFrameCallback_DoesNotJoinCurrentLoop()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        SetPrivateField(svg, "_animationRenderLoopActive", true);
        SetPrivateField(svg, "_animationRenderLoopRequested", true);
        SetPrivateField(svg, "_animationRenderLoopGeneration", 2L);
        SetPrivateField(
            svg,
            "_animationBackendResolution",
            new SvgAnimationHostBackendResolution(
                SvgAnimationHostBackend.Default,
                SvgAnimationHostBackend.RenderLoop,
                null));

        InvokeAnimationFrameCallback(svg, generation: 1L);

        Assert.True((bool)GetPrivateField(svg, "_animationRenderLoopRequested"));
    }

    [AvaloniaFact]
    public void CurrentAnimationFrameCallback_AdvancesCurrentLoopState()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        SetPrivateField(svg, "_animationRenderLoopActive", true);
        SetPrivateField(svg, "_animationRenderLoopRequested", true);
        SetPrivateField(svg, "_animationRenderLoopGeneration", 3L);
        SetPrivateField(
            svg,
            "_animationBackendResolution",
            new SvgAnimationHostBackendResolution(
                SvgAnimationHostBackend.Default,
                SvgAnimationHostBackend.RenderLoop,
                null));

        InvokeAnimationFrameCallback(svg, generation: 3L);

        Assert.False((bool)GetPrivateField(svg, "_animationRenderLoopRequested"));
    }

    [AvaloniaFact]
    public void CurrentColor_ReloadsInlineSource()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
        {
            Source = CurrentColorSvg
        };

        svg.CurrentColor = Color.FromRgb(0, 128, 255);

        Assert.Equal(new SKColor(0, 128, 255, 255), GetFirstFillColor(svg.SkSvg));
    }

    private static void InvokeAnimationFrameCallback(Svg svg, long generation)
    {
        var callback = typeof(Svg).GetMethod(
            "OnAnimationFrameRequested",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(TimeSpan), typeof(long) },
            null);

        Assert.NotNull(callback);
        callback.Invoke(svg, new object[] { TimeSpan.Zero, generation });
    }

    private static SKColor GetFirstFillColor(SKSvg? svg)
    {
        var command = svg?.Model?
            .FindCommands<DrawPathCanvasCommand>()
            .FirstOrDefault(x => x.Paint?.Style == SKPaintStyle.Fill);

        Assert.NotNull(command?.Paint?.Color);
        return command!.Paint!.Color!.Value;
    }

    private static object GetPrivateField(Svg svg, string fieldName)
    {
        var field = typeof(Svg).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(svg)!;
    }

    private static void SetPrivateField(Svg svg, string fieldName, object value)
    {
        var field = typeof(Svg).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(svg, value);
    }
}
