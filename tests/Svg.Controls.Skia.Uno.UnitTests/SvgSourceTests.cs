using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Uno.Svg.Skia.UnitTests;

public class SvgSourceTests
{
    private const string SampleSvg =
        "<svg width=\"10\" height=\"10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" class=\"accent\" fill=\"red\" /></svg>";

    private const string AnimatedSampleSvg =
        """
        <svg width="100" height="20" viewBox="0 0 100 20" xmlns="http://www.w3.org/2000/svg">
          <rect x="0" y="0" width="10" height="10" fill="red">
            <animate attributeName="x" from="0" to="50" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    [Fact]
    public void LoadFromSvg_SetsSvg()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [Fact]
    public void LoadFromSvgDocument_SetsSvg()
    {
        var document = SvgService.FromSvg(SampleSvg);

        Assert.NotNull(document);

        var source = SvgSource.LoadFromSvgDocument(document!);

        Assert.NotNull(source.Svg);
        Assert.NotNull(source.Picture);
    }

    [Fact]
    public void RebuildFromModel_RefreshesPicture()
    {
        var source = SvgSource.LoadFromSvg(SampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);

        source.RebuildFromModel();

        Assert.NotNull(source.Picture);
        Assert.NotSame(original, source.Picture);
    }

    [Fact]
    public void AnimatedSource_PictureGetterTracksLiveSkSvgPicture()
    {
        var source = SvgSource.LoadFromSvg(AnimatedSampleSvg);
        var original = source.Picture;

        Assert.NotNull(original);
        Assert.NotNull(source.Svg);

        source.Svg!.SetAnimationTime(TimeSpan.FromMilliseconds(500));

        var updated = source.Picture;

        Assert.NotNull(updated);
        Assert.NotSame(original, updated);
        _ = updated!.CullRect;
    }

    [Fact]
    public async Task LoadAsync_FilePath_SetsSvg()
    {
        var filePath = CreateTempSvgFile();

        try
        {
            var source = await SvgSource.LoadAsync(filePath);

            Assert.NotNull(source.Svg);
            Assert.NotNull(source.Picture);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ReLoadAsync_PathBackedSource_PreservesPicture()
    {
        var filePath = CreateTempSvgFile();

        try
        {
            var source = await SvgSource.LoadAsync(filePath);

            await source.ReLoadAsync(new SvgParameters(null, ".accent { fill: #000000; }"));

            Assert.NotNull(source.Svg);
            Assert.NotNull(source.Picture);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SvgSource.LoadAsync("/Assets/does-not-matter.svg", cancellationToken: cts.Token));
    }

    [Fact]
    public void NormalizePath_LeadingSlash_UsesMsAppxUri()
    {
        var uri = SvgSource.NormalizePath("/Assets/Icon.svg");

        Assert.Equal("ms-appx:///Assets/Icon.svg", uri.ToString());
    }

    [Fact]
    public void NormalizePath_RelativePath_UsesBaseUri()
    {
        var uri = SvgSource.NormalizePath("Assets/Icon.svg", new Uri("ms-appx:///Samples/"));

        Assert.Equal("ms-appx:///Samples/Assets/Icon.svg", uri.ToString());
    }

    [Fact]
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

    [Fact]
    public async Task Dispose_DuringRender_DoesNotDeadlock()
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

        var completed = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(completed);
    }

    [Fact]
    public void CacheKey_ChangesWhenCssOrEntitiesChange()
    {
        var path = "ms-appx:///Assets/Icon.svg";
        var first = SvgCacheKey.Create(path, new SvgParameters(
            new Dictionary<string, string> { ["accent"] = "#ff0000" },
            ".accent { fill: red; }"));
        var second = SvgCacheKey.Create(path, new SvgParameters(
            new Dictionary<string, string> { ["accent"] = "#0000ff" },
            ".accent { fill: red; }"));
        var third = SvgCacheKey.Create(path, new SvgParameters(
            new Dictionary<string, string> { ["accent"] = "#ff0000" },
            ".accent { fill: blue; }"));

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void CacheKey_IgnoresEntityInsertionOrder()
    {
        var path = "ms-appx:///Assets/Icon.svg";
        var first = SvgCacheKey.Create(path, new SvgParameters(
            new Dictionary<string, string>
            {
                ["accent"] = "#ff0000",
                ["stroke"] = "#000000"
            },
            ".accent { fill: red; }"));
        var second = SvgCacheKey.Create(path, new SvgParameters(
            new Dictionary<string, string>
            {
                ["stroke"] = "#000000",
                ["accent"] = "#ff0000"
            },
            ".accent { fill: red; }"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void RenderLayout_MapsControlPointToPicturePoint()
    {
        var created = SvgRenderLayout.TryCreateRenderInfo(
            new SvgSize(200, 100),
            new SvgRect(0, 0, 100, 50),
            Stretch.Uniform,
            StretchDirection.Both,
            1.0,
            0.0,
            0.0,
            out var renderInfo);

        Assert.True(created);
        Assert.True(renderInfo.TryMapToPicture(new SvgPoint(100, 50), out var picturePoint));
        Assert.Equal(50, picturePoint.X, 6);
        Assert.Equal(25, picturePoint.Y, 6);
    }

    [Fact]
    public void ZoomToPoint_AdjustsPanAndZoom()
    {
        var result = SvgRenderLayout.ZoomToPoint(1.0, 0.0, 0.0, 2.0, new SvgPoint(50, 25));

        Assert.Equal(2.0, result.Zoom, 6);
        Assert.Equal(-50.0, result.PanX, 6);
        Assert.Equal(-25.0, result.PanY, 6);
    }

    [Fact]
    public void BuildParameters_MergesSourceAndControlCss()
    {
        var source = new SvgSource
        {
            Css = ".source { fill: red; }",
            Entities = new Dictionary<string, string> { ["accent"] = "#ff0000" }
        };

        var parameters = Svg.BuildParameters(source, ".control { fill: blue; }", ".current { stroke: white; }");

        Assert.NotNull(parameters);
        Assert.Equal(".source { fill: red; } .control { fill: blue; } .current { stroke: white; }", parameters?.Css);
        Assert.Equal("#ff0000", parameters?.Entities?["accent"]);
    }

    [Fact]
    public void PrepareWorkingSource_KeepsSharedSourceIsolated_AndPropagatesRenderFlags()
    {
        var sharedSource = SvgSource.LoadFromSvg(SampleSvg);

        var workingSource = Svg.PrepareWorkingSource(
            sharedSource,
            ".control { fill: blue; }",
            ".current { stroke: white; }",
            wireframe: true,
            disableFilters: true);

        Assert.NotSame(sharedSource, workingSource);
        Assert.NotSame(sharedSource.Svg, workingSource.Svg);
        Assert.NotNull(sharedSource.Svg);
        Assert.NotNull(workingSource.Svg);
        Assert.False(sharedSource.Svg!.Wireframe);
        Assert.True(workingSource.Svg!.Wireframe);
        Assert.Equal(DrawAttributes.None, sharedSource.Svg.IgnoreAttributes);
        Assert.Equal(DrawAttributes.Filter, workingSource.Svg.IgnoreAttributes);
        Assert.Null(sharedSource.Parameters);
    }

    private static string CreateTempSvgFile()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, SampleSvg);
        return path;
    }
}
