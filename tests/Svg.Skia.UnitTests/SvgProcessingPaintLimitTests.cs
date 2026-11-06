using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingPaintLimitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongPaintFallbackChain_ThrowsCatchableLimitException(bool compatibilityLoader)
    {
        var paint = string.Concat(Enumerable.Repeat("url(#missing) ", 2048)) + "red";
        var input = $"<svg xmlns='http://www.w3.org/2000/svg'><rect fill='{paint}' /></svg>";

        Assert.Throws<SvgProcessingLimitException>(() => compatibilityLoader
            ? SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(input)
            : SvgDocument.FromSvg<SvgDocument>(input));
    }

    [Theory]
    [InlineData("serialize")]
    [InlineData("clone")]
    public void CallerCreatedPaintFallbackChain_ThrowsCatchableLimitException(string operation)
    {
        SvgPaintServer paint = new SvgColourServer(Color.Red);
        for (var i = 0; i < 2048; i++)
        {
            paint = new SvgDeferredPaintServer("url(#missing)", paint);
        }

        if (operation == "serialize")
        {
            Assert.Throws<SvgProcessingLimitException>(() => paint.ToString());
        }
        else
        {
            Assert.Throws<SvgProcessingLimitException>(() => paint.DeepCopy());
        }
    }

    [Fact]
    public void OrdinaryPaintFallback_PreservesItsColor()
    {
        var document = SvgDocument.FromSvg<SvgDocument>(
            "<svg xmlns='http://www.w3.org/2000/svg'><rect fill='url(#missing) red' /></svg>");
        var rectangle = Assert.IsType<SvgRectangle>(Assert.Single(document.Children));
        var paint = Assert.IsType<SvgDeferredPaintServer>(rectangle.Fill);

        Assert.Equal(Color.Red, Assert.IsType<SvgColourServer>(paint.FallbackServer).Colour);
    }

    [Fact]
    public void RejectedPaint_OnSmallStack_DoesNotPoisonFollowingLoad()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var paint = string.Concat(Enumerable.Repeat("url(#missing) ", 2048)) + "red";
                Assert.Throws<SvgProcessingLimitException>(() => SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
                    $"<svg xmlns='http://www.w3.org/2000/svg'><rect fill='{paint}' /></svg>"));

                var valid = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
                    "<svg xmlns='http://www.w3.org/2000/svg'><rect fill='green' /></svg>");
                var rectangle = Assert.IsType<SvgRectangle>(Assert.Single(valid.Children));
                Assert.Equal(Color.Green, Assert.IsType<SvgColourServer>(rectangle.Fill).Colour);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        }, 256 * 1024);

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "SVG processing must terminate.");
        Assert.Null(threadFailure);
    }
}
