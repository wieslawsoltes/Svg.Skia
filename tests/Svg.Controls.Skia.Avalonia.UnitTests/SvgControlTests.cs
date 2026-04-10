using System;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgControlTests
{
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
