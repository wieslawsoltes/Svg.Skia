using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.DataTypes;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal sealed class SvgSceneContextPaint
{
    public SvgSceneContextPaint(SvgVisualElement element, SKRect bounds, SvgSceneContextPaint? parent)
    {
        Element = element;
        Bounds = bounds;
        Parent = parent;
    }

    public SvgVisualElement Element { get; }

    public SKRect Bounds { get; }

    public SvgSceneContextPaint? Parent { get; }
}

internal static class SvgScenePaintingService
{
    internal readonly record struct SolidFillPaintCacheKey(bool IsAntialias, SKColor Color, bool LinearRgb);

    internal sealed class GradientPaintCache
    {
        private Dictionary<GradientStopCacheKey, GradientStopCacheEntry>? _stops;

        internal bool TryGetStops(
            SvgGradientServer root,
            float opacity,
            DrawAttributes ignoreAttributes,
            bool isLinearRgb,
            out GradientStopCacheEntry entry)
        {
            if (_stops is null)
            {
                entry = default;
                return false;
            }

            return _stops.TryGetValue(
                new GradientStopCacheKey(root, opacity, ignoreAttributes, isLinearRgb),
                out entry);
        }

        internal void SetStops(
            SvgGradientServer root,
            float opacity,
            DrawAttributes ignoreAttributes,
            bool isLinearRgb,
            GradientStopCacheEntry entry)
        {
            _stops ??= new Dictionary<GradientStopCacheKey, GradientStopCacheEntry>();
            _stops[new GradientStopCacheKey(root, opacity, ignoreAttributes, isLinearRgb)] = entry;
        }
    }

    private readonly record struct GradientStopCacheKey(
        SvgGradientServer Root,
        float Opacity,
        DrawAttributes IgnoreAttributes,
        bool IsLinearRgb);

    internal readonly struct GradientStopCacheEntry
    {
        public GradientStopCacheEntry(SKColor singleColor)
        {
            HasStops = true;
            SingleColor = singleColor;
            Colors = null;
            ColorPos = null;
        }

        public GradientStopCacheEntry(SKColorF[] colors, float[] colorPos)
        {
            HasStops = true;
            SingleColor = default;
            Colors = colors;
            ColorPos = colorPos;
        }

        public static GradientStopCacheEntry Empty { get; } = new();

        public bool HasStops { get; }

        public SKColor SingleColor { get; }

        public SKColorF[]? Colors { get; }

        public float[]? ColorPos { get; }
    }

    private readonly struct GradientServerChain
    {
        private readonly SvgGradientServer? _single;
        private readonly List<SvgGradientServer>? _servers;

        public GradientServerChain(SvgGradientServer single)
        {
            _single = single;
            _servers = null;
        }

        public GradientServerChain(List<SvgGradientServer> servers)
        {
            _single = null;
            _servers = servers;
        }

        public int Count => _servers?.Count ?? (_single is null ? 0 : 1);

        public SvgGradientServer this[int index]
        {
            get
            {
                if (_servers is not null)
                {
                    return _servers[index];
                }

                if (index == 0 && _single is not null)
                {
                    return _single;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    private const int MaxVisiblePatternOverflowCopies = 256;

    [ThreadStatic]
    private static HashSet<SvgPatternServer>? s_activePatternServers;

    internal static float AdjustSvgOpacity(float opacity)
    {
        return Math.Min(Math.Max(opacity, 0f), 1f);
    }

    internal static SKPaint? GetOpacityPaint(float opacity)
    {
        var adjustedOpacity = AdjustSvgOpacity(opacity);
        if (adjustedOpacity >= 1f)
        {
            return null;
        }

        return new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, (byte)Math.Round(adjustedOpacity * 255f)),
            Style = SKPaintStyle.StrokeAndFill
        };
    }

    internal static bool IsValidFill(SvgElement svgElement)
    {
        return IsValidHitTestPaintServer(svgElement.Fill, svgElement);
    }

    internal static bool IsValidStroke(SvgElement svgElement, SKRect skBounds)
    {
        var stroke = svgElement.Stroke;
        var strokeWidth = svgElement.StrokeWidth;
        return IsValidHitTestPaintServer(stroke, svgElement)
            && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
    }

    private static bool IsValidHitTestPaintServer(SvgPaintServer? server, SvgElement owner, int depth = 0)
    {
        if (server is null || server == SvgPaintServer.None || depth >= 8)
        {
            return false;
        }

        if (server is SvgDeferredPaintServer deferredServer)
        {
            var resolved = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, owner);
            if (resolved is not null)
            {
                return IsValidHitTestPaintServer(resolved, owner, depth + 1);
            }

            return IsValidHitTestPaintServer(deferredServer.FallbackServer, owner, depth + 1);
        }

        return true;
    }

    internal static SKPaint? GetFillPaint(
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneContextPaint? contextPaint = null,
        GradientPaintCache? gradientPaintCache = null)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = PaintingService.IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Fill
        };

