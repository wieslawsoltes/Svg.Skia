// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;
using Svg.Ast;

namespace Svg.Model.Ast;

internal sealed class SvgAstRenderState
{
    public bool Display { get; set; } = true;

    public bool Visible { get; set; } = true;

    public SKColor? FillColor { get; set; } = new SKColor(0x00, 0x00, 0x00, 0xFF);

    public float FillOpacity { get; set; } = 1f;

    public string? FillPaintId { get; set; }

    public SKColor? StrokeColor { get; set; }

    public float StrokeOpacity { get; set; } = 1f;

    public string? StrokePaintId { get; set; }

    public float StrokeWidth { get; set; } = 1f;

    public float Opacity { get; set; } = 1f;

    public SKStrokeCap StrokeCap { get; set; } = SKStrokeCap.Butt;

    public SKStrokeJoin StrokeJoin { get; set; } = SKStrokeJoin.Miter;

    public string? MaskId { get; set; }

    public string? ClipPathId { get; set; }

    public float FontSize { get; set; } = 16f;

    public SKTextAlign TextAlign { get; set; } = SKTextAlign.Left;

    public SvgAstRenderState Clone()
    {
        return new SvgAstRenderState
        {
            Display = Display,
            Visible = Visible,
            FillColor = FillColor,
            FillOpacity = FillOpacity,
            FillPaintId = FillPaintId,
            StrokeColor = StrokeColor,
            StrokeOpacity = StrokeOpacity,
            StrokePaintId = StrokePaintId,
            StrokeWidth = StrokeWidth,
            Opacity = Opacity,
            StrokeCap = StrokeCap,
            StrokeJoin = StrokeJoin,
            MaskId = MaskId,
            ClipPathId = ClipPathId,
            FontSize = FontSize,
            TextAlign = TextAlign
        };
    }

    public SKPaint? CreateFillPaint(SvgAstPaintServerResolver resolver, SKRect geometryBounds, bool antialias)
        => CreatePaint(resolver, geometryBounds, antialias, isStroke: false);

    public SKPaint? CreateStrokePaint(SvgAstPaintServerResolver resolver, SKRect geometryBounds, bool antialias)
        => CreatePaint(resolver, geometryBounds, antialias, isStroke: true);

    public SKPaint? CreateTextPaint(SvgAstPaintServerResolver resolver, SKRect geometryBounds, bool antialias)
    {
        var paint = CreateFillPaint(resolver, geometryBounds, antialias);
        if (paint is null)
        {
            return null;
        }

        paint.TextSize = FontSize;
        paint.TextAlign = TextAlign;
        paint.TextEncoding = SKTextEncoding.Utf8;
        paint.LcdRenderText = true;
        paint.SubpixelText = true;
        return paint;
    }

    private SKPaint? CreatePaint(SvgAstPaintServerResolver resolver, SKRect geometryBounds, bool antialias, bool isStroke)
    {
        if (!Display || !Visible)
        {
            return null;
        }

        var paintId = isStroke ? StrokePaintId : FillPaintId;
        var baseColor = isStroke ? StrokeColor : FillColor;
        var opacity = SvgAstRenderUtilities.Clamp01((isStroke ? StrokeOpacity : FillOpacity) * Opacity);

        if (opacity <= 0 && string.IsNullOrEmpty(paintId))
        {
            return null;
        }

        var paint = new SKPaint
        {
            Style = isStroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
            IsAntialias = antialias,
            StrokeWidth = StrokeWidth,
            StrokeCap = StrokeCap,
            StrokeJoin = StrokeJoin
        };

        if (!string.IsNullOrEmpty(paintId))
        {
            var shader = resolver.TryCreateShader(paintId!, geometryBounds);
            if (shader is not null)
            {
                paint.Shader = shader;
            }
            else if (baseColor is null)
            {
                return null;
            }
        }

        if (baseColor is not null)
        {
            paint.Color = SvgAstRenderUtilities.ApplyOpacity(baseColor.Value, opacity);
        }

        return paint.Shader is null && paint.Color is null ? null : paint;
    }
}

