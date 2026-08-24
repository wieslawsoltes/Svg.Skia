using System;
using System.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Svg;
using Avalonia.Svg.Commands;
using Avalonia.Svg.UnitTests.Views;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgSourceTests
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
    private const string ClipPathSvg = """
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <clipPath id="clip">
              <rect width="10" height="10" />
            </clipPath>
          </defs>
          <rect fill="#F00" width="24" height="24" rx="12" clip-path="url(#clip)" />
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
    public void Exposes_Parameterless_Constructor()
    {
        Assert.NotNull(typeof(SvgSource).GetConstructor(Type.EmptyTypes));
    }

    [AvaloniaFact]
    public void Initialization_Batches_Path_And_Css_Reload()
    {
        var source = new SvgSource(new Uri("avares://Svg.Controls.Avalonia.UnitTests/"));
        var invalidated = 0;
        source.Invalidated += (_, _) => invalidated++;

        source.BeginInit();
        source.Path = "/Assets/Icon.svg";
        source.Css = "#background { fill: #010203; }";

        Assert.Null(source.Picture);
        Assert.Equal(0, invalidated);

        source.EndInit();

        Assert.NotNull(source.Picture);
        Assert.Equal(1, invalidated);

        Assert.Same(source, source.ProvideValue(new EmptyServiceProvider()));
        Assert.Equal(1, invalidated);
    }

    [AvaloniaFact]
    public void RebuildFromModel_RefreshesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        var command = source.Picture?.FindCommands<DrawPathCanvasCommand>().FirstOrDefault();
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
    public void Clone_DeepClonesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var clone = source.Clone();

        Assert.NotSame(source, clone);
        Assert.NotSame(source.Picture, clone.Picture);
    }

    [AvaloniaFact]
    public void Xaml_Resource_Loads_Path_With_Css()
    {
        var view = new StyledSvgSourceView();
        var source = Assert.IsType<SvgSource>(view.Resources["StyledIcon"]);
        var background = source.Picture?
            .FindCommands<DrawPathCanvasCommand>()
            .FirstOrDefault();

        Assert.Equal("/Assets/Icon.svg", source.Path);
        Assert.Equal("#background { fill: #010203; }", source.Css);
        Assert.NotNull(background?.Paint);
        Assert.Equal(new SKColor(1, 2, 3, 255), background!.Paint!.Color);
    }

    [AvaloniaFact]
    public void Xaml_Resource_Uses_Path_As_Content()
    {
        var view = new StyledSvgSourceView();
        var source = Assert.IsType<SvgSource>(view.Resources["ContentIcon"]);

        Assert.Equal("/Assets/Icon.svg", source.Path);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public void Css_Reload_Invalidates_Consuming_Image()
    {
        var view = new StyledSvgSourceView();
        var source = Assert.IsType<SvgSource>(view.Resources["StyledIcon"]);
        var image = new SvgImage { Source = source };
        var invalidated = 0;
        image.Invalidated += (_, _) => invalidated++;

        source.Css = "#background { fill: #040506; }";

        var background = source.Picture?
            .FindCommands<DrawPathCanvasCommand>()
            .FirstOrDefault();
        Assert.Equal(1, invalidated);
        Assert.NotNull(background?.Paint);
        Assert.Equal(new SKColor(4, 5, 6, 255), background!.Paint!.Color);
    }

    [AvaloniaFact]
    public void Load_Path_Preserves_Source_For_Css_Reload()
    {
        var source = SvgSource.Load(
            "/Assets/Icon.svg",
            new Uri("avares://Svg.Controls.Avalonia.UnitTests/"));

        source.Css = "#background { fill: #070809; }";

        var background = source.Picture?
            .FindCommands<DrawPathCanvasCommand>()
            .FirstOrDefault();
        Assert.Equal("/Assets/Icon.svg", source.Path);
        Assert.NotNull(background?.Paint);
        Assert.Equal(new SKColor(7, 8, 9, 255), background!.Paint!.Color);
    }

    [AvaloniaFact]
    public void LoadFromSvg_UsesSvgGlyphPaths()
    {
        var source = SvgSource.LoadFromSvg(SvgFontGlyphSvg);

        Assert.NotNull(source.Picture);
        Assert.NotEmpty(source.Picture!.FindCommands<DrawPathCanvasCommand>());
        Assert.Empty(source.Picture.FindCommands<DrawTextCanvasCommand>());
        Assert.Empty(source.Picture.FindCommands<DrawTextBlobCanvasCommand>());
    }

    [AvaloniaFact]
    public void Record_ConvertsClipPathToGeometryClip()
    {
        var source = SvgSource.LoadFromSvg(ClipPathSvg);

        Assert.NotNull(source.Picture);
        using var picture = AvaloniaPicture.Record(source.Picture!);
        var geometryClip = Assert.Single(picture.Commands.OfType<GeometryClipDrawCommand>());

        Assert.NotNull(geometryClip.Clip);
        Assert.Equal(new Rect(0, 0, 10, 10), geometryClip.Clip.Bounds);
        Assert.Contains(picture.Commands, command => command is RectangleDrawCommand);
    }
}
