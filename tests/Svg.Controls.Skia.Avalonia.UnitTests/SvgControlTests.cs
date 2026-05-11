using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
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

    private const string CurrentColorSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
          <rect x="0" y="0" width="10" height="10" fill="currentColor" />
        </svg>
        """;

    private const string ButtonSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
            <circle cx="50" cy="50" r="44" fill="#3B82F6"/>
        </svg>
        """;

    private const string InteractiveSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
            <rect id="hit" x="0" y="0" width="80" height="80" fill="#3B82F6"/>
        </svg>
        """;

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

    [AvaloniaFact]
    public async Task CurrentColor_ReloadsInlineSource()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
        {
            Source = CurrentColorSvg
        };

        await WaitForSourceAsync(svg);
        var initialPicture = svg.Picture;

        svg.CurrentColor = Color.FromRgb(0, 128, 255);

        await WaitForSourceChangeAsync(svg, initialPicture);

        Assert.Equal(new SKColor(0, 128, 255, 255), GetFirstFillColor(svg.SkSvg));
    }

    [AvaloniaFact]
    public async Task SvgContent_BubblesPointerEventsToParentButton()
    {
        var pressed = 0;
        var released = 0;
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
        {
            Source = ButtonSvg,
            Width = 80,
            Height = 80
        };
        var button = new Button
        {
            Content = svg
        };
        button.AddHandler(
            InputElement.PointerPressedEvent,
            (_, _) => pressed++,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        button.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, _) => released++,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        var window = new Window
        {
            Width = 160,
            Height = 160,
            Content = button
        };

        window.Show();
        try
        {
            await WaitForSourceAsync(svg);

            window.MouseMove(new Point(80, 80), RawInputModifiers.None);
            window.MouseDown(new Point(80, 80), MouseButton.Left, RawInputModifiers.None);
            Assert.True(button.IsPressed);
            window.MouseUp(new Point(80, 80), MouseButton.Left, RawInputModifiers.None);

            Assert.Equal(1, pressed);
            Assert.Equal(1, released);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SvgInteractionCapture_ClearsWhenReleasedOutsideControl()
    {
        var svg = new Svg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
        {
            Source = InteractiveSvg,
            Width = 80,
            Height = 80
        };
        var canvas = new Canvas
        {
            Width = 220,
            Height = 180,
            Children = { svg }
        };
        Canvas.SetLeft(svg, 20);
        Canvas.SetTop(svg, 20);
        var window = new Window
        {
            Width = 220,
            Height = 180,
            Content = canvas
        };

        window.Show();
        try
        {
            await WaitForSourceAsync(svg);

            window.MouseMove(new Point(40, 40), RawInputModifiers.None);
            window.MouseDown(new Point(40, 40), MouseButton.Left, RawInputModifiers.None);
            Assert.NotNull(svg.Interaction.CapturedElement);

            window.MouseUp(new Point(180, 140), MouseButton.Left, RawInputModifiers.None);

            Assert.Null(svg.Interaction.CapturedElement);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SvgInteractionCapture_ClearsWhenPointerCaptureLost()
    {
        var svg = new CaptureLossSvg(new Uri("avares://Svg.Controls.Skia.Avalonia.UnitTests/"))
        {
            Source = InteractiveSvg,
            Width = 80,
            Height = 80
        };
        var window = new Window
        {
            Width = 160,
            Height = 160,
            Content = svg
        };

        window.Show();
        try
        {
            await WaitForSourceAsync(svg);

            window.MouseMove(new Point(40, 40), RawInputModifiers.None);
            window.MouseDown(new Point(40, 40), MouseButton.Left, RawInputModifiers.None);
            Assert.NotNull(svg.Interaction.CapturedElement);
            Assert.NotNull(svg.Interaction.PressedElement);
            Assert.NotNull(svg.PressedPointer);

            svg.PressedPointer!.Capture(null);

            Assert.Null(svg.Interaction.CapturedElement);
            Assert.Null(svg.Interaction.PressedElement);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class CaptureLossSvg(Uri baseUri) : Svg(baseUri)
    {
        public IPointer? PressedPointer { get; private set; }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            PressedPointer = e.Pointer;
        }
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

    private static async Task WaitForSourceChangeAsync(Svg svg, object? previousPicture)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (ReferenceEquals(svg.Picture, previousPicture))
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.NotSame(previousPicture, svg.Picture);
                return;
            }

            await Task.Delay(10);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }
}
