using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgSceneClipCompiler
{
    public static ClipPath? CompileClipPath(SvgClipPath svgClipPath, SKRect targetBounds, ISvgAssetLoader assetLoader)
    {
        var clipPath = new ClipPath
        {
            Clip = new ClipPath()
        };

        PopulateClipPath(svgClipPath, targetBounds, assetLoader, new HashSet<Uri>(), clipPath, svgClipPathClipRule: null);
        return clipPath.Clips is { Count: > 0 } || clipPath.Clip is { Clips.Count: > 0 }
            ? clipPath
            : null;
    }

    private static void PopulateClipPath(
        SvgClipPath svgClipPath,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        PopulateClipPathReference(svgClipPath, targetBounds, assetLoader, uris, clipPath);

        var clipRule = GetSvgClipRule(svgClipPath) ?? svgClipPathClipRule;
        PopulateClipChildren(svgClipPath.Children, targetBounds, assetLoader, uris, clipPath, clipRule);

        var transform = SKMatrix.CreateIdentity();
        if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            transform = transform.PostConcat(SKMatrix.CreateScale(targetBounds.Width, targetBounds.Height));
            transform = transform.PostConcat(SKMatrix.CreateTranslation(targetBounds.Left, targetBounds.Top));
        }

        transform = transform.PostConcat(TransformsService.ToMatrix(svgClipPath.Transforms));
        clipPath.Transform = transform;

        if (clipPath.Clips is { Count: 0 })
        {
            clipPath.Clips.Add(new PathClip
            {
                Path = new SKPath(),
                Transform = SKMatrix.CreateIdentity()
            });
        }
    }

    private static void PopulateClipPathReference(
        SvgClipPath svgClipPath,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        if (clipPath.Clip is null)
        {
            clipPath.Clip = new ClipPath();
        }

        var referencedClipPath = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
        if (referencedClipPath?.Children is null)
        {
            return;
        }

        PopulateClipPath(referencedClipPath, targetBounds, assetLoader, uris, clipPath.Clip, svgClipPathClipRule: null);

        var transform = SKMatrix.CreateIdentity();
        if (referencedClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            transform = transform.PostConcat(SKMatrix.CreateScale(targetBounds.Width, targetBounds.Height));
            transform = transform.PostConcat(SKMatrix.CreateTranslation(targetBounds.Left, targetBounds.Top));
        }

        transform = transform.PostConcat(TransformsService.ToMatrix(referencedClipPath.Transforms));
        clipPath.Clip.Transform = transform;
    }

    private static void PopulateClipChildren(
        SvgElementCollection children,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        foreach (var child in children)
        {
            if (child is not SvgVisualElement visualChild ||
                !MaskingService.CanDraw(visualChild, DrawAttributes.None))
            {
                continue;
            }

            PopulateVisualClip(visualChild, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule);
        }
    }

    private static void PopulateVisualClip(
        SvgVisualElement visualElement,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        switch (visualElement)
        {
            case SvgPath svgPath:
                AddVisualPathClip(svgPath, svgPath.PathData?.ToPath(ToFillRule(svgPath, svgClipPathClipRule)), assetLoader, uris, clipPath);
                break;
            case SvgRectangle svgRectangle:
                AddVisualPathClip(svgRectangle, svgRectangle.ToPath(ToFillRule(svgRectangle, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgCircle svgCircle:
                AddVisualPathClip(svgCircle, svgCircle.ToPath(ToFillRule(svgCircle, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgEllipse svgEllipse:
                AddVisualPathClip(svgEllipse, svgEllipse.ToPath(ToFillRule(svgEllipse, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgLine svgLine:
                AddVisualPathClip(svgLine, svgLine.ToPath(ToFillRule(svgLine, svgClipPathClipRule), targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgPolyline svgPolyline:
                AddVisualPathClip(svgPolyline, svgPolyline.Points?.ToPath(ToFillRule(svgPolyline, svgClipPathClipRule), false, targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgPolygon svgPolygon:
                AddVisualPathClip(svgPolygon, svgPolygon.Points?.ToPath(ToFillRule(svgPolygon, svgClipPathClipRule), true, targetBounds), assetLoader, uris, clipPath);
                break;
            case SvgUse svgUse:
                PopulateUseClip(svgUse, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule);
                break;
            case SvgText svgText:
                AddTextClip(svgText, targetBounds, assetLoader, uris, clipPath);
                break;
        }
    }

    private static void AddVisualPathClip(
        SvgVisualElement visualElement,
        SKPath? path,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        if (path is null)
        {
            return;
        }

        var pathClip = new PathClip
        {
            Path = path,
            Transform = TransformsService.ToMatrix(visualElement.Transforms),
            Clip = new ClipPath
            {
                Clip = new ClipPath()
            }
        };

        clipPath.Clips?.Add(pathClip);
        PopulateNestedClipPath(visualElement, path.Bounds, assetLoader, uris, pathClip.Clip!);
    }

    private static void PopulateUseClip(
        SvgUse svgUse,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath,
        SvgClipRule? svgClipPathClipRule)
    {
        if (SvgService.HasRecursiveReference(svgUse, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var referencedVisualElement = SvgService.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
        if (referencedVisualElement is null ||
            referencedVisualElement is SvgSymbol ||
            !MaskingService.CanDraw(referencedVisualElement, DrawAttributes.None))
        {
            return;
        }

        var previousClipCount = clipPath.Clips?.Count ?? 0;
        PopulateVisualClip(referencedVisualElement, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule);
        if (clipPath.Clips is { Count: > 0 } clips &&
            clips.Count > previousClipCount &&
            clips[clips.Count - 1].Clip is { } lastClip)
        {
            PopulateNestedClipPath(svgUse, targetBounds, assetLoader, uris, lastClip);
        }
    }

    private static void AddTextClip(
        SvgText svgText,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        var path = SvgSceneTextCompiler.CreateClipPath(svgText, targetBounds, assetLoader);
        if (path is null || path.IsEmpty)
        {
            return;
        }

        var pathClip = new PathClip
        {
            Path = path,
            Transform = TransformsService.ToMatrix(svgText.Transforms),
            Clip = new ClipPath
            {
                Clip = new ClipPath()
            }
        };

        clipPath.Clips?.Add(pathClip);
        PopulateNestedClipPath(svgText, path.Bounds, assetLoader, uris, pathClip.Clip!);
    }

    private static void PopulateNestedClipPath(
        SvgVisualElement visualElement,
        SKRect targetBounds,
        ISvgAssetLoader assetLoader,
        HashSet<Uri> uris,
        ClipPath clipPath)
    {
        if (visualElement.ClipPath is null ||
            SvgService.HasRecursiveReference(visualElement, static e => e.ClipPath, uris))
        {
            return;
        }

        var referencedClipPath = SvgService.GetReference<SvgClipPath>(visualElement, visualElement.ClipPath);
        if (referencedClipPath?.Children is null)
        {
            return;
        }

        PopulateClipPath(referencedClipPath, targetBounds, assetLoader, uris, clipPath, svgClipPathClipRule: null);
    }

    private static SvgFillRule ToFillRule(SvgVisualElement visualElement, SvgClipRule? svgClipPathClipRule)
    {
        var svgClipRule = svgClipPathClipRule ?? visualElement.ClipRule;
        return svgClipRule == SvgClipRule.EvenOdd
            ? SvgFillRule.EvenOdd
            : SvgFillRule.NonZero;
    }

    private static SvgClipRule? GetSvgClipRule(SvgClipPath svgClipPath)
    {
        if (!SvgService.TryGetAttribute(svgClipPath, "clip-rule", out var clipRuleString))
        {
            return null;
        }

        return clipRuleString switch
        {
            "nonzero" => SvgClipRule.NonZero,
            "evenodd" => SvgClipRule.EvenOdd,
            "inherit" => SvgClipRule.Inherit,
            _ => null
        };
    }
}
