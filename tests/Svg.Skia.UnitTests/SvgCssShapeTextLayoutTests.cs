using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgCssShapeTextLayoutTests
{
    private static readonly Type s_svgSceneTextCompilerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgSceneTextCompiler")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgSceneTextCompiler.");
    private static readonly Type s_svgTextContentMetricsType =
        s_svgSceneTextCompilerType.GetNestedType("SvgTextContentMetrics", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate SvgSceneTextCompiler.SvgTextContentMetrics.");
    private static readonly Type s_svgCssShapeImageSamplerType =
        typeof(SvgSceneNode).Assembly.GetType("Svg.Skia.SvgCssShapeImageSampler")
        ?? throw new InvalidOperationException("Could not locate Svg.Skia.SvgCssShapeImageSampler.");

    private static readonly MethodInfo s_tryCreateTextContentMetricsMethod =
        s_svgSceneTextCompilerType.GetMethod(
            "TryCreateTextContentMetrics",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(SvgTextBase), typeof(SKRect), typeof(ISvgAssetLoader), s_svgTextContentMetricsType.MakeByRefType() },
            null)
        ?? throw new InvalidOperationException("Could not locate SvgSceneTextCompiler.TryCreateTextContentMetrics.");

    private static readonly MethodInfo s_tryCreateCssShapeAlphaPathMethod =
        s_svgCssShapeImageSamplerType.GetMethod(
            "TryCreateAlphaPath",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(int), typeof(int), typeof(byte[]), typeof(SKRect), typeof(float), typeof(SKPath).MakeByRefType() },
            null)
        ?? throw new InvalidOperationException("Could not locate SvgCssShapeImageSampler.TryCreateAlphaPath.");

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRlShapeInsideUsesResolvedBaselineColumn()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="160" viewBox="0 0 140 160">
              <defs>
                <rect id="shape" x="40" y="20" width="30" height="120" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" shape-inside="url(#shape)">A</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(10f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        var start = metrics!.GetStartPositionOfChar(0);
        Assert.InRange(start.X, 59.5f, 60.1f);
        Assert.True(start.Y > 130f, $"Expected vertical RTL inline progression to start at the bottom edge of the shape interval, but Y was {start.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_VerticalRtlShapeSubtractUsesMultipleSameColumnFragments()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="180" viewBox="0 0 180 180">
              <defs>
                <rect id="shape" x="40" y="20" width="80" height="140" />
                <rect id="subtract" x="40" y="70" width="80" height="40" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(20f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(firstStart.X, secondStart.X, 3);
        Assert.True(firstStart.Y >= 140f, $"Expected RTL vertical text to consume the lower same-column fragment first, but Y was {firstStart.Y}.");
        Assert.True(secondStart.Y <= 70f, $"Expected RTL vertical text to continue in the upper same-column fragment, but Y was {secondStart.Y}.");
    }

    [Fact]
    public void TryCreateTextContentMetrics_NonZeroOppositeDirectionSubpathCreatesHoleFragment()
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="150" height="120" viewBox="0 0 150 120">
              <defs>
                <path id="shape" fill-rule="nonzero" d="M10 20 H120 V100 H10 Z M50 20 V100 H80 V20 Z" />
              </defs>
              <text id="label" font-family="sans-serif" font-size="10" shape-inside="url(#shape)">A B</text>
            </svg>
            """);
        var svgText = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var viewport = GetDocumentViewport(document);
        var assetLoader = new FixedAdvanceAssetLoader(20f);

        var succeeded = InvokeTryCreateTextContentMetrics(svgText, viewport, assetLoader, out var metrics);

        Assert.True(succeeded);
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.NumberOfChars);

        var firstStart = metrics.GetStartPositionOfChar(0);
        var secondStart = metrics.GetStartPositionOfChar(1);
        Assert.Equal(10f, firstStart.X, 3);
        Assert.Equal(firstStart.Y, secondStart.Y, 3);
        Assert.True(secondStart.X >= 80f, $"Expected opposite-direction nonzero subpath to create a same-line hole fragment, but positions were {firstStart} and {secondStart}.");
    }

    [Fact]
    public void SvgCssShapeImageSampler_ThresholdUsesStrictAlphaComparison()
    {
        var alpha = new byte[] { 128, 0 };

        var succeeded = InvokeTryCreateCssShapeAlphaPath(2, 1, alpha, SKRect.Create(0f, 0f, 20f, 10f), 0.5f, out var path);

        Assert.True(succeeded);
        Assert.NotNull(path);
        Assert.Equal(0f, path!.Bounds.Left, 3);
        Assert.Equal(10f, path.Bounds.Right, 3);
    }

    [Fact]
    public void SvgCssShapeImageSampler_CoalescesAdjacentAlphaRunsButKeepsIslands()
    {
        var alpha = new byte[]
        {
            255, 255, 0, 255,
            255, 255, 0, 255
        };

        var succeeded = InvokeTryCreateCssShapeAlphaPath(4, 2, alpha, SKRect.Create(0f, 0f, 40f, 20f), 0f, out var path);

        Assert.True(succeeded);
        Assert.NotNull(path);
        Assert.Equal(2, path!.Commands!.OfType<AddRectPathCommand>().Count());
        Assert.Equal(0f, path.Bounds.Left, 3);
        Assert.Equal(40f, path.Bounds.Right, 3);
    }

    [Fact]
    public void SvgCssShapeImageSampler_RejectsOverflowingImageDimensions()
    {
        var succeeded = InvokeTryCreateCssShapeAlphaPath(
            50000,
            50000,
            Array.Empty<byte>(),
            SKRect.Create(0f, 0f, 100f, 100f),
            0f,
            out var path);

        Assert.False(succeeded);
        Assert.Null(path);
    }

    private static bool InvokeTryCreateTextContentMetrics(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out TextContentMetricsSnapshot? snapshot)
    {
        var args = new object?[]
        {
            svgTextBase,
            viewport,
            assetLoader,
            null
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateTextContentMetricsMethod.Invoke(null, args));
        if (!succeeded)
        {
            snapshot = null;
            return false;
        }

        Assert.NotNull(args[3]);
        snapshot = new TextContentMetricsSnapshot(args[3]!);
        return true;
    }

    private static bool InvokeTryCreateCssShapeAlphaPath(
        int width,
        int height,
        byte[] alpha,
        SKRect referenceBox,
        float threshold,
        out SKPath? path)
    {
        var args = new object?[]
        {
            width,
            height,
            alpha,
            referenceBox,
            threshold,
            null
        };

        var succeeded = Assert.IsType<bool>(s_tryCreateCssShapeAlphaPathMethod.Invoke(null, args));
        if (!succeeded)
        {
            path = null;
            return false;
        }

        Assert.NotNull(args[5]);
        path = Assert.IsType<SKPath>(args[5]);
        return true;
    }

    private static SKRect GetDocumentViewport(SvgDocument document)
    {
        var size = SvgService.GetDimensions(document);
        var bounds = SKRect.Create(size);
        if (!bounds.IsEmpty)
        {
            return bounds;
        }

        if (document.ViewBox.Width > 0f && document.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                document.ViewBox.MinX,
                document.ViewBox.MinY,
                document.ViewBox.Width,
                document.ViewBox.Height);
        }

        return SKRect.Empty;
    }

    private sealed class TextContentMetricsSnapshot
    {
        private readonly object _metrics;
        private readonly Type _metricsType;

        public TextContentMetricsSnapshot(object metrics)
        {
            _metrics = metrics;
            _metricsType = metrics.GetType();
        }

        public int NumberOfChars => Assert.IsType<int>(_metricsType.GetProperty(nameof(NumberOfChars))!.GetValue(_metrics));

        public SKPoint GetStartPositionOfChar(int charnum)
        {
            return Assert.IsType<SKPoint>(_metricsType.GetMethod(nameof(GetStartPositionOfChar))!.Invoke(_metrics, new object[] { charnum }));
        }
    }

    private sealed class FixedAdvanceAssetLoader : ISvgAssetLoader, ISvgTextRunTypefaceResolver, ISvgTextGlyphRunResolver
    {
        private readonly float _codepointAdvance;
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
            "sans-serif",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        public FixedAdvanceAssetLoader(float codepointAdvance)
        {
            _codepointAdvance = codepointAdvance;
        }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream) => new();

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
        {
            var resolvedText = text ?? string.Empty;
            return
            [
                new TypefaceSpan(resolvedText, GetAdvance(resolvedText), _typeface)
            ];
        }

        public SKFontMetrics GetFontMetrics(SKPaint paint)
        {
            var size = paint.TextSize > 0f ? paint.TextSize : 10f;
            return new SKFontMetrics
            {
                Ascent = -size * 0.8f,
                Descent = size * 0.2f,
                Top = -size * 0.8f,
                Bottom = size * 0.2f,
                Leading = 0f
            };
        }

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
        {
            var advance = GetAdvance(text ?? string.Empty);
            var metrics = GetFontMetrics(paint);
            bounds = advance > 0f
                ? new SKRect(0f, metrics.Ascent, advance, metrics.Descent)
                : SKRect.Empty;
            return advance;
        }

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y) => null;

        public SKTypeface? FindRunTypeface(string? text, SKPaint paintPreferredTypeface) => _typeface;

        public bool TryShapeGlyphRun(string? text, SKPaint paint, out ShapedGlyphRun shapedRun)
        {
            if (string.IsNullOrEmpty(text))
            {
                shapedRun = default;
                return false;
            }

            var glyphs = new ushort[text.Length];
            var points = new SKPoint[text.Length];
            var clusters = new int[text.Length];
            var currentX = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                glyphs[i] = (ushort)(i + 1);
                points[i] = new SKPoint(currentX, 0f);
                clusters[i] = i;
                currentX += GetAdvance(text[i].ToString());
            }

            shapedRun = new ShapedGlyphRun(glyphs, points, clusters, currentX);
            return true;
        }

        private float GetAdvance(string text)
        {
            var advance = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                if (!IsZeroAdvanceControl(text[i]))
                {
                    advance += _codepointAdvance;
                }
            }

            return advance;
        }

        private static bool IsZeroAdvanceControl(char value)
        {
            return value is '\r' or '\n' or '\u034F' or '\u061C' or '\u200C' or '\u200D' or
                '\u200E' or '\u200F' or '\u202A' or '\u202B' or '\u202C' or '\u202D' or
                '\u202E' or '\u2060' or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\uFEFF';
        }
    }
}
