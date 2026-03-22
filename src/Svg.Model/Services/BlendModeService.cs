// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using ShimSkiaSharp;

namespace Svg.Model.Services;

public static class BlendModeService
{
    public const string CssBlendModeAttribute = "mix-blend-mode";
    public const string EditorBlendModeAttribute = "data-svgskia-blend-mode";
    public const string PassThroughToken = "pass-through";
    public const string NormalToken = "normal";
    public const string DarkenToken = "darken";
    public const string MultiplyToken = "multiply";
    public const string PlusDarkerToken = "plus-darker";
    public const string ColorBurnToken = "color-burn";
    public const string LightenToken = "lighten";
    public const string ScreenToken = "screen";
    public const string PlusLighterToken = "plus-lighter";
    public const string ColorDodgeToken = "color-dodge";
    public const string OverlayToken = "overlay";
    public const string SoftLightToken = "soft-light";
    public const string HardLightToken = "hard-light";
    public const string DifferenceToken = "difference";
    public const string ExclusionToken = "exclusion";
    public const string HueToken = "hue";
    public const string SaturationToken = "saturation";
    public const string ColorToken = "color";
    public const string LuminosityToken = "luminosity";

    public static string? GetBlendModeToken(SvgElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (element.CustomAttributes.TryGetValue(EditorBlendModeAttribute, out var editorValue))
        {
            var normalizedEditorValue = NormalizeToken(editorValue);
            if (normalizedEditorValue is not null)
            {
                return normalizedEditorValue;
            }
        }

        return null;
    }

    public static void SetBlendModeToken(SvgElement element, string? token)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        var normalizedToken = NormalizeToken(token);
        if (normalizedToken is null)
        {
            ClearBlendModeToken(element);
            return;
        }

        element.CustomAttributes[EditorBlendModeAttribute] = normalizedToken;

        if (string.Equals(normalizedToken, PassThroughToken, StringComparison.Ordinal))
        {
            element.CustomAttributes.Remove(CssBlendModeAttribute);
            return;
        }

        element.CustomAttributes[CssBlendModeAttribute] = normalizedToken;
    }

    public static void ClearBlendModeToken(SvgElement element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.CustomAttributes.Remove(EditorBlendModeAttribute);
        element.CustomAttributes.Remove(CssBlendModeAttribute);
    }

    public static SKPaint? GetBlendPaint(SvgElement? element)
    {
        var blendMode = ToBlendMode(GetBlendModeToken(element));
        if (blendMode is null)
        {
            return null;
        }

        return new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 255),
            Style = SKPaintStyle.StrokeAndFill,
            BlendMode = blendMode.Value
        };
    }

    public static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token!.Trim().ToLowerInvariant()
            .Replace('_', '-')
            .Replace(' ', '-');

        return normalized switch
        {
            "passthrough" or PassThroughToken => PassThroughToken,
            NormalToken => NormalToken,
            DarkenToken => DarkenToken,
            MultiplyToken => MultiplyToken,
            PlusDarkerToken or "plusdarker" => PlusDarkerToken,
            ColorBurnToken or "colorburn" => ColorBurnToken,
            LightenToken => LightenToken,
            ScreenToken => ScreenToken,
            PlusLighterToken or "pluslighter" => PlusLighterToken,
            ColorDodgeToken or "colordodge" => ColorDodgeToken,
            OverlayToken => OverlayToken,
            SoftLightToken or "softlight" => SoftLightToken,
            HardLightToken or "hardlight" => HardLightToken,
            DifferenceToken => DifferenceToken,
            ExclusionToken => ExclusionToken,
            HueToken => HueToken,
            SaturationToken => SaturationToken,
            ColorToken => ColorToken,
            LuminosityToken => LuminosityToken,
            _ => null
        };
    }

    public static SKBlendMode? ToBlendMode(string? token)
    {
        return NormalizeToken(token) switch
        {
            null or PassThroughToken => null,
            NormalToken => SKBlendMode.SrcOver,
            DarkenToken => SKBlendMode.Darken,
            MultiplyToken => SKBlendMode.Multiply,
            PlusDarkerToken => SKBlendMode.Darken,
            ColorBurnToken => SKBlendMode.ColorBurn,
            LightenToken => SKBlendMode.Lighten,
            ScreenToken => SKBlendMode.Screen,
            PlusLighterToken => SKBlendMode.Plus,
            ColorDodgeToken => SKBlendMode.ColorDodge,
            OverlayToken => SKBlendMode.Overlay,
            SoftLightToken => SKBlendMode.SoftLight,
            HardLightToken => SKBlendMode.HardLight,
            DifferenceToken => SKBlendMode.Difference,
            ExclusionToken => SKBlendMode.Exclusion,
            HueToken => SKBlendMode.Hue,
            SaturationToken => SKBlendMode.Saturation,
            ColorToken => SKBlendMode.Color,
            LuminosityToken => SKBlendMode.Luminosity,
            _ => null
        };
    }
}
