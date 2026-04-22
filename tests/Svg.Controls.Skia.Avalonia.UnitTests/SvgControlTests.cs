using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Svg.Model;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgControlTests
{
    private const string SampleSvg =
        "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";

    private const string ReplacementSvg =
        "<svg width=\"20\" height=\"12\"><rect x=\"0\" y=\"0\" width=\"20\" height=\"12\" fill=\"blue\" /></svg>";

    [AvaloniaFact]
    public async Task Source_LoadsInlineSvg()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        svg.Source = SampleSvg;

        await WaitForSourceAsync(svg);

        Assert.NotNull(svg.SkSvg);
        Assert.NotNull(svg.Picture);
        Assert.Equal(10, svg.Picture!.CullRect.Width);
        Assert.Equal(10, svg.Picture.CullRect.Height);
    }

    [AvaloniaFact]
    public async Task Source_UsesMostRecentInlineSvg()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        svg.Source = SampleSvg;
        svg.Source = ReplacementSvg;

        await WaitForSourceAsync(svg);

        Assert.NotNull(svg.Picture);
        Assert.Equal(20, svg.Picture!.CullRect.Width);
        Assert.Equal(12, svg.Picture.CullRect.Height);
    }

    [AvaloniaFact]
    public async Task Source_AppliesCurrentRenderOptionsWhenLoadCompletes()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"));

        svg.Source = SampleSvg;
        svg.Wireframe = true;
        svg.DisableFilters = true;

        await WaitForSourceAsync(svg);

        Assert.NotNull(svg.SkSvg);
        Assert.True(svg.SkSvg!.Wireframe);
        Assert.Equal(DrawAttributes.Filter, svg.SkSvg.IgnoreAttributes);
    }

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

    private static async Task WaitForSourceAsync(Svg svg)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (svg.Picture is null)
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.NotNull(svg.Picture);
                return;
            }

            await Task.Delay(10);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }
}
