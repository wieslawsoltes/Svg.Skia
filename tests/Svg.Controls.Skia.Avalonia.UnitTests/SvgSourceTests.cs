using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgSourceTests
{
    private const string SampleSvg = "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";

    [AvaloniaFact]
    public void LoadFromSvg_SetsSvg()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
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
        var command = source.Svg?.Model?.Commands?.OfType<DrawPathCanvasCommand>().FirstOrDefault();
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
}
