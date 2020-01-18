// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//#define USE_NEW_FILTERS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgFilterskUtil
    {
        public static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        public static float[] CreateIdentityColorMatrixArray()
        {
            return new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0
            };
        }

        public static SKImageFilter? CreateColorMatrix(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgColourMatrix svgColourMatrix, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            SKColorFilter skColorFilter;

            switch (svgColourMatrix.Type)
            {
                case FilterEffects.SvgColourMatrixType.HueRotate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        var angle = (float)DegreeToRadian(value);
                        var a1 = Math.Cos(angle);
                        var a2 = Math.Sin(angle);
                        float[] matrix = new float[]
                        {
                            (float)(0.213 + a1 * +0.787 + a2 * -0.213),
                            (float)(0.715 + a1 * -0.715 + a2 * -0.715),
                            (float)(0.072 + a1 * -0.072 + a2 * +0.928), 0, 0,
                            (float)(0.213 + a1 * -0.213 + a2 * +0.143),
                            (float)(0.715 + a1 * +0.285 + a2 * +0.140),
                            (float)(0.072 + a1 * -0.072 + a2 * -0.283), 0, 0,
                            (float)(0.213 + a1 * -0.213 + a2 * -0.787),
                            (float)(0.715 + a1 * -0.715 + a2 * +0.715),
                            (float)(0.072 + a1 * +0.928 + a2 * +0.072), 0, 0,
                            0, 0, 0, 1, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    }
                    break;
                case FilterEffects.SvgColourMatrixType.LuminanceToAlpha:
                    {
                        float[] matrix = new float[]
                        {
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0.2125f, 0.7154f, 0.0721f, 0, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        //skColorFilter = SKColorFilter.CreateLumaColor();
                    }
                    break;
                case FilterEffects.SvgColourMatrixType.Saturate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        float[] matrix = new float[]
                        {
                            (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                            0, 0, 0, 1, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    };
                    break;
                default:
                case FilterEffects.SvgColourMatrixType.Matrix:
                    {
                        float[] matrix;
                        if (string.IsNullOrEmpty(svgColourMatrix.Values))
                        {
                            matrix = CreateIdentityColorMatrixArray();
                        }
                        else
                        {
                            var parts = svgColourMatrix.Values.Split(new char[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Count() == 20)
                            {
                                matrix = new float[20];
                                for (int i = 0; i < 20; i++)
                                {
                                    matrix[i] = float.Parse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture);
                                }
                            }
                            else
                            {
                                matrix = CreateIdentityColorMatrixArray();
                            }
                        }
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    }
                    break;
            }

            return SKImageFilter.CreateColorFilter(skColorFilter, input, cropRect);
        }

        public static SKImageFilter? CreateBlur(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgGaussianBlur svgGaussianBlur, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO: Calculate correct value of sigma using one value stdDeviation.
            var sigmaX = svgGaussianBlur.StdDeviation;
            var sigmaY = svgGaussianBlur.StdDeviation;

            return svgGaussianBlur.BlurType switch
            {
                FilterEffects.BlurType.HorizontalOnly => SKImageFilter.CreateBlur(sigmaX, 0f, input, cropRect),
                FilterEffects.BlurType.VerticalOnly => SKImageFilter.CreateBlur(0f, sigmaY, input, cropRect),
                _ => SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect),
            };
        }

        public static SKImageFilter? CreateMerge(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgMerge svgMerge, Dictionary<string, SKImageFilter> results, SKImageFilter.CropRect? cropRect = null)
        {
            var children = svgMerge.Children.OfType<FilterEffects.SvgMergeNode>().ToList();
            var filters = new SKImageFilter[children.Count];

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var inputKey = child.Input;
                if (!string.IsNullOrWhiteSpace(inputKey) && results.ContainsKey(inputKey))
                {
                    filters[i] = results[inputKey];
                }
                else
                {
                    return null;
                }
            }

            return SKImageFilter.CreateMerge(filters, cropRect);
        }

        public static SKImageFilter? CreateOffset(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgOffset svgOffset, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            float dx = svgOffset.Dx.ToDeviceValue(UnitRenderingType.Horizontal, svgOffset, skBounds);
            float dy = svgOffset.Dy.ToDeviceValue(UnitRenderingType.Vertical, svgOffset, skBounds);

            return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
        }

        private static SKImageFilter? GetInputFilter(string inputKey, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult)
        {
            SKImageFilter? inputFilter;

            if (!string.IsNullOrWhiteSpace(inputKey) && results.ContainsKey(inputKey))
            {
                inputFilter = results[inputKey];
            }
            else
            {
                inputFilter = lastResult;
            }

            return inputFilter;
        }

        private static SKImageFilter? SetImageFilter(FilterEffects.SvgFilterPrimitive svgFilterPrimitive, SKPaint skPaint, SKImageFilter skImageFilter, Dictionary<string, SKImageFilter> results, CompositeDisposable disposable)
        {
            var key = svgFilterPrimitive.Result;
            if (!string.IsNullOrWhiteSpace(key))
            {
                results[key] = skImageFilter;
            }
            disposable.Add(skImageFilter);
            skPaint.ImageFilter = skImageFilter;
            return skImageFilter;
        }

        public static SKPaint? GetFilterSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, CompositeDisposable disposable)
        {
            var filter = svgVisualElement.Filter;
            if (filter == null)
            {
                return null;
            }

            if (SvgExtensions.HasRecursiveReference(svgVisualElement, (e) => e.Filter, new HashSet<Uri>()))
            {
                return null;
            }

            var svgFilter = SvgExtensions.GetReference<FilterEffects.SvgFilter>(svgVisualElement, svgVisualElement.Filter);
            if (svgFilter == null)
            {
                return null;
            }

            var results = new Dictionary<string, SKImageFilter>();
            var lastResult = default(SKImageFilter);

            // TODO: Handle filterUnits and primitiveUnits.

            float x = svgFilter.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFilter, skBounds);
            float y = svgFilter.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFilter, skBounds);
            float width = svgFilter.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFilter, skBounds);
            float height = svgFilter.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFilter, skBounds);

            if (width <= 0f || height <= 0f)
            {
                // TODO: Disable visual element rendering.
                //return null;
            }

            var skClipRect = SKRect.Create(x, y, width, height);
            var skCropRect = default(SKImageFilter.CropRect);
            // TODO: var skCropRect = new SKImageFilter.CropRect(skClipRect);

            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill
            };

            foreach (var child in svgFilter.Children)
            {
                if (child is FilterEffects.SvgFilterPrimitive svgFilterPrimitive)
                {
                    switch (svgFilterPrimitive)
                    {
#if USE_NEW_FILTERS
                        case FilterEffects.SvgBlend svgBlend:
                            {
                                // TODO:
                            }
                            break;
#endif
                        case FilterEffects.SvgColourMatrix svgColourMatrix:
                            {
                                var inputKey = svgColourMatrix.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateColorMatrix(svgVisualElement, skBounds, svgColourMatrix, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgColourMatrix, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#if USE_NEW_FILTERS
                        case FilterEffects.SvgComponentTransfer svgComponentTransfer:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgComposite svgComposite:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgConvolveMatrix svgConvolveMatrix:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgDiffuseLighting svgDiffuseLighting:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgDisplacementMap svgDisplacementMap:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgDistantLight svgDistantLight:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgFlood svgFlood:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgFuncA svgFuncA:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgFuncB svgFuncB:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgFuncG svgFuncG:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgFuncR svgFuncR:
                            {
                                // TODO:
                            }
                            break;
#endif
                        case FilterEffects.SvgGaussianBlur svgGaussianBlur:
                            {
                                var inputKey = svgGaussianBlur.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateBlur(svgVisualElement, skBounds, svgGaussianBlur, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgGaussianBlur, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;

#if USE_NEW_FILTERS
                        case FilterEffects.SvgImage svgImage:
                            {
                                // TODO:
                            }
                            break;
#endif
                        case FilterEffects.SvgMerge svgMerge:
                            {
                                var skImageFilter = CreateMerge(svgVisualElement, skBounds, svgMerge, results, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgMerge, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#if USE_NEW_FILTERS
                        case FilterEffects.SvgMorphology svgMorphology:
                            {
                                // TODO:
                            }
                            break;
#endif
                        case FilterEffects.SvgOffset svgOffset:
                            {
                                var inputKey = svgOffset.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateOffset(svgVisualElement, skBounds, svgOffset, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgOffset, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#if USE_NEW_FILTERS
                        case FilterEffects.SvgPointLight svgPointLight:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgSpecularLighting svgSpecularLighting:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgSpotLight svgSpotLight:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgTile svgTile:
                            {
                                // TODO:
                            }
                            break;
                        case FilterEffects.SvgTurbulence svgTurbulence:
                            {
                                // TODO:
                            }
                            break;
#endif
                        default:
                            {
                                // TODO: Implement other filters.
                            }
                            break;
                    }
                }
            }

            disposable.Add(skPaint);
            return skPaint;
        }
    }
}
