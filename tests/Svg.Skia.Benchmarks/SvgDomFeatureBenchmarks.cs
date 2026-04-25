using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Svg.JavaScript;

namespace Svg.Skia.Benchmarks;

public class SvgDomFeatureBenchmarks
{
    private static readonly FieldInfo s_runtimeField =
        typeof(SKSvg).GetField("_javaScriptRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to access SVG JavaScript runtime.");

    private static readonly string s_styleCaptureSvg = CreateStyleCaptureSvg(elementCount: 1200);
    private static readonly string s_noCssPresentationSvg = CreateNoCssPresentationSvg(elementCount: 1200);
    private static readonly byte[] s_noCssPresentationSvgBytes = Encoding.UTF8.GetBytes(s_noCssPresentationSvg);
    private static readonly string s_svgFontTextDomSvg = CreateSvgFontTextDomSvg(repetitionCount: 80);

    private SKSvg? styleMutationSvg;
    private SvgJavaScriptElement? styleMutationTarget;
    private int styleMutationToggle;

    private SKSvg? textDomSvg;
    private SvgJavaScriptElement? textDomElement;

    [GlobalSetup]
    public void GlobalSetup()
    {
        styleMutationSvg = CreateJavaScriptSvg(s_styleCaptureSvg);
        styleMutationTarget = GetRuntime(styleMutationSvg).GetElement(styleMutationSvg.SourceDocument!.GetElementById("rect-600")!);

        textDomSvg = CreateJavaScriptSvg(s_svgFontTextDomSvg);
        textDomElement = GetRuntime(textDomSvg).GetElement(textDomSvg.SourceDocument!.GetElementById("text")!);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        styleMutationSvg?.Dispose();
        textDomSvg?.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "StyleCapture", "Load")]
    public float LoadJavaScriptStyleCapture()
    {
        using var svg = CreateJavaScriptSvg(s_styleCaptureSvg);
        return svg.Picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "StyleCapture", "Load")]
    public float LoadJavaScriptNoCssPresentationAttributes()
    {
        using var svg = CreateJavaScriptSvg(s_noCssPresentationSvg);
        return svg.Picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "StyleCapture", "Load")]
    public float LoadJavaScriptNoCssPresentationAttributesFromStream()
    {
        using var svg = CreateJavaScriptSvgFromStream(s_noCssPresentationSvgBytes);
        return svg.Picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "StyleCapture", "Clone")]
    public float CloneJavaScriptStyleCapture()
    {
        using var clone = styleMutationSvg!.Clone();
        return clone.Picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "StyleCapture", "MutationRender")]
    public float MutateStyleClassAndRefresh()
    {
        var className = (styleMutationToggle++ & 1) == 0 ? "hot" : "cold";
        styleMutationTarget!.setAttribute("class", className);
        using var picture = styleMutationSvg!.RefreshFromSourceDocument();
        return picture?.CullRect.Width ?? 0f;
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "Text", "SvgFont", "FirstQuery")]
    public double LoadSvgFontTextDomAndQueryMetrics()
    {
        using var svg = CreateJavaScriptSvg(s_svgFontTextDomSvg);
        var element = GetRuntime(svg).GetElement(svg.SourceDocument!.GetElementById("text")!);
        return QueryTextDomMetrics(element);
    }

    [Benchmark]
    [BenchmarkCategory("DOM", "Text", "SvgFont", "CachedQuery")]
    public double QueryCachedSvgFontTextDomMetrics()
    {
        return QueryTextDomMetrics(textDomElement!);
    }

    private static SKSvg CreateJavaScriptSvg(string svgText)
    {
        var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.EnableSvgFonts = true;
        svg.FromSvg(svgText);
        return svg;
    }

    private static SKSvg CreateJavaScriptSvgFromStream(byte[] svgBytes)
    {
        var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.EnableSvgFonts = true;
        using var stream = new MemoryStream(svgBytes, writable: false);
        svg.Load(stream);
        return svg;
    }

    private static double QueryTextDomMetrics(SvgJavaScriptElement element)
    {
        var count = element.getNumberOfChars();
        if (count == 0)
        {
            return 0f;
        }

        var extent = element.getExtentOfChar(count - 1);
        return element.getComputedTextLength() +
               element.getSubStringLength(0, count) +
               element.getRotationOfChar(count - 1) +
               extent.width;
    }

    private static SvgJavaScriptRuntime GetRuntime(SKSvg svg)
    {
        return (SvgJavaScriptRuntime?)s_runtimeField.GetValue(svg)
               ?? throw new InvalidOperationException("SVG JavaScript runtime is not initialized.");
    }

    private static string CreateStyleCaptureSvg(int elementCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1200" viewBox="0 0 1200 1200">""");
        builder.AppendLine("""
          <style><![CDATA[
            rect.cold { fill: #0b7a75; stroke: #222; stroke-width: 1; }
            rect.hot { fill: #d1495b; stroke: #111; stroke-width: 2; }
            g.band rect:nth-child(2n) { opacity: 0.86; }
          ]]></style>
          <g class="band">
        """);

        for (var i = 0; i < elementCount; i++)
        {
            var x = (i % 60) * 20;
            var y = (i / 60) * 20;
            var className = (i & 1) == 0 ? "cold" : "hot";
            builder.Append(CultureInfo.InvariantCulture, $"""    <rect id="rect-{i}" class="{className}" x="{x}" y="{y}" width="16" height="16" fill="#5b8def" stroke="#203040" stroke-width="1" style="fill-opacity:{0.55 + ((i % 5) * 0.05):0.00}" />""");
            builder.AppendLine();
        }

        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string CreateNoCssPresentationSvg(int elementCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1200" viewBox="0 0 1200 1200">""");
        for (var i = 0; i < elementCount; i++)
        {
            var x = (i % 60) * 20;
            var y = (i / 60) * 20;
            var fill = (i & 1) == 0 ? "#5b8def" : "#d1495b";
            builder.Append(CultureInfo.InvariantCulture, $"""  <rect id="rect-{i}" x="{x}" y="{y}" width="16" height="16" fill="{fill}" stroke="#203040" stroke-width="1" opacity="{0.55 + ((i % 5) * 0.05):0.00}" />""");
            builder.AppendLine();
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string CreateSvgFontTextDomSvg(int repetitionCount)
    {
        var text = string.Concat(Enumerable.Repeat("ffi A ", repetitionCount));
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="9600" height="160" viewBox="0 0 9600 160">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'BenchLigatureFont';
                    src: url('#BenchLigatureFontFace') format('svg');
                  }
                ]]></style>
                <font id="BenchLigatureFontFace" horiz-adv-x="100">
                  <font-face font-family="BenchLigatureFont" units-per-em="100" ascent="100" descent="0" />
                  <missing-glyph horiz-adv-x="60" d="M5 0H45V100H5Z" />
                  <glyph unicode="ffi" horiz-adv-x="120" d="M0 0H18V100H0ZM42 0H60V100H42ZM84 0H102V100H84Z" />
                  <glyph unicode="ff" horiz-adv-x="80" d="M0 0H18V100H0ZM42 0H60V100H42Z" />
                  <glyph unicode="f" horiz-adv-x="40" d="M8 0H28V100H8Z" />
                  <glyph unicode="i" horiz-adv-x="35" d="M12 0H24V80H12ZM12 90H24V100H12Z" />
                  <glyph unicode="A" horiz-adv-x="90" d="M5 0H25L45 100H65L85 0H65L60 25H30L25 0Z" />
                  <glyph unicode=" " horiz-adv-x="35" />
                </font>
              </defs>
              <text id="text" x="10" y="120" fill="black" font-family="BenchLigatureFont" font-size="100">{{text}}</text>
            </svg>
            """;
    }
}
