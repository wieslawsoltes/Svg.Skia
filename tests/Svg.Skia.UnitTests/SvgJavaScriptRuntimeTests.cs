using System.Drawing;
using System.IO;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgJavaScriptRuntimeTests
{
    [Fact]
    public void FromSvg_DoesNotExecuteScriptsByDefault()
    {
        using var svg = new SKSvg();

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script>document.getElementById('target').setAttribute('fill', 'green');</script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Red);
    }

    [Fact]
    public void FromSvg_ExecutesInlineScriptsWhenEnabled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                document.getElementById('target').setAttribute('fill', 'green');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_SkipsUnsupportedScriptTypes()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script type="application/noSuchLanguage">
                document.getElementById('target').setAttribute('fill', 'green');
              </script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Red);
    }

    [Fact]
    public void FromSvg_ExecutesRootOnLoadWhenEnabled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
                 onload="document.getElementById('target').setAttribute('fill', 'green')">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void Load_ExecutesExternalScriptsWhenEnabled()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var scriptPath = Path.Combine(tempDirectory, "update.js");
            var svgPath = Path.Combine(tempDirectory, "test.svg");
            File.WriteAllText(scriptPath, "document.getElementById('target').setAttribute('fill', 'green');");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg"
                     xmlns:xlink="http://www.w3.org/1999/xlink"
                     width="20"
                     height="20">
                  <rect id="target" width="10" height="10" fill="red" />
                  <script xlink:href="update.js" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Settings.EnableJavaScript = true;
            svg.Load(svgPath);

            AssertFill(svg, "target", Color.Green);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DispatchPointerReleased_ExecutesClickHandlerAndRefreshesDocument()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="evt.target.ownerDocument.getElementById('target').style.fill = 'green'" />
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        Assert.Equal("target", svg.HitTestTopmostElement(input.PicturePoint)?.ID);
        var target = Assert.IsType<SvgRectangle>(svg.SourceDocument!.Descendants().Single(element => element.ID == "target"));
        Assert.True(target.TryGetAttribute("onclick", out _));

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_HonorsJavaScriptStopPropagation()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g onclick="document.getElementById('target').setAttribute('fill', 'blue')">
                <rect id="target" width="20" height="20" fill="red"
                      onclick="evt.stopPropagation(); this.setAttribute('fill', 'green')" />
              </g>
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_ReturnFalseMarksEventHandled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red" onclick="return false;" />
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        Assert.True(result.Handled);
    }

    [Fact]
    public void Clone_DispatchPointerReleased_ExecutesClickHandler()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="this.setAttribute('fill', 'green')" />
            </svg>
            """);

        using var clone = svg.Clone();
        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(clone, input);
        dispatcher.DispatchPointerReleased(clone, input);

        AssertFill(svg, "target", Color.Red);
        AssertFill(clone, "target", Color.Green);
    }

    private static void AssertFill(SKSvg svg, string id, Color expected)
    {
        var rect = Assert.IsType<SvgRectangle>(svg.SourceDocument!.Descendants().Single(element => element.ID == id));
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }
}