        var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
        return TryApplyPaintServer(
                svgVisualElement,
                svgVisualElement.Fill,
                opacity,
                skBounds,
                skPaint,
                forStroke: false,
                assetLoader,
                ignoreAttributes,
                contextPaint,
                gradientPaintCache)
            ? skPaint
            : null;
    }

    internal static bool TryCreateSolidFillPaintCacheKey(
        SvgVisualElement svgVisualElement,
        DrawAttributes ignoreAttributes,
        out SolidFillPaintCacheKey key)
    {
        key = default;

        if (svgVisualElement.Fill is not SvgColourServer svgColourServer)
        {
            return false;
        }

        var colorInterpolation = GetColorInterpolation(svgVisualElement);
        var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
        var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
        var skColor = GetColor(svgColourServer, opacity, ignoreAttributes);
        if (isLinearRgb)
        {
            skColor = ToLinear(skColor);
        }

        key = new SolidFillPaintCacheKey(
            PaintingService.IsAntialias(svgVisualElement),
            skColor,
            isLinearRgb);
        return true;
    }

    internal static SKPaint CreateSolidFillPaint(SolidFillPaintCacheKey key)
    {
        var paint = new SKPaint
        {
            IsAntialias = key.IsAntialias,
            Style = SKPaintStyle.Fill
        };

        if (!key.LinearRgb)
        {
            paint.Color = key.Color;
            return paint;
        }

        paint.Shader = SKShader.CreateColor(key.Color, SKColorSpace.SrgbLinear);
        return paint;
    }

    internal static SKPaint? GetStrokePaint(
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneContextPaint? contextPaint = null,
        SKPath? geometryPath = null,
        GradientPaintCache? gradientPaintCache = null)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = PaintingService.IsAntialias(svgVisualElement),
            Style = SKPaintStyle.Stroke
        };

        var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
        if (!TryApplyPaintServer(
                svgVisualElement,
                svgVisualElement.Stroke,
                opacity,
                skBounds,
                skPaint,
                forStroke: true,
                assetLoader,
                ignoreAttributes,
                contextPaint,
                gradientPaintCache))
        {
            return null;
        }

        skPaint.StrokeCap = svgVisualElement.StrokeLineCap switch
        {
            SvgStrokeLineCap.Round => SKStrokeCap.Round,
            SvgStrokeLineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };

        skPaint.StrokeJoin = svgVisualElement.StrokeLineJoin switch
        {
            SvgStrokeLineJoin.Round => SKStrokeJoin.Round,
            SvgStrokeLineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };

        skPaint.StrokeMiter = svgVisualElement.StrokeMiterLimit;
        skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgVisualElement, skBounds);
        skPaint.IsStrokeNonScaling = svgVisualElement.VectorEffect == SvgVectorEffect.NonScalingStroke;

        if (svgVisualElement.StrokeDashArray is { })
        {
            SetDash(svgVisualElement, skPaint, skBounds, geometryPath);
        }

        return skPaint;
    }

    private static bool TryApplyPaintServer(
        SvgVisualElement svgVisualElement,
        SvgPaintServer? server,
        float opacity,
        SKRect skBounds,
        SKPaint skPaint,
        bool forStroke,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneContextPaint? contextPaint,
        GradientPaintCache? gradientPaintCache,
        int contextPaintDepth = 0)
    {
        if (server is null)
        {
            return false;
        }

        var fallbackServer = SvgPaintServer.None;
        if (server is SvgDeferredPaintServer deferredServer)
        {
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, svgVisualElement);
            fallbackServer = deferredServer.FallbackServer;
            if (server is null)
            {
                server = fallbackServer ?? SvgPaintServer.NotSet;
                fallbackServer = null;
            }
        }

        if (server == SvgPaintServer.None)
        {
            return false;
        }

        if (server is SvgGradientServer or SvgPatternServer &&
            server is SvgElement serverElement &&
            !serverElement.PassesConditionalProcessing(ignoreAttributes))
        {
            return TryApplyFallbackPaintServer(
                svgVisualElement,
                fallbackServer,
                opacity,
                skBounds,
                skPaint,
                forStroke,
                assetLoader,
                ignoreAttributes,
                contextPaint,
                gradientPaintCache,
                contextPaintDepth,
                SKColorSpace.Srgb);
        }

        switch (server)
        {
            case SvgContextPaintServer svgContextPaintServer:
                return TryApplyContextPaintServer(
                    svgVisualElement,
                    svgContextPaintServer,
                    opacity,
                    skBounds,
                    skPaint,
                    forStroke,
                    assetLoader,
                    ignoreAttributes,
                    contextPaint,
                    gradientPaintCache,
                    contextPaintDepth);

            case SvgColourServer svgColourServer:
                return TryApplyColor(svgVisualElement, svgColourServer, opacity, skPaint, ignoreAttributes);

            case SvgPatternServer svgPatternServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgVisualElement);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;
                    var skPatternShader = CreatePatternShader(svgPatternServer, skBounds, svgVisualElement, opacity, assetLoader, ignoreAttributes);
                    if (skPatternShader is not null)
                    {
                        skPaint.Shader = skPatternShader;
                        return true;
                    }

                    return TryApplyFallbackPaintServer(
                        svgVisualElement,
                        fallbackServer,
                        opacity,
                        skBounds,
                        skPaint,
                        forStroke,
                        assetLoader,
                        ignoreAttributes,
                        contextPaint,
                        gradientPaintCache,
                        contextPaintDepth,
                        skColorSpace);
                }

            case SvgLinearGradientServer svgLinearGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox &&
                        (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        return TryApplyFallbackPaintServer(
                            svgVisualElement,
                            fallbackServer,
                            opacity,
                            skBounds,
                            skPaint,
                            forStroke,
                            assetLoader,
                            ignoreAttributes,
                            contextPaint,
                            gradientPaintCache,
                            contextPaintDepth,
                            skColorSpace);
                    }

                    var shader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace, gradientPaintCache);
                    if (shader is null)
                    {
                        return TryApplyFallbackPaintServer(
                            svgVisualElement,
                            fallbackServer,
                            opacity,
                            skBounds,
                            skPaint,
                            forStroke,
                            assetLoader,
                            ignoreAttributes,
                            contextPaint,
                            gradientPaintCache,
                            contextPaintDepth,
                            skColorSpace);
                    }

                    skPaint.Shader = shader;
                    return true;
                }

            case SvgRadialGradientServer svgRadialGradientServer:
                {
                    var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                    var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;
                    var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;

                    if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox &&
                        (skBounds.Width == 0f || skBounds.Height == 0f))
                    {
                        return TryApplyFallbackPaintServer(
                            svgVisualElement,
                            fallbackServer,
                            opacity,
                            skBounds,
                            skPaint,
                            forStroke,
                            assetLoader,
                            ignoreAttributes,
                            contextPaint,
                            gradientPaintCache,
                            contextPaintDepth,
                            skColorSpace);
                    }

                    var shader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace, gradientPaintCache);
                    if (shader is null)
                    {
                        return TryApplyFallbackPaintServer(
                            svgVisualElement,
                            fallbackServer,
                            opacity,
                            skBounds,
                            skPaint,
                            forStroke,
                            assetLoader,
                            ignoreAttributes,
                            contextPaint,
                            gradientPaintCache,
                            contextPaintDepth,
                            skColorSpace);
                    }

                    skPaint.Shader = shader;
                    return true;
                }

            case SvgDeferredPaintServer svgDeferredPaintServer:
                return TryApplyPaintServer(
                    svgVisualElement,
                    svgDeferredPaintServer,
                    opacity,
                    skBounds,
                    skPaint,
                    forStroke,
                    assetLoader,
                    ignoreAttributes,
                    contextPaint,
                    gradientPaintCache,
                    contextPaintDepth);

            default:
                return false;
        }
    }

    private static bool TryApplyContextPaintServer(
        SvgVisualElement svgVisualElement,
        SvgContextPaintServer svgContextPaintServer,
        float opacity,
        SKRect skBounds,
        SKPaint skPaint,
        bool forStroke,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneContextPaint? contextPaint,
        GradientPaintCache? gradientPaintCache,
        int contextPaintDepth)
    {
        if (contextPaint is null ||
            ReferenceEquals(svgVisualElement, contextPaint.Element) ||
            contextPaintDepth >= 8)
        {
            return false;
        }

        var contextServer = svgContextPaintServer.Kind == SvgContextPaintKind.Stroke
            ? contextPaint.Element.Stroke
            : contextPaint.Element.Fill;
        var resolvedContextServer = SvgDeferredPaintServer.TryGet<SvgPaintServer>(contextServer, contextPaint.Element);
        var contextServerBounds = resolvedContextServer is SvgPatternServer
            ? SKRect.Create(0f, 0f, contextPaint.Bounds.Width, contextPaint.Bounds.Height)
            : contextPaint.Bounds;

        return TryApplyPaintServer(
            svgVisualElement,
            contextServer,
            opacity,
            contextServerBounds,
            skPaint,
            forStroke,
            assetLoader,
            ignoreAttributes,
            contextPaint.Parent,
            gradientPaintCache,
            contextPaintDepth + 1);
    }

    private static bool TryApplyColor(
        SvgVisualElement svgVisualElement,
        SvgColourServer svgColourServer,
        float opacity,
        SKPaint skPaint,
        DrawAttributes ignoreAttributes)
    {
        var skColor = GetColor(svgColourServer, opacity, ignoreAttributes);
        var colorInterpolation = GetColorInterpolation(svgVisualElement);
        var isLinearRgb = colorInterpolation == SvgColourInterpolation.LinearRGB;

        if (colorInterpolation == SvgColourInterpolation.SRGB)
        {
            skPaint.Color = skColor;
            skPaint.Shader = null;
            return true;
        }

        var skColorSpace = isLinearRgb ? SKColorSpace.SrgbLinear : SKColorSpace.Srgb;
        if (isLinearRgb)
        {
            skColor = ToLinear(skColor);
        }

        var shader = SKShader.CreateColor(skColor, skColorSpace);
        if (shader is null)
        {
            return false;
        }

        skPaint.Shader = shader;
        return true;
    }

    private static bool TryApplyFallbackColor(
        SvgPaintServer? fallbackServer,
        float opacity,
        SKPaint skPaint,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace)
    {
        if (fallbackServer is not SvgColourServer svgColourServerFallback)
        {
            return false;
        }

        var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
        if (skColorSpace == SKColorSpace.Srgb)
        {
            skPaint.Color = skColor;
            skPaint.Shader = null;
            return true;
        }

        if (skColorSpace == SKColorSpace.SrgbLinear)
        {
            skColor = ToLinear(skColor);
        }

        var shader = SKShader.CreateColor(skColor, skColorSpace);
        if (shader is null)
        {
            return false;
        }

        skPaint.Shader = shader;
        return true;
    }

    private static bool TryApplyFallbackPaintServer(
        SvgVisualElement svgVisualElement,
        SvgPaintServer? fallbackServer,
        float opacity,
        SKRect skBounds,
        SKPaint skPaint,
        bool forStroke,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneContextPaint? contextPaint,
        GradientPaintCache? gradientPaintCache,
        int contextPaintDepth,
        SKColorSpace skColorSpace)
    {
        if (fallbackServer is null || fallbackServer == SvgPaintServer.None || fallbackServer == SvgPaintServer.NotSet)
        {
            return false;
        }

        if (fallbackServer is SvgColourServer)
        {
            return TryApplyFallbackColor(fallbackServer, opacity, skPaint, ignoreAttributes, skColorSpace);
        }

        return TryApplyPaintServer(
            svgVisualElement,
            fallbackServer,
            opacity,
            skBounds,
            skPaint,
            forStroke,
            assetLoader,
            ignoreAttributes,
            contextPaint,
            gradientPaintCache,
            contextPaintDepth + 1);
    }

    private static SKColor GetColor(SvgColourServer svgColourServer, float opacity, DrawAttributes ignoreAttributes)
    {
        var colour = svgColourServer.Colour;
        var alpha = ignoreAttributes.Has(DrawAttributes.Opacity)
            ? svgColourServer.Colour.A
            : CombineWithOpacity(svgColourServer.Colour.A, opacity);

        return new SKColor(colour.R, colour.G, colour.B, alpha);
    }

    private static byte CombineWithOpacity(byte alpha, float opacity)
    {
        return (byte)Math.Round(opacity * (alpha / 255.0) * 255.0);
    }

    private static SKColor ToLinear(SKColor color)
    {
        var r = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Red / 255f) * 255f);
        var g = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Green / 255f) * 255f);
        var b = (byte)Math.Round(FilterEffectsService.SRGBToLinear(color.Blue / 255f) * 255f);
        return new SKColor(r, g, b, color.Alpha);
    }

    private static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
    {
        return svgElement.ColorInterpolation switch
        {
            SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
            _ => SvgColourInterpolation.SRGB,
        };
    }

    private static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds, SKPath? geometryPath)
    {
        var strokeDashArray = svgElement.StrokeDashArray;
        var strokeDashOffset = svgElement.StrokeDashOffset;
        var count = strokeDashArray.Count;

        if (strokeDashArray is null || count <= 0)
        {
            return null;
        }

        var isOdd = count % 2 != 0;
        var sum = 0f;
        var intervals = new float[isOdd ? count * 2 : count];
        var normalization = SvgGeometryService.CreatePathLengthNormalization(svgElement, geometryPath);
        for (var i = 0; i < count; i++)
        {
            var dash = normalization.ToActualDistance(strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds));
            if (dash < 0f)
            {
                return null;
            }

            intervals[i] = dash;
            if (isOdd)
            {
                intervals[i + count] = dash;
            }

            sum += dash;
        }

        if (sum <= 0f)
        {
            return null;
        }

        var phase = normalization.ToActualDistance(strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds));
        return SKPathEffect.CreateDash(intervals, phase);
    }

    private static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, SKRect skBounds, SKPath? geometryPath)
    {
        if (CreateDash(svgVisualElement, skBounds, geometryPath) is { } dash)
        {
            skPaint.PathEffect = dash;
        }
    }

    private static GradientServerChain GetLinkedGradientServers(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
    {
        if (!svgGradientServer.PassesConditionalProcessing(DrawAttributes.None))
        {
            return default;
        }

        if (!svgGradientServer.TryGetEffectiveHrefString(out _))
        {
            return new GradientServerChain(svgGradientServer);
        }

        var gradientServers = new List<SvgGradientServer>();
        var visited = new HashSet<SvgGradientServer>();
        var currentGradientServer = svgGradientServer;
        do
        {
            if (!visited.Add(currentGradientServer))
            {
                break;
            }

            if (!currentGradientServer.PassesConditionalProcessing(DrawAttributes.None))
            {
                break;
            }

            gradientServers.Add(currentGradientServer);
            currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
        } while (currentGradientServer is not null);

        return new GradientServerChain(gradientServers);
    }

    private static bool TryCreateGradientStops(
        SvgGradientServer rootGradientServer,
        GradientServerChain svgReferencedGradientServers,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb,
        GradientPaintCache? gradientPaintCache,
        out SKColor singleColor,
        out SKColorF[]? colors,
        out float[]? colorPos)
    {
        singleColor = default;
        colors = null;
        colorPos = null;

        if (gradientPaintCache is not null &&
            gradientPaintCache.TryGetStops(rootGradientServer, opacity, ignoreAttributes, isLinearRgb, out var cachedEntry))
        {
            return TryGetCachedGradientStops(cachedEntry, out singleColor, out colors, out colorPos);
        }

        for (var i = 0; i < svgReferencedGradientServers.Count; i++)
        {
            var svgReferencedGradientServer = svgReferencedGradientServers[i];
            var stops = svgReferencedGradientServer.Stops;
            if (stops.Count == 0)
            {
                continue;
            }

            if (stops.Count == 1)
            {
                if (TryGetGradientStopColor(stops[0], opacity, ignoreAttributes, isLinearRgb, out singleColor))
                {
                    gradientPaintCache?.SetStops(
                        rootGradientServer,
                        opacity,
                        ignoreAttributes,
                        isLinearRgb,
                        new GradientStopCacheEntry(singleColor));
                    return true;
                }

                continue;
            }

            colors = new SKColorF[stops.Count];
            colorPos = new float[stops.Count];
            var stopCount = FillGradientStops(stops, opacity, ignoreAttributes, isLinearRgb, colors, colorPos);
            if (stopCount == 0)
            {
                colors = null;
                colorPos = null;
                continue;
            }

            if (stopCount == 1)
            {
                singleColor = (SKColor)colors[0];
                colors = null;
                colorPos = null;
                gradientPaintCache?.SetStops(
                    rootGradientServer,
                    opacity,
                    ignoreAttributes,
                    isLinearRgb,
                    new GradientStopCacheEntry(singleColor));
                return true;
            }

            if (stopCount != stops.Count)
            {
                Array.Resize(ref colors, stopCount);
                Array.Resize(ref colorPos, stopCount);
            }

            AdjustStopColorPos(colorPos);
            gradientPaintCache?.SetStops(
                rootGradientServer,
                opacity,
                ignoreAttributes,
                isLinearRgb,
                new GradientStopCacheEntry(colors, colorPos));
            return true;
        }

        gradientPaintCache?.SetStops(
            rootGradientServer,
            opacity,
            ignoreAttributes,
            isLinearRgb,
            GradientStopCacheEntry.Empty);
        return false;
    }

    private static bool TryGetCachedGradientStops(
        GradientStopCacheEntry entry,
        out SKColor singleColor,
        out SKColorF[]? colors,
        out float[]? colorPos)
    {
        singleColor = entry.SingleColor;
        colors = entry.Colors;
        colorPos = entry.ColorPos;
        return entry.HasStops;
    }

    private static int FillGradientStops(
        List<SvgGradientStop> stops,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb,
        SKColorF[] colors,
        float[] colorPos)
    {
        var index = 0;
        for (var i = 0; i < stops.Count; i++)
        {
            var svgGradientStop = stops[i];
            if (!TryGetGradientStopColor(svgGradientStop, opacity, ignoreAttributes, isLinearRgb, out var stopColor))
            {
                continue;
            }

            colors[index] = stopColor;
            colorPos[index] = GetGradientStopOffset(svgGradientStop);
            index++;
        }

        return index;
    }

    private static bool TryGetGradientStopColor(
        SvgGradientStop svgGradientStop,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb,
        out SKColor color)
    {
        color = default;
        if (!TryGetGradientStopColorServer(svgGradientStop, ignoreAttributes, out var stopColorSvgColourServer))
        {
            return false;
        }

        color = CreateGradientStopColor(svgGradientStop, stopColorSvgColourServer, opacity, ignoreAttributes, isLinearRgb);
        return true;
    }

    private static bool TryGetGradientStopColorServer(
        SvgGradientStop svgGradientStop,
        DrawAttributes ignoreAttributes,
        out SvgColourServer stopColorSvgColourServer)
    {
        stopColorSvgColourServer = null!;

        if (!svgGradientStop.PassesConditionalProcessing(ignoreAttributes))
        {
            return false;
        }

        var server = svgGradientStop.StopColor;
        if (server is SvgDeferredPaintServer svgDeferredPaintServer)
        {
            // Match the model-path behavior: stop-level currentColor/inherit must resolve
            // against the gradient definition tree rather than the referencing element.
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgGradientStop);
        }

        if (server is not SvgColourServer colorServer)
        {
            return false;
        }

        stopColorSvgColourServer = colorServer;
        return true;
    }

    private static SKColor CreateGradientStopColor(
        SvgGradientStop svgGradientStop,
        SvgColourServer stopColorSvgColourServer,
        float opacity,
        DrawAttributes ignoreAttributes,
        bool isLinearRgb)
    {
        var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
        var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
        return isLinearRgb ? ToLinear(stopColor) : stopColor;
    }

    private static void AdjustStopColorPos(float[] colorPos)
    {
        var maxPos = float.MinValue;
        for (var i = 0; i < colorPos.Length; i++)
        {
            var pos = colorPos[i];
            if (pos > maxPos)
            {
                maxPos = pos;
            }
            else if (pos < maxPos)
            {
                colorPos[i] = maxPos;
            }
        }
    }

    private static float GetGradientStopOffset(SvgGradientStop svgGradientStop)
    {
        var offset = svgGradientStop.Offset;
        var value = offset.Type == SvgUnitType.Percentage ? offset.Value / 100f : offset.Value;
        if (float.IsNaN(value) || float.IsNegativeInfinity(value))
        {
            return 0f;
        }

        if (float.IsPositiveInfinity(value))
        {
            return 1f;
        }

        return Math.Min(Math.Max(value, 0f), 1f);
    }

    private static SKShader? CreateLinearGradient(
        SvgLinearGradientServer svgLinearGradientServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace,
        GradientPaintCache? gradientPaintCache)
    {
        var svgReferencedGradientServers = GetLinkedGradientServers(svgLinearGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = null;
        SvgGradientServer? firstGradientTransform = null;
        SvgGradientServer? firstGradientUnits = null;
        SvgLinearGradientServer? firstX1 = null;
        SvgLinearGradientServer? firstY1 = null;
        SvgLinearGradientServer? firstX2 = null;
        SvgLinearGradientServer? firstY2 = null;

        for (var i = 0; i < svgReferencedGradientServers.Count; i++)
        {
            var p = svgReferencedGradientServers[i];
            if (firstSpreadMethod is null && SvgService.TryGetAttribute(p, "spreadMethod", out _))
            {
                firstSpreadMethod = p;
            }

            if (firstGradientTransform is null && p.GradientTransform is { Count: > 0 })
            {
                firstGradientTransform = p;
            }

            if (firstGradientUnits is null && SvgService.TryGetAttribute(p, "gradientUnits", out _))
            {
                firstGradientUnits = p;
            }

            if (p is not SvgLinearGradientServer gradientServerHref)
            {
                continue;
            }

            if (firstX1 is null && gradientServerHref.X1 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "x1", out _))
            {
                firstX1 = gradientServerHref;
            }

            if (firstY1 is null && gradientServerHref.Y1 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "y1", out _))
            {
                firstY1 = gradientServerHref;
            }

            var x2UnitCandidate = gradientServerHref.X2;
            if (firstX2 is null && x2UnitCandidate != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "x2", out _))
            {
                firstX2 = gradientServerHref;
            }

            if (firstY2 is null && gradientServerHref.Y2 != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "y2", out _))
            {
                firstY2 = gradientServerHref;
            }
        }

        var svgSpreadMethod = firstSpreadMethod?.SpreadMethod ?? SvgGradientSpreadMethod.Pad;
        var svgGradientTransform = firstGradientTransform?.GradientTransform;
        var svgGradientUnits = firstGradientUnits?.GradientUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var x1Unit = firstX1?.X1 ?? new SvgUnit(SvgUnitType.Percentage, 0f);
        var y1Unit = firstY1?.Y1 ?? new SvgUnit(SvgUnitType.Percentage, 0f);
        var x2Unit = firstX2?.X2 ?? new SvgUnit(SvgUnitType.Percentage, 100f);
        var y2Unit = firstY2?.Y2 ?? new SvgUnit(SvgUnitType.Percentage, 0f);

        var normalizedX1 = x1Unit.Normalize(svgGradientUnits);
        var normalizedY1 = y1Unit.Normalize(svgGradientUnits);
        var normalizedX2 = x2Unit.Normalize(svgGradientUnits);
        var normalizedY2 = y2Unit.Normalize(svgGradientUnits);

        var x1 = normalizedX1.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
        var y1 = normalizedY1.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);
        var x2 = normalizedX2.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
        var y2 = normalizedY2.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);

        if (!IsFinite(x1) || !IsFinite(y1) || !IsFinite(x2) || !IsFinite(y2))
        {
            return null;
        }

        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        if (!TryCreateGradientStops(
                svgLinearGradientServer,
                svgReferencedGradientServers,
                opacity,
                ignoreAttributes,
                isLinearRgb,
                gradientPaintCache,
                out var singleColor,
                out var skColorsF,
                out var skColorPos))
        {
            return null;
        }

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp
        };

        if (skColorsF is null)
        {
            return SKShader.CreateColor(singleColor, skColorSpace);
        }

        var skStart = new SKPoint(x1, y1);
        var skEnd = new SKPoint(x2, y2);

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp2 = 1
            };

            if (svgGradientTransform is { Count: > 0 })
            {
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(TransformsService.ToMatrix(svgGradientTransform));
            }

            return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
        }

        if (svgGradientTransform is { Count: > 0 })
        {
            return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, TransformsService.ToMatrix(svgGradientTransform));
        }

        return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
    }

    private static SKShader? CreateTwoPointConicalGradient(
        SvgRadialGradientServer svgRadialGradientServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        DrawAttributes ignoreAttributes,
        SKColorSpace skColorSpace,
        GradientPaintCache? gradientPaintCache)
    {
        var svgReferencedGradientServers = GetLinkedGradientServers(svgRadialGradientServer, svgVisualElement);

        SvgGradientServer? firstSpreadMethod = null;
        SvgGradientServer? firstGradientTransform = null;
        SvgGradientServer? firstGradientUnits = null;
        SvgRadialGradientServer? firstCenterX = null;
        SvgRadialGradientServer? firstCenterY = null;
        SvgRadialGradientServer? firstRadius = null;
        SvgRadialGradientServer? firstFocalX = null;
        SvgRadialGradientServer? firstFocalY = null;
        SvgRadialGradientServer? firstFocalRadius = null;

        for (var i = 0; i < svgReferencedGradientServers.Count; i++)
        {
            var p = svgReferencedGradientServers[i];
            if (firstSpreadMethod is null && SvgService.TryGetAttribute(p, "spreadMethod", out _))
            {
                firstSpreadMethod = p;
            }

            if (firstGradientTransform is null && p.GradientTransform is { Count: > 0 })
            {
                firstGradientTransform = p;
            }

            if (firstGradientUnits is null && SvgService.TryGetAttribute(p, "gradientUnits", out _))
            {
                firstGradientUnits = p;
            }

            if (p is not SvgRadialGradientServer gradientServerHref)
            {
                continue;
            }

            if (firstCenterX is null && gradientServerHref.CenterX != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "cx", out _))
            {
                firstCenterX = gradientServerHref;
            }

            if (firstCenterY is null && gradientServerHref.CenterY != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "cy", out _))
            {
                firstCenterY = gradientServerHref;
            }

            if (firstRadius is null && gradientServerHref.Radius != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "r", out _))
            {
                firstRadius = gradientServerHref;
            }

            if (firstFocalX is null && gradientServerHref.FocalX != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "fx", out _))
            {
                firstFocalX = gradientServerHref;
            }

            if (firstFocalY is null && gradientServerHref.FocalY != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "fy", out _))
            {
                firstFocalY = gradientServerHref;
            }

            if (firstFocalRadius is null && gradientServerHref.FocalRadius != SvgUnit.None && SvgService.TryGetAttribute(gradientServerHref, "fr", out _))
            {
                firstFocalRadius = gradientServerHref;
            }
        }

        var svgSpreadMethod = firstSpreadMethod?.SpreadMethod ?? SvgGradientSpreadMethod.Pad;
        var svgGradientTransform = firstGradientTransform?.GradientTransform;
        var svgGradientUnits = firstGradientUnits?.GradientUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var centerXUnit = firstCenterX?.CenterX ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var centerYUnit = firstCenterY?.CenterY ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var radiusUnit = firstRadius?.Radius ?? new SvgUnit(SvgUnitType.Percentage, 50f);
        var focalXUnit = firstFocalX?.FocalX ?? centerXUnit;
        var focalYUnit = firstFocalY?.FocalY ?? centerYUnit;
        var focalRadiusUnit = firstFocalRadius?.FocalRadius ?? new SvgUnit(SvgUnitType.Percentage, 0f);

        var centerX = centerXUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var centerY = centerYUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);
        var radius = radiusUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);
        var focalX = focalXUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
        var focalY = focalYUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);
        var focalRadius = focalRadiusUnit.Normalize(svgGradientUnits).ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

        if (!IsFinite(centerX) || !IsFinite(centerY) || !IsFinite(radius) ||
            !IsFinite(focalX) || !IsFinite(focalY) || !IsFinite(focalRadius) ||
            radius < 0f)
        {
            return null;
        }

        var isLinearRgb = skColorSpace == SKColorSpace.SrgbLinear;
        if (!TryCreateGradientStops(
                svgRadialGradientServer,
                svgReferencedGradientServers,
                opacity,
                ignoreAttributes,
                isLinearRgb,
                gradientPaintCache,
                out var singleColor,
                out var skColorsF,
                out var skColorPos))
        {
            return null;
        }

        var shaderTileMode = svgSpreadMethod switch
        {
            SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp
        };

        if (skColorsF is null)
        {
            return SKShader.CreateColor(singleColor, skColorSpace);
        }

        if (radius == 0f)
        {
            return SKShader.CreateColor((SKColor)skColorsF[skColorsF.Length - 1], skColorSpace);
        }

        var skCenter = new SKPoint(centerX, centerY);
        var skFocal = new SKPoint(focalX, focalY);
        focalRadius = Math.Max(0f, focalRadius);
        if (svgGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
        {
            skFocal = PaintingService.CorrectRadialGradientFocalPoint(skCenter, radius, skFocal, focalRadius);
        }

        var isRadialGradient = focalRadius == 0f && skCenter.X == skFocal.X && skCenter.Y == skFocal.Y;

        if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundingBoxTransform = new SKMatrix
            {
                ScaleX = skBounds.Width,
                ScaleY = skBounds.Height,
                TransX = skBounds.Left,
                TransY = skBounds.Top,
                Persp2 = 1
            };

            if (svgGradientTransform is { Count: > 0 })
            {
                skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(TransformsService.ToMatrix(svgGradientTransform));
            }

            return isRadialGradient
                ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform)
                : SKShader.CreateTwoPointConicalGradient(skFocal, focalRadius, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
        }

        if (svgGradientTransform is { Count: > 0 })
        {
            var gradientTransform = TransformsService.ToMatrix(svgGradientTransform);
            return isRadialGradient
                ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform)
                : SKShader.CreateTwoPointConicalGradient(skFocal, focalRadius, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
        }

        return isRadialGradient
            ? SKShader.CreateRadialGradient(skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode)
            : SKShader.CreateTwoPointConicalGradient(skFocal, focalRadius, skCenter, radius, skColorsF, skColorSpace, skColorPos, shaderTileMode);
    }

    private static SKShader? CreatePatternShader(
        SvgPatternServer svgPatternServer,
        SKRect skBounds,
        SvgVisualElement svgVisualElement,
        float opacity,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        if (!SvgPatternPaintStateResolver.TryCreate(svgPatternServer, svgVisualElement, skBounds, out var patternState) ||
            patternState is null)
        {
            return null;
        }

        if (IsActivePattern(svgPatternServer) || IsActivePattern(patternState.ContentSource))
        {
            return null;
        }

        using var activePatternScope = PushActivePattern(svgPatternServer, patternState.ContentSource);
        var patternScene = SvgSceneCompiler.CompileTemporaryChildrenScene(
            patternState.ContentSource,
            patternState.Children,
            patternState.PictureCullRect,
            patternState.PictureViewport,
            patternState.PictureTransform,
            opacity,
            assetLoader,
            ignoreAttributes);
        if (patternScene is null)
        {
            return null;
        }

        if (patternState.ClipTile)
        {
            patternScene.Root.Overflow = patternState.TileClip;
        }

        var picture = SvgSceneRenderer.Render(patternScene);
        if (picture is not null && !patternState.ClipTile)
        {
            picture = CreateVisibleOverflowPatternPicture(picture, patternState);
        }

        return picture is null
            ? null
            : SKShader.CreatePicture(picture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, patternState.ShaderMatrix, patternState.ShaderTile);
    }

    private static SKPicture CreateVisibleOverflowPatternPicture(SKPicture picture, SvgPatternPaintState patternState)
    {
        var tile = patternState.ShaderTile;
        var sourceBounds = picture.CullRect;
        if (!HasPositiveArea(tile) || !HasPositiveArea(sourceBounds))
        {
            return picture;
        }

        var minX = GetRepeatStart(tile.Left, sourceBounds.Right, tile.Width);
        var maxX = GetRepeatEnd(tile.Right, sourceBounds.Left, tile.Width);
        var minY = GetRepeatStart(tile.Top, sourceBounds.Bottom, tile.Height);
        var maxY = GetRepeatEnd(tile.Bottom, sourceBounds.Top, tile.Height);
        if (IsRepeatSearchTooLarge(minX, maxX, minY, maxY))
        {
            return picture;
        }

        var repeatCount = CountIntersectingRepeats(sourceBounds, tile, tile.Width, tile.Height, minX, maxX, minY, maxY);
        if (repeatCount == 0 ||
            (repeatCount == 1 && Intersects(sourceBounds, tile)) ||
            repeatCount > MaxVisiblePatternOverflowCopies)
        {
            return picture;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(tile);
        for (var y = minY; y <= maxY; y++)
        {
            var offsetY = y * tile.Height;
            for (var x = minX; x <= maxX; x++)
            {
                var offsetX = x * tile.Width;
                var shiftedBounds = OffsetRect(sourceBounds, offsetX, offsetY);
                if (!Intersects(shiftedBounds, tile))
                {
                    continue;
                }

                if (x == 0 && y == 0)
                {
                    canvas.DrawPicture(picture);
                    continue;
                }

                canvas.Save();
                canvas.SetMatrix(SKMatrix.CreateTranslation(offsetX, offsetY));
                canvas.DrawPicture(picture);
                canvas.Restore();
            }
        }

        return recorder.EndRecording();
    }

    private static int CountIntersectingRepeats(
        SKRect sourceBounds,
        SKRect tile,
        float stepX,
        float stepY,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        var count = 0;
        for (var y = minY; y <= maxY; y++)
        {
            var offsetY = y * stepY;
            for (var x = minX; x <= maxX; x++)
            {
                var offsetX = x * stepX;
                if (Intersects(OffsetRect(sourceBounds, offsetX, offsetY), tile))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int GetRepeatStart(float tileStart, float sourceEnd, float step)
        => (int)Math.Floor((tileStart - sourceEnd) / step);

    private static int GetRepeatEnd(float tileEnd, float sourceStart, float step)
        => (int)Math.Ceiling((tileEnd - sourceStart) / step);

    private static bool IsRepeatSearchTooLarge(int minX, int maxX, int minY, int maxY)
    {
        var width = (long)maxX - minX + 1;
        var height = (long)maxY - minY + 1;
        return width <= 0 ||
               height <= 0 ||
               width * height > MaxVisiblePatternOverflowCopies;
    }

    private static SKRect OffsetRect(SKRect rect, float x, float y)
        => new(rect.Left + x, rect.Top + y, rect.Right + x, rect.Bottom + y);

    private static bool Intersects(SKRect a, SKRect b)
        => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool IsActivePattern(SvgPatternServer svgPatternServer)
        => s_activePatternServers?.Contains(svgPatternServer) == true;

    private static bool HasPositiveArea(SKRect rect)
        => rect.Width > 0f && rect.Height > 0f;

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private static ActivePatternScope PushActivePattern(SvgPatternServer svgPatternServer, SvgPatternServer contentSource)
    {
        s_activePatternServers ??= new HashSet<SvgPatternServer>();
        var addedPattern = s_activePatternServers.Add(svgPatternServer);
        var addedContentSource = !ReferenceEquals(svgPatternServer, contentSource) && s_activePatternServers.Add(contentSource);
        return new ActivePatternScope(svgPatternServer, contentSource, addedPattern, addedContentSource);
    }

    private readonly struct ActivePatternScope : IDisposable
    {
        private readonly SvgPatternServer _svgPatternServer;
        private readonly SvgPatternServer _contentSource;
        private readonly bool _addedPattern;
        private readonly bool _addedContentSource;

        public ActivePatternScope(
            SvgPatternServer svgPatternServer,
            SvgPatternServer contentSource,
            bool addedPattern,
            bool addedContentSource)
        {
            _svgPatternServer = svgPatternServer;
            _contentSource = contentSource;
            _addedPattern = addedPattern;
            _addedContentSource = addedContentSource;
        }

        public void Dispose()
        {
            if (s_activePatternServers is null)
            {
                return;
            }

            if (_addedContentSource)
            {
                s_activePatternServers.Remove(_contentSource);
            }

            if (_addedPattern)
            {
                s_activePatternServers.Remove(_svgPatternServer);
            }

            if (s_activePatternServers.Count == 0)
            {
                s_activePatternServers = null;
            }
        }
    }
}