internal static class SvgAstStyleHelper
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, SKStrokeCap> s_strokeCaps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["butt"] = SKStrokeCap.Butt,
        ["round"] = SKStrokeCap.Round,
        ["square"] = SKStrokeCap.Square
    };

    private static readonly Dictionary<string, SKStrokeJoin> s_strokeJoins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["miter"] = SKStrokeJoin.Miter,
        ["round"] = SKStrokeJoin.Round,
        ["bevel"] = SKStrokeJoin.Bevel
    };

    public static void ApplyStyles(SvgAstElement element, SvgAstRenderState state)
    {
        var styleMap = SvgAstRenderUtilities.ParseStyleAttribute(element);
        Apply(styleMap, state);
        ApplyPresentationAttribute(element, "fill", state);
        ApplyPresentationAttribute(element, "fill-opacity", state);
        ApplyPresentationAttribute(element, "stroke", state);
        ApplyPresentationAttribute(element, "stroke-opacity", state);
        ApplyPresentationAttribute(element, "stroke-width", state);
        ApplyPresentationAttribute(element, "stroke-linecap", state);
        ApplyPresentationAttribute(element, "stroke-linejoin", state);
        ApplyPresentationAttribute(element, "opacity", state);
        ApplyPresentationAttribute(element, "display", state);
        ApplyPresentationAttribute(element, "visibility", state);
        ApplyPresentationAttribute(element, "mask", state);
        ApplyPresentationAttribute(element, "clip-path", state);
    }

    private static void ApplyPresentationAttribute(SvgAstElement element, string name, SvgAstRenderState state)
    {
        if (element.TryGetAttribute(name, out var attribute))
        {
            Apply(name, attribute.GetValueText(), state);
        }
    }

    private static void Apply(IReadOnlyDictionary<string, string> styleMap, SvgAstRenderState state)
    {
        foreach (var pair in styleMap)
        {
            Apply(pair.Key, pair.Value, state);
        }
    }

    private static void Apply(string name, string? value, SvgAstRenderState state)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var propertyValue = value!;

        switch (name.ToLowerInvariant())
        {
            case "fill":
                if (SvgAstRenderUtilities.TryExtractUrlReference(propertyValue, out var fillReference))
                {
                    state.FillPaintId = fillReference;
                    state.FillColor = null;
                }
                else
                {
                    state.FillPaintId = null;
                    state.FillColor = SvgAstColorParser.TryParse(propertyValue, out var fillColor) ? fillColor : state.FillColor;
                    if (string.Equals(propertyValue, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        state.FillColor = null;
                    }
                }
                break;
            case "fill-opacity":
                if (float.TryParse(propertyValue, NumberStyles.Float, s_invariant, out var fillOpacity))
                {
                    state.FillOpacity = SvgAstRenderUtilities.Clamp01(fillOpacity);
                }
                break;
            case "stroke":
                if (SvgAstRenderUtilities.TryExtractUrlReference(propertyValue, out var strokeReference))
                {
                    state.StrokePaintId = strokeReference;
                    state.StrokeColor = null;
                }
                else
                {
                    state.StrokePaintId = null;
                    state.StrokeColor = SvgAstColorParser.TryParse(propertyValue, out var strokeColor) ? strokeColor : state.StrokeColor;
                    if (string.Equals(propertyValue, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        state.StrokeColor = null;
                    }
                }
                break;
            case "stroke-opacity":
                if (float.TryParse(propertyValue, NumberStyles.Float, s_invariant, out var strokeOpacity))
                {
                    state.StrokeOpacity = SvgAstRenderUtilities.Clamp01(strokeOpacity);
                }
                break;
            case "stroke-width":
                if (SvgAstRenderUtilities.TryParseNumber(propertyValue, out var strokeWidth))
                {
                    state.StrokeWidth = Math.Max(strokeWidth, 0f);
                }
                break;
            case "stroke-linecap":
                if (s_strokeCaps.TryGetValue(propertyValue.Trim(), out var cap))
                {
                    state.StrokeCap = cap;
                }
                break;
            case "stroke-linejoin":
                if (s_strokeJoins.TryGetValue(propertyValue.Trim(), out var join))
                {
                    state.StrokeJoin = join;
                }
                break;
            case "opacity":
                if (float.TryParse(propertyValue, NumberStyles.Float, s_invariant, out var opacity))
                {
                    state.Opacity = SvgAstRenderUtilities.Clamp01(opacity);
                }
                break;
            case "display":
                state.Display = !string.Equals(propertyValue.Trim(), "none", StringComparison.OrdinalIgnoreCase);
                break;
            case "visibility":
                state.Visible = !string.Equals(propertyValue.Trim(), "hidden", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(propertyValue.Trim(), "collapse", StringComparison.OrdinalIgnoreCase);
                break;
            case "mask":
                if (SvgAstRenderUtilities.TryExtractUrlReference(propertyValue, out var maskReference))
                {
                    state.MaskId = maskReference;
                }
                break;
            case "clip-path":
                if (SvgAstRenderUtilities.TryExtractUrlReference(propertyValue, out var clipReference))
                {
                    state.ClipPathId = clipReference;
                }
                break;
            case "font-size":
                if (SvgAstRenderUtilities.TryParseNumber(propertyValue, out var fontSize) && fontSize > 0)
                {
                    state.FontSize = fontSize;
                }
                break;
            case "text-anchor":
                var anchor = propertyValue.Trim().ToLowerInvariant();
                state.TextAlign = anchor switch
                {
                    "middle" => SKTextAlign.Center,
                    "end" => SKTextAlign.Right,
                    "right" => SKTextAlign.Right,
                    _ => SKTextAlign.Left
                };
                break;
        }
    }
}
