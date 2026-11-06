using System;
using System.Text;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingRenderLimitTests
{
    [Fact]
    public void LongAcyclicUseChainFailsWithCatchableLimit()
    {
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(CreateUseChain(300)));
    }

    [Fact]
    public void SharedUseAliasInSiblingBranchesIsNotACycle()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="10">
              <defs>
                <rect id="shape" width="8" height="8" fill="red" />
                <use id="alias" href="#shape" />
                <g id="pair">
                  <use href="#alias" />
                  <use href="#alias" x="12" />
                </g>
              </defs>
              <use href="#pair" />
            </svg>
            """;

        using var svg = new SKSvg();
        var picture = svg.FromSvg(source);
        Assert.NotNull(picture);

        using var bitmap = new SkiaSharp.SKBitmap(24, 10);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.DrawPicture(picture!);
        canvas.Flush();

        Assert.Equal(SkiaSharp.SKColors.Red, bitmap.GetPixel(4, 4));
        Assert.Equal(SkiaSharp.SKColors.Red, bitmap.GetPixel(16, 4));
    }

    [Fact]
    public void ExistingUseCyclesRemainNonRendering()
    {
        const string source = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <defs>
                <use id="a" href="#b" />
                <use id="b" href="#a" />
              </defs>
              <use href="#a" />
              <rect width="10" height="10" fill="red" />
            </svg>
            """;

        using var svg = new SKSvg();
        Assert.NotNull(svg.FromSvg(source));
    }

    [Fact]
    public void CallerCreatedDeepDocumentIsRejectedBeforeLoading()
    {
        var document = CreateDeepDocument(300);
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvgDocument(document));
        Assert.Null(svg.SourceDocument);
    }

    [Fact]
    public void RuntimeRejectsDeepFragmentAncestorsBeforeOwnerDocumentLookup()
    {
        var document = CreateDeepDocument(300);
        SvgElement parent = document;
        while (parent.Children.Count > 0)
        {
            parent = parent.Children[0];
        }

        var fragment = new SvgFragment { Width = 10, Height = 10 };
        parent.Children.Add(fragment);
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() =>
            SvgSceneRuntime.CreateModel(fragment, svg.AssetLoader));
        Assert.Throws<SvgProcessingLimitException>(() =>
            SKSvg.ToPicture(fragment, svg.SkiaModel, svg.AssetLoader));
    }

    [Fact]
    public void FragmentFontScopeRejectsDeepSiblingInOwningDocument()
    {
        var document = CreateDeepDocument(300);
        var fragment = new SvgFragment { Width = 10, Height = 10 };
        fragment.Children.Add(new SvgRectangle { Width = 10, Height = 10 });
        document.Children.Add(fragment);
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() =>
            SKSvg.ToPicture(fragment, svg.SkiaModel, svg.AssetLoader));
    }

    [Fact]
    public void LongClipReferenceChainFailsWithCatchableLimit()
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs>");
        for (var i = 0; i < 300; i++)
        {
            source.Append($"<clipPath id='c{i}' clip-path='url(#c{i + 1})'><rect width='10' height='10'/></clipPath>");
        }

        source.Append("<clipPath id='c300'><rect width='10' height='10'/></clipPath></defs><rect width='10' height='10' clip-path='url(#c0)'/></svg>");
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(source.ToString()));
    }

    [Fact]
    public void LongGradientReferenceChainFailsWithCatchableLimit()
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs>");
        for (var i = 0; i < 300; i++)
        {
            source.Append($"<linearGradient id='g{i}' href='#g{i + 1}'/>");
        }

        source.Append("<linearGradient id='g300'><stop offset='0' stop-color='red'/></linearGradient></defs><rect width='10' height='10' fill='url(#g0)'/></svg>");
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(source.ToString()));
    }

    [Fact]
    public void NestedPatternsCannotResetProcessingBudget()
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs>");
        for (var i = 0; i < 300; i++)
        {
            source.Append($"<pattern id='p{i}' width='10' height='10' patternUnits='userSpaceOnUse'><rect width='10' height='10' fill='url(#p{i + 1})'/></pattern>");
        }

        source.Append("<pattern id='p300' width='10' height='10' patternUnits='userSpaceOnUse'><rect width='10' height='10' fill='red'/></pattern></defs><rect width='10' height='10' fill='url(#p0)'/></svg>");
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(source.ToString()));
    }

    [Fact]
    public void FlatFilterPrimitivesCannotCreateUnboundedNativeFilterGraph()
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs><filter id='f'>");
        for (var i = 0; i < 600; i++)
        {
            source.Append("<feOffset dx='0' dy='0'/>");
        }

        source.Append("</filter></defs><rect width='10' height='10' filter='url(#f)'/></svg>");
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(source.ToString()));
    }

    [Fact]
    public void NativeImageFilterConversionRejectsDeepGraphsAndRecovers()
    {
        SKImageFilter? filter = null;
        for (var i = 0; i < 600; i++)
        {
            filter = SKImageFilter.CreateBlur(1, 1, filter);
        }

        using var svg = new SKSvg();
        Assert.Throws<SvgProcessingLimitException>(() => svg.SkiaModel.ToSKImageFilter(filter));

        using var simpleFilter = svg.SkiaModel.ToSKImageFilter(SKImageFilter.CreateBlur(1, 1));
        Assert.NotNull(simpleFilter);
    }

    [Fact]
    public void NativePictureConversionRejectsDeepCommandGraphs()
    {
        var bounds = SKRect.Create(0, 0, 10, 10);
        var picture = new SKPicture(bounds, Array.Empty<CanvasCommand>());
        for (var i = 0; i < 600; i++)
        {
            picture = new SKPicture(bounds, new CanvasCommand[] { new DrawPictureCanvasCommand(picture) });
        }

        using var svg = new SKSvg();
        Assert.Throws<SvgProcessingLimitException>(() => svg.SkiaModel.ToSKPicture(picture));
    }

    [Fact]
    public void ProgrammaticDeepCssMathIsRejectedDuringRendering()
    {
        var document = CreateDocumentWithFilter("blur(calc(" + new string('(', 100) + "1px" + new string(')', 100) + "))");
        using var svg = new SKSvg();

        Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvgDocument(document));
    }

    [Fact]
    public void LongUnaryCssMathUsesIterationAndStillCreatesFilter()
    {
        var document = CreateDocumentWithFilter("blur(calc(" + new string('+', 4096) + "1px))");
        using var svg = new SKSvg();

        Assert.NotNull(svg.FromSvgDocument(document));
        Assert.NotNull(svg.Model);
        Assert.Contains(svg.Model!.FindCommands<SaveLayerCanvasCommand>(), command => command.Paint?.ImageFilter is not null);
    }

    [Fact]
    public void ProcessingBudgetRecoversAfterFailureAndDoesNotAccumulateAcrossSiblings()
    {
        using var svg = new SKSvg();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg(CreateUseChain(300)));
            Assert.NotNull(svg.FromSvg(CreateUseChain(8)));
        }

        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'>");
        for (var i = 0; i < 1000; i++)
        {
            source.Append("<rect width='1' height='1'/>");
        }

        source.Append("</svg>");
        Assert.NotNull(svg.FromSvg(source.ToString()));
    }

    private static string CreateUseChain(int length)
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs>");
        for (var i = 0; i < length; i++)
        {
            source.Append($"<use id='u{i}' href='#u{i + 1}'/>");
        }

        source.Append($"<rect id='u{length}' width='10' height='10' fill='red'/></defs><use href='#u0'/></svg>");
        return source.ToString();
    }

    private static SvgDocument CreateDeepDocument(int depth)
    {
        var document = new SvgDocument { Width = 10, Height = 10 };
        SvgElement parent = document;
        for (var i = 0; i < depth; i++)
        {
            var child = new SvgGroup();
            parent.Children.Add(child);
            parent = child;
        }

        return document;
    }

    private static SvgDocument CreateDocumentWithFilter(string filter)
    {
        var document = new SvgDocument { Width = 10, Height = 10 };
        var rectangle = new SvgRectangle { Width = 10, Height = 10 };
        rectangle.AddStyle("filter", filter, 1 << 16);
        document.Children.Add(rectangle);
        return document;
    }
}
