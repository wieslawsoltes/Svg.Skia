using System;
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
    public void LoadFromSvg_UsesSvgFontsByDefault()
    {
        using var source = SvgSource.LoadFromSvg(SvgFontGlyphSvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
        Assert.True(source.Svg!.Settings.EnableSvgFonts);
        Assert.NotEmpty(source.Svg.Model!.FindCommands<DrawPathCanvasCommand>());
        Assert.Empty(source.Svg.Model.FindCommands<DrawTextCanvasCommand>());
        Assert.Empty(source.Svg.Model.FindCommands<DrawTextBlobCanvasCommand>());
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
    public void ReLoad_DuringRender_DefersPreviousResourceDisposal()
    {
        using var source = SvgSource.LoadFromSvg(SampleSvg, new SvgParameters(null, " "));
        var originalPicture = source.Picture;

        Assert.NotNull(originalPicture);
        Assert.True(BeginRender(source));

        try
        {
            source.ReLoad(new SvgParameters(null, "rect { fill: blue; }"));

            Assert.NotSame(originalPicture, source.Picture);
            Assert.NotEqual(IntPtr.Zero, originalPicture.Handle);
        }
        finally
        {
            EndRender(source);
        }

        Assert.Equal(IntPtr.Zero, originalPicture.Handle);
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

        var task = Task.Run(() =>
        {
            if (!BeginRender(source))
            {
                return false;
            }

            source.Dispose();
            EndRender(source);

            return source.Svg is null && source.Picture is null;
        });

        Assert.True(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(task.Result);
    }

    private static bool BeginRender(SvgSource source)
    {
        var beginRender = typeof(SvgSource).GetMethod("BeginRender", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(beginRender);
        return (bool)(beginRender.Invoke(source, null) ?? false);
    }

    private static void EndRender(SvgSource source)
    {
        var endRender = typeof(SvgSource).GetMethod("EndRender", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(endRender);
        endRender.Invoke(source, null);
    }
}
