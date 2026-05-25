#nullable enable

using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.Skia;

internal interface ISvgTextLayoutEngine
{
    bool TryLayout(SvgTextLayoutRequest request, out SvgTextLayoutResult result);
}

internal interface ISvgTextLayoutFontServices
{
    SvgTextMeasureResult MeasureText(SvgTextResolvedStyle style, string text, SKRect geometryBounds);

    float[] MeasureCodepointAdvances(
        SvgTextResolvedStyle style,
        IReadOnlyList<SvgTextCodepoint> codepoints,
        SKRect geometryBounds);

    bool TryShapeGlyphRun(
        SvgTextResolvedStyle style,
        string text,
        bool rightToLeft,
        SKRect geometryBounds,
        out SvgTextGlyphRun glyphRun);

    bool TryGetTextPath(
        SvgTextResolvedStyle style,
        string text,
        SKPoint origin,
        SKRect geometryBounds,
        out SKPath path);
}

internal interface ISvgTextLayoutBreakResolver
{
    bool AllowsSoftWrapOpportunity(
        SvgTextCodepointRun run,
        int codepointIndex,
        SvgTextLineBreakPolicy policy);
}

internal interface ISvgTextLayoutShapeResolver
{
    bool TryResolveLayoutArea(
        SvgTextBase textRoot,
        SKRect viewport,
        SKRect geometryBounds,
        out SvgTextShapeLayout shapeLayout);
}

internal interface ISvgTextPathLayoutResolver
{
    bool TryResolveTextPath(
        SvgTextPath textPath,
        SKRect viewport,
        SKRect geometryBounds,
        out SvgTextPathGeometry geometry);
}

internal sealed class SvgTextLayoutRequest
{
    public SvgTextLayoutRequest(
        SvgTextBase textRoot,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SvgTextLayoutOptions options,
        ISvgTextLayoutFontServices fontServices,
        ISvgTextLayoutBreakResolver breakResolver,
        ISvgTextLayoutShapeResolver? shapeResolver = null,
        ISvgTextPathLayoutResolver? textPathResolver = null)
    {
        TextRoot = textRoot ?? throw new ArgumentNullException(nameof(textRoot));
        Viewport = viewport;
        GeometryBounds = geometryBounds;
        AssetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        Options = options;
        FontServices = fontServices ?? throw new ArgumentNullException(nameof(fontServices));
        BreakResolver = breakResolver ?? throw new ArgumentNullException(nameof(breakResolver));
        ShapeResolver = shapeResolver;
        TextPathResolver = textPathResolver;
    }

    public SvgTextBase TextRoot { get; }

    public SKRect Viewport { get; }

    public SKRect GeometryBounds { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SvgTextLayoutOptions Options { get; }

    public ISvgTextLayoutFontServices FontServices { get; }

    public ISvgTextLayoutBreakResolver BreakResolver { get; }

    public ISvgTextLayoutShapeResolver? ShapeResolver { get; }

    public ISvgTextPathLayoutResolver? TextPathResolver { get; }
}

internal readonly record struct SvgTextLayoutOptions(
    bool IncludeRenderCommands,
    bool IncludeDomMetrics,
    bool EnableTextReferences,
    bool EnableTextPath,
    bool EnableShapeLayout,
    bool EnableTextLength,
    bool TrimLeadingWhitespace,
    bool UseVisualBidiOrder)
{
    public static SvgTextLayoutOptions Default { get; } = new(
        IncludeRenderCommands: true,
        IncludeDomMetrics: true,
        EnableTextReferences: true,
        EnableTextPath: true,
        EnableShapeLayout: true,
        EnableTextLength: true,
        TrimLeadingWhitespace: true,
        UseVisualBidiOrder: true);
}

internal readonly record struct SvgTextWrappingOptions(
    int MaxLineSearchCount,
    bool PreserveLineEdgeWhitespace,
    float TextLengthTolerance)
{
    public int EffectiveMaxLineSearchCount => Math.Max(1, MaxLineSearchCount);

    public float EffectiveTextLengthTolerance => Math.Max(0f, TextLengthTolerance);
}

internal readonly record struct SvgTextMeasureResult(
    string DrawText,
    float Advance,
    float NaturalAdvance,
    SKRect RelativeBounds,
    SKTypeface? Typeface,
    bool UsesResolvedRunTypeface);

internal readonly record struct SvgTextLayoutDiagnostic(
    string Code,
    string Message,
    SvgElement? SourceElement = null);
