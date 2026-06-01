// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;

namespace Svg.Model.Services;

internal static class MaskingService
{
    internal static bool CanDraw(SvgVisualElement svgVisualElement, DrawAttributes ignoreAttributes)
    {
        return IsVisible(svgVisualElement, ignoreAttributes) &&
               IsDisplayRendered(svgVisualElement, ignoreAttributes);
    }

    internal static bool IsVisible(SvgVisualElement svgVisualElement, DrawAttributes ignoreAttributes)
    {
        return ignoreAttributes.HasFlag(DrawAttributes.Visibility) || svgVisualElement.Visible;
    }

    internal static bool IsDisplayRendered(SvgVisualElement svgVisualElement, DrawAttributes ignoreAttributes)
    {
        return ignoreAttributes.HasFlag(DrawAttributes.Display) ||
               !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static SvgFillRule ToFillRule(SvgVisualElement svgVisualElement, SvgClipRule? svgClipPathClipRule)
    {
        var svgClipRule = svgClipPathClipRule ?? svgVisualElement.ClipRule;
        return svgClipRule == SvgClipRule.EvenOdd ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
    }

    private static SvgClipRule? GetSvgClipRule(SvgClipPath svgClipPath)
    {
        SvgService.TryGetAttribute(svgClipPath, "clip-rule", out var clipRuleString);

        return clipRuleString switch
        {
            "nonzero" => SvgClipRule.NonZero,
            "evenodd" => SvgClipRule.EvenOdd,
            // TODO: SvgClipRule.Inherit
            "inherit" => SvgClipRule.Inherit,
            _ => null
        };
    }

    internal static void GetClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
    {
        if (clipPath is null)
        {
            return;
        }

        if (!CanDraw(svgVisualElement, DrawAttributes.None))
        {
            return;
        }

        switch (svgVisualElement)
        {
            case SvgPath svgPath:
                {
                    var fillRule = ToFillRule(svgPath, svgClipPathClipRule);
                    var skPath = svgPath.PathData?.ToPath(fillRule);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgPath.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgRectangle svgRectangle:
                {
                    var fillRule = ToFillRule(svgRectangle, svgClipPathClipRule);
                    var skPath = svgRectangle.ToPath(fillRule, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgRectangle.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgCircle svgCircle:
                {
                    var fillRule = ToFillRule(svgCircle, svgClipPathClipRule);
                    var skPath = svgCircle.ToPath(fillRule, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgCircle.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgEllipse svgEllipse:
                {
                    var fillRule = ToFillRule(svgEllipse, svgClipPathClipRule);
                    var skPath = svgEllipse.ToPath(fillRule, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgEllipse.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgLine svgLine:
                {
                    var fillRule = ToFillRule(svgLine, svgClipPathClipRule);
                    var skPath = svgLine.ToPath(fillRule, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgLine.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgPolyline svgPolyline:
                {
                    var fillRule = ToFillRule(svgPolyline, svgClipPathClipRule);
                    var skPath = svgPolyline.Points?.ToPath(fillRule, false, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgPolyline.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgPolygon svgPolygon:
                {
                    var fillRule = ToFillRule(svgPolygon, svgClipPathClipRule);
                    var skPath = svgPolygon.Points?.ToPath(fillRule, true, skBounds);
                    if (skPath is null)
                    {
                        break;
                    }

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgPolygon.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, pathClip.Clip);
                }
                break;

            case SvgUse svgUse:
                {
                    if (SvgService.HasRecursiveReference(svgUse, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()))
                    {
                        break;
                    }

                    var svgReferencedVisualElement = SvgService.GetReference<SvgVisualElement>(svgUse, SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement));
                    if (svgReferencedVisualElement is null || svgReferencedVisualElement is SvgSymbol)
                    {
                        break;
                    }

                    WithUseInstanceStyleScope(svgReferencedVisualElement, svgUse, () =>
                    {
                        if (!CanDraw(svgReferencedVisualElement, DrawAttributes.None))
                        {
                            return;
                        }

                        // TODO: GetClipPath
                        GetClipPath(svgReferencedVisualElement, skBounds, uris, clipPath, svgClipPathClipRule);

                        if (clipPath.Clips is { } && clipPath.Clips.Count > 0)
                        {
                            // TODO: clipPath.Clips
                            var lastClip = clipPath.Clips[clipPath.Clips.Count - 1];
                            if (lastClip.Clip is { })
                            {
                                GetSvgVisualElementClipPath(svgUse, skBounds, uris, lastClip.Clip);
                            }
                        }
                    });
                }
                break;

            case SvgText svgText:
                {
                    var skPath = new SKPath();
                    skPath.AddRect(skBounds);

                    var pathClip = new PathClip
                    {
                        Path = skPath,
                        Transform = TransformsService.ToMatrix(svgText.Transforms),
                        Clip = new ClipPath
                        {
                            Clip = new ClipPath()
                        }
                    };
                    clipPath.Clips?.Add(pathClip);

                    GetSvgVisualElementClipPath(svgText, skPath.Bounds, uris, pathClip.Clip);
                }
                break;
        }
    }

    private static void WithUseInstanceStyleScope(SvgElement element, SvgUse useElement, Action action)
    {
        _ = element.WithUseInstanceStyleScope(useElement, () =>
        {
            action();
            return true;
        });
    }

    private static void GetClipPath(SvgElementCollection svgElementCollection, SKRect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
    {
        foreach (var svgElement in svgElementCollection)
        {
            if (svgElement is SvgVisualElement visualChild)
            {
                if (!CanDraw(visualChild, DrawAttributes.None))
                {
                    continue;
                }
                GetClipPath(visualChild, skBounds, uris, clipPath, svgClipPathClipRule);
            }
        }
    }

    internal static void GetClipPathClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return;
        }

        var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
        if (svgClipPathRef?.Children is null)
        {
            return;
        }

        GetClipPath(svgClipPathRef, skBounds, uris, clipPath);

        var skMatrix = SKMatrix.CreateIdentity();

        if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
            skMatrix = skMatrix.PostConcat(skScaleMatrix);

            var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
            skMatrix = skMatrix.PostConcat(skTranslateMatrix);
        }

        var skTransformsMatrix = TransformsService.ToMatrix(svgClipPathRef.Transforms);
        skMatrix = skMatrix.PostConcat(skTransformsMatrix);

        // TODO: clipPath.Transform
        clipPath.Transform = skMatrix;
    }

    internal static void GetClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return;
        }

        GetClipPathClipPath(svgClipPath, skBounds, uris, clipPath.Clip);

        var clipPathClipRule = GetSvgClipRule(svgClipPath);

        GetClipPath(svgClipPath.Children, skBounds, uris, clipPath, clipPathClipRule);

        var skMatrix = SKMatrix.CreateIdentity();

        if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
            skMatrix = skMatrix.PostConcat(skScaleMatrix);

            var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
            skMatrix = skMatrix.PostConcat(skTranslateMatrix);
        }

        var skTransformsMatrix = TransformsService.ToMatrix(svgClipPath.Transforms);
        skMatrix = skMatrix.PostConcat(skTransformsMatrix);

        // TODO: clipPath.Transform
        clipPath.Transform = skMatrix;

        if (clipPath.Clips is { } && clipPath.Clips.Count == 0 && !HasClipGeometry(clipPath.Clip))
        {
            var pathClip = new PathClip
            {
                Path = new SKPath(),
                Transform = SKMatrix.CreateIdentity(),
                Clip = default
            };
            clipPath.Clips.Add(pathClip);
        }
    }

    private static bool HasClipGeometry(ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return false;
        }

        if (clipPath.Clips is { Count: > 0 })
        {
            return true;
        }

        return HasClipGeometry(clipPath.Clip);
    }

    internal static void GetSvgVisualElementClipPath(SvgVisualElement? svgVisualElement, SKRect skBounds, HashSet<Uri> uris, ClipPath clipPath)
    {
        if (svgVisualElement?.ClipPath is null)
        {
            return;
        }

        if (SvgService.HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
        {
            return;
        }

        var svgClipPath = SvgService.GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
        if (svgClipPath?.Children is null)
        {
            return;
        }

        GetClipPath(svgClipPath, skBounds, uris, clipPath);
    }

    internal static SKRect? GetClipRect(string clip, SKRect skRectBounds)
    {
        if (string.IsNullOrWhiteSpace(clip))
        {
            return default;
        }

        clip = clip.Trim();
        if (!clip.StartsWith("rect(", StringComparison.OrdinalIgnoreCase) ||
            !clip.EndsWith(")", StringComparison.Ordinal))
        {
            return default;
        }

        var value = clip.Substring(5, clip.Length - 6);
        var parts = value.IndexOf(',') >= 0
            ? value.Split(',')
            : value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4)
        {
            return default;
        }

        if (value.IndexOf(',') >= 0)
        {
            return CreateLegacyOffsetClipRect(parts, skRectBounds);
        }

        if (!TryResolveClipEdge(parts[0], 0f, out var top) ||
            !TryResolveClipEdge(parts[1], skRectBounds.Width, out var right) ||
            !TryResolveClipEdge(parts[2], skRectBounds.Height, out var bottom) ||
            !TryResolveClipEdge(parts[3], 0f, out var left))
        {
            return default;
        }

        var width = Math.Max(0f, right - left);
        var height = Math.Max(0f, bottom - top);
        return SKRect.Create(skRectBounds.Left + left, skRectBounds.Top + top, width, height);
    }

    private static SKRect? CreateLegacyOffsetClipRect(string[] parts, SKRect skRectBounds)
    {
        if (!TryResolveClipEdge(parts[0], 0f, out var topOffset) ||
            !TryResolveClipEdge(parts[1], 0f, out var rightOffset) ||
            !TryResolveClipEdge(parts[2], 0f, out var bottomOffset) ||
            !TryResolveClipEdge(parts[3], 0f, out var leftOffset))
        {
            return default;
        }

        var width = Math.Max(0f, skRectBounds.Width - leftOffset - rightOffset);
        var height = Math.Max(0f, skRectBounds.Height - topOffset - bottomOffset);
        return SKRect.Create(skRectBounds.Left + leftOffset, skRectBounds.Top + topOffset, width, height);
    }

    private static bool TryResolveClipEdge(string value, float autoValue, out float edge)
    {
        value = value.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            edge = autoValue;
            return true;
        }

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 2).Trim();
        }

        return float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out edge);
    }
}
