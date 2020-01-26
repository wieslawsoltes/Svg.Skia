// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public static class SvgClippingExtensions
    {
        public static bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible == true;
            bool ignoreDisplay = ignoreAttributes.HasFlag(Attributes.Display);
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        public static SKPath? GetClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            if (!CanDraw(svgVisualElement, Attributes.None))
            {
                return null;
            }
            switch (svgVisualElement)
            {
                case SvgPath svgPath:
                    {
                        var fillRule = (svgPath.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPath.PathData?.ToSKPath(fillRule, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        var fillRule = (svgRectangle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgRectangle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgRectangle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        var fillRule = (svgCircle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgCircle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgCircle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        var fillRule = (svgEllipse.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgEllipse.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgEllipse.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgLine svgLine:
                    {
                        var fillRule = (svgLine.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgLine.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgLine.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        var fillRule = (svgPolyline.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPolyline.Points?.ToSKPath(fillRule, false, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgPolyline.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        var fillRule = (svgPolygon.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPolygon.Points?.ToSKPath(fillRule, true, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgPolygon.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgUse svgUse:
                    {
                        if (SvgExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
                        {
                            break;
                        }

                        var svgReferencedVisualElement = SvgExtensions.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                        if (svgReferencedVisualElement == null || svgReferencedVisualElement is SvgSymbol)
                        {
                            break;
                        }

                        if (!CanDraw(svgReferencedVisualElement, Attributes.None))
                        {
                            break;
                        }

                        var skPath = GetClipPath(svgReferencedVisualElement, skBounds, uris, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgUse.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgUse, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgText svgText:
                    {
                        // TODO: Get path from SvgText.
                    }
                    break;
                default:
                    break;
            }
            return null;
        }

        private static SKPath? GetClipPath(SvgElementCollection svgElementCollection, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var skPathClip = default(SKPath);

            foreach (var svgElement in svgElementCollection)
            {
                if (svgElement is SvgVisualElement visualChild)
                {
                    if (!CanDraw(visualChild, Attributes.None))
                    {
                        continue;
                    }
                    var skPath = GetClipPath(visualChild, skBounds, uris, disposable);
                    if (skPath != null)
                    {
                        if (skPathClip == null)
                        {
                            skPathClip = skPath;
                        }
                        else
                        {
                            var result = skPathClip.Op(skPath, SKPathOp.Union);
                            disposable.Add(result);
                            skPathClip = result;
                        }
                    }
                }
            }

            return skPathClip;
        }

        public static SKPath? GetClipPathClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
            if (svgClipPathRef == null || svgClipPathRef.Children == null)
            {
                return null;
            }

            var clipPath = GetClipPath(svgClipPathRef, skBounds, uris, disposable);
            if (clipPath != null)
            {
                var skMatrix = SKMatrix.MakeIdentity();

                if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skScaleMatrix = SKMatrix.MakeScale(skBounds.Width, skBounds.Height);
                    SKMatrix.PostConcat(ref skMatrix, ref skScaleMatrix);

                    var skTranslateMatrix = SKMatrix.MakeTranslation(skBounds.Left, skBounds.Top);
                    SKMatrix.PostConcat(ref skMatrix, ref skTranslateMatrix);
                }

                var skTransformsMatrix = SvgTransformsExtensions.ToSKMatrix(svgClipPathRef.Transforms);
                SKMatrix.PostConcat(ref skMatrix, ref skTransformsMatrix);

                clipPath.Transform(skMatrix);
            }

            return clipPath;
        }

        public static SKPath? GetClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var skPathClip = default(SKPath);

            var clipPathClipPath = GetClipPathClipPath(svgClipPath, skBounds, uris, disposable);
            if (clipPathClipPath != null && !clipPathClipPath.IsEmpty)
            {
                skPathClip = clipPathClipPath;
            }

            var clipPath = GetClipPath(svgClipPath.Children, skBounds, uris, disposable);
            if (clipPath != null)
            {
                var skMatrix = SKMatrix.MakeIdentity();

                if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skScaleMatrix = SKMatrix.MakeScale(skBounds.Width, skBounds.Height);
                    SKMatrix.PostConcat(ref skMatrix, ref skScaleMatrix);

                    var skTranslateMatrix = SKMatrix.MakeTranslation(skBounds.Left, skBounds.Top);
                    SKMatrix.PostConcat(ref skMatrix, ref skTranslateMatrix);
                }

                var skTransformsMatrix = SvgTransformsExtensions.ToSKMatrix(svgClipPath.Transforms);
                SKMatrix.PostConcat(ref skMatrix, ref skTransformsMatrix);

                clipPath.Transform(skMatrix);

                if (skPathClip == null)
                {
                    skPathClip = clipPath;
                }
                else
                {
                    var result = skPathClip.Op(clipPath, SKPathOp.Intersect);
                    disposable.Add(result);
                    skPathClip = result;
                }
            }

            if (skPathClip == null)
            {
                skPathClip = new SKPath();
                disposable.Add(skPathClip);
            }

            return skPathClip;
        }

        public static SKPath? GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            if (svgVisualElement == null || svgVisualElement.ClipPath == null)
            {
                return null;
            }

            if (SvgExtensions.HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
            {
                return null;
            }

            var svgClipPath = SvgExtensions.GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
            if (svgClipPath == null || svgClipPath.Children == null)
            {
                return null;
            }

            return GetClipPath(svgClipPath, skBounds, uris, disposable);
        }

        public static SKRect? GetClipRect(SvgVisualElement svgVisualElement, SKRect skRectBounds)
        {
            var clip = svgVisualElement.Clip;
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect("))
            {
                clip = clip.Trim();
                var offsets = new List<float>();
                foreach (var o in clip.Substring(5, clip.Length - 6).Split(','))
                {
                    offsets.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                }

                var skClipRect = SKRect.Create(
                    skRectBounds.Left + offsets[3],
                    skRectBounds.Top + offsets[0],
                    skRectBounds.Width - (offsets[3] + offsets[1]),
                    skRectBounds.Height - (offsets[2] + offsets[0]));
                return skClipRect;
            }
            return null;
        }

        public static MaskDrawable? GetSvgElementMask(SvgElement svgElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMaskRef = svgElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef == null || svgMaskRef.Children == null)
            {
                return null;
            }
            var maskDrawable = new MaskDrawable(svgMaskRef, skBounds, null, null, Attributes.None);
            disposable.Add(maskDrawable);
            return maskDrawable;
        }
    }
}
