using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgSourceTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
    private const string PercentageOpacitySvg = """
        <svg xmlns="http://www.w3.org/2000/svg" height="40" width="40" viewBox="0 0 24 24" fill="#fff" x="0" y="0" opacity="100%">
          <path d="m19 1-5 5v11l5-4.5V1zM1 6v14.65c0 .25.25.5.5.5.1 0 .15-.05.25-.05C3.1 20.45 5.05 20 6.5 20c1.95 0 4.05.4 5.5 1.5V6c-1.45-1.1-3.55-1.5-5.5-1.5S2.45 4.9 1 6zm22 13.5V6c-.6-.45-1.25-.75-2-1v13.5c-1.1-.35-2.3-.5-3.5-.5-1.7 0-4.15.65-5.5 1.5v2c1.35-.85 3.8-1.5 5.5-1.5 1.65 0 3.35.3 4.75 1.05.1.05.15.05.25.05.25 0 .5-.25.5-.5v-1.1z"/>
        </svg>
        """;
    private const string JavaScriptMutationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
          <rect id="target" width="10" height="10" fill="red" />
          <script><![CDATA[
            document.getElementById('target').setAttribute('fill', 'green');
          ]]></script>
        </svg>
        """;
    private const string SvgFontGlyphSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
          <defs>
            <style type="text/css"><![CDATA[
              @font-face {
                font-family: 'DefaultFont';
                src: url('#DefaultFontFace') format('svg');
              }
            ]]></style>
            <font id="DefaultFontFace" horiz-adv-x="100">
              <font-face font-family="DefaultFont" units-per-em="100" ascent="100" descent="0" />
              <glyph unicode="A" horiz-adv-x="100" d="M10 0H30V100H10Z" />
            </font>
          </defs>
          <text x="10" y="110" fill="black" font-family="DefaultFont" font-size="100">A</text>
        </svg>
        """;

    [AvaloniaFact]
    public void LoadFromSvg_SetsSvg()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public void LoadFromSvg_WithPercentageOpacity_DoesNotThrow()
    {
        using var source = SvgSource.LoadFromSvg(PercentageOpacitySvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public void LoadFromSvg_UsesSvgFontsByDefault()
    {
        using var source = SvgSource.LoadFromSvg(SvgFontGlyphSvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
        Assert.True(source.Svg!.Settings.EnableSvgFonts);
        Assert.NotEmpty(source.Svg.Model!.FindCommands<DrawPathCanvasCommand>());
        Assert.Empty(source.Svg.Model!.FindCommands<DrawTextCanvasCommand>());
        Assert.Empty(source.Svg.Model!.FindCommands<DrawTextBlobCanvasCommand>());
    }

    [AvaloniaFact]
    public void LoadFromSvg_AppliesSharedSkiaModelJavaScriptSettings()
    {
        WithSharedJavaScriptEnabled(() =>
        {
            using var source = SvgSource.LoadFromSvg(JavaScriptMutationSvg);

            Assert.True(source.Svg!.Settings.EnableJavaScript);
            AssertTargetFill(source, "green");
        });
    }

    [AvaloniaFact]
    public void Load_AppliesSharedSkiaModelJavaScriptSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, JavaScriptMutationSvg);
        try
        {
            WithSharedJavaScriptEnabled(() =>
            {
                using var source = SvgSource.Load(path);

                Assert.True(source.Svg!.Settings.EnableJavaScript);
                AssertTargetFill(source, "green");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void LoadFromSvgDocument_SetsSvg()
    {
        var document = SvgService.FromSvg(SampleSvg);
        Assert.NotNull(document);

        var source = SvgSource.LoadFromSvgDocument(document!);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public void LoadFromSvgDocument_AppliesSharedSkiaModelJavaScriptSettings()
    {
        var document = SvgService.FromSvg(JavaScriptMutationSvg);
        Assert.NotNull(document);

        WithSharedJavaScriptEnabled(() =>
        {
            using var source = SvgSource.LoadFromSvgDocument(document!);

            Assert.True(source.Svg!.Settings.EnableJavaScript);
            AssertTargetFill(source, "green");
        });
    }

    [AvaloniaFact]
    public void RebuildFromModel_RefreshesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        var command = source.Svg?.Model?.FindCommands<DrawPathCanvasCommand>().FirstOrDefault();
        Assert.NotNull(command);

        if (command?.Paint is { } paint)
        {
            paint.Color = new SKColor(0, 0, 0, 255);
        }

        source.RebuildFromModel();

        Assert.NotNull(source.Picture);
        Assert.NotSame(original, source.Picture);
    }

    [AvaloniaFact]
    public void Picture_TracksSkSvgRebuilds()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        var command = source.Svg?.Model?.FindCommands<DrawPathCanvasCommand>().FirstOrDefault();
        Assert.NotNull(command);

        if (command?.Paint is { } paint)
        {
            paint.Color = new SKColor(0, 255, 0, 255);
        }

        var rebuilt = source.Svg?.RebuildFromModel();

        Assert.NotNull(rebuilt);
        Assert.Same(rebuilt, source.Picture);
        Assert.NotSame(original, source.Picture);
    }

    [AvaloniaFact]
    public void LoadFromSvg_ReLoad_PreservesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);

        source.ReLoad(new SvgParameters(null, ".Black { fill: #000000; }"));

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public void Clone_DeepClonesModel()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var clone = source.Clone();

        Assert.NotSame(source, clone);
        Assert.NotNull(source.Svg);
        Assert.NotNull(clone.Svg);
        Assert.NotSame(source.Svg, clone.Svg);
        Assert.NotSame(source.Svg?.Model, clone.Svg?.Model);
        Assert.NotSame(source.Picture, clone.Picture);
    }

    [AvaloniaFact]
    public void Dispose_DuringRender_DoesNotDeadlock()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var beginRender = typeof(SvgSource).GetMethod("BeginRender", BindingFlags.Instance | BindingFlags.NonPublic);
        var endRender = typeof(SvgSource).GetMethod("EndRender", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(beginRender);
        Assert.NotNull(endRender);

        var task = Task.Run(() =>
        {
            var started = (bool)(beginRender!.Invoke(source, null) ?? false);
            if (!started)
            {
                return false;
            }

            source.Dispose();
            endRender!.Invoke(source, null);

            return source.Svg is null && source.Picture is null;
        });

        Assert.True(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(task.Result);
    }

    private static void WithSharedJavaScriptEnabled(Action action)
    {
        var settings = SvgSource.s_skiaModel.Settings;
        var oldEnableJavaScript = settings.EnableJavaScript;
        var oldThrowOnJavaScriptError = settings.ThrowOnJavaScriptError;
        try
        {
            settings.EnableJavaScript = true;
            settings.ThrowOnJavaScriptError = true;
            action();
        }
        finally
        {
            settings.EnableJavaScript = oldEnableJavaScript;
            settings.ThrowOnJavaScriptError = oldThrowOnJavaScriptError;
        }
    }

    private static void AssertTargetFill(SvgSource source, string expected)
    {
        var target = source.Svg?.SourceDocument?.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetAttribute("fill", out var fill));
        Assert.Equal(expected, fill, ignoreCase: true);
    }
}
