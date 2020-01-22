// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//#define USE_NEW_FILTERS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Svg.FilterEffects;

namespace Svg.Skia
{
    public static class SvgFiltersExtensions
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
#if USE_NEW_FILTERS
        public static SKBlendMode GetSKBlendMode(FilterEffects.SvgBlendMode svgBlendMode)
        {
            return svgBlendMode switch
            {
                FilterEffects.SvgBlendMode.Normal => SKBlendMode.SrcOver,
                FilterEffects.SvgBlendMode.Multiply => SKBlendMode.Multiply,
                FilterEffects.SvgBlendMode.Screen => SKBlendMode.Screen,
                FilterEffects.SvgBlendMode.Darken => SKBlendMode.Darken,
                FilterEffects.SvgBlendMode.Lighten => SKBlendMode.Lighten,
                _ => SKBlendMode.SrcOver,
            };
        }

        public static SKImageFilter? CreateBlend(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgBlend svgBlend, SKImageFilter background, SKImageFilter? foreground = null, SKImageFilter.CropRect? cropRect = null)
        {
            var mode = GetSKBlendMode(svgBlend.Mode);
            return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
        }
#endif
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
#if USE_NEW_FILTERS
        private static SKImageFilter? CreateComponentTransfer(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgComponentTransfer svgComponentTransfer, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            foreach (var child in svgComponentTransfer.Children)
            {
                switch (child)
                {
                    case SvgFuncA svgFuncA:
                        // TODO:
                        break;
                    case SvgFuncB svgFuncB:
                        // TODO:
                        break;
                    case SvgFuncG svgFuncG:
                        // TODO:
                        break;
                    case SvgFuncR svgFuncR:
                        // TODO:
                        break;
                }
            }

            // TODO:
            return null;
        }

        public static SKImageFilter? CreateComposite(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgComposite svgComposite, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO:
            return null;
        }

        public static SKImageFilter? CreateConvolveMatrix(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgConvolveMatrix svgConvolveMatrix, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO:
            return null;
        }

        public static SKImageFilter? CreateDiffuseLighting(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgDiffuseLighting svgDiffuseLighting, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            switch (svgDiffuseLighting.LightSource)
            {
                case SvgDistantLight svgDistantLight:
                    // TODO:
                    break;
                case SvgPointLight svgPointLight:
                    // TODO:
                    break;
                case SvgSpotLight svgSpotLight:
                    // TODO:
                    break;
                default:
                    // TODO:
                    break;
            }
            // TODO:
            return null;
        }

        public static SKImageFilter? CreateDisplacementMap(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgDisplacementMap svgDisplacementMap, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO:
            return null;
        }

        public static SKImageFilter? CreateFlood(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgFlood svgFlood, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var floodColor = svgFlood.FloodColor;
            var floodOpacity = svgFlood.FloodOpacity;

            var skPaint = new SKPaint()
            {
                Style = SKPaintStyle.StrokeAndFill
            };
            disposable.Add(skPaint);

            SvgPaintingExtensions.SetColorOrShader(svgVisualElement, floodColor, floodOpacity, skBounds, skPaint, false, IgnoreAttributes.None, disposable);

            if (cropRect == null)
            {
                cropRect = new SKImageFilter.CropRect(skBounds);
            }

            return SKImageFilter.CreatePaint(skPaint, cropRect);
        }
#endif
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
#if USE_NEW_FILTERS
        public static SKImageFilter? CreateImage(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgImage svgImage, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var SrcRect = default(SKRect);
            var image = SvgImageExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                return null;
            }

            if (skImage != null)
            {
                SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            if (skImage != null)
            {
                disposable.Add(skImage);
                return SKImageFilter.CreateImage(skImage, SrcRect, skBounds, SKFilterQuality.None);
            }

            if (svgFragment != null)
            {
                using var fragmentDrawable = new FragmentDrawable(svgFragment, skBounds, IgnoreAttributes.None);
                var skPicture = fragmentDrawable.Snapshot();
                disposable.Add(skPicture);

                if (cropRect == null)
                {
                    cropRect = new SKImageFilter.CropRect(skBounds);
                }

                SKImageFilter.CreatePicture(skPicture, cropRect.Rect);
            }

            return null;
        }
#endif
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
#if USE_NEW_FILTERS
        public static SKImageFilter? CreateMorphology(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgMorphology svgMorphology, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO:
            return null;
        }
#endif
        public static SKImageFilter? CreateOffset(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgOffset svgOffset, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            float dx = svgOffset.Dx.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgOffset, skBounds);
            float dy = svgOffset.Dy.ToDeviceValue(UnitRenderingType.VerticalOffset, svgOffset, skBounds);

            return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
        }
#if USE_NEW_FILTERS
        public static SKImageFilter? CreateSpecularLighting(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgSpecularLighting svgSpecularLighting, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            switch (svgSpecularLighting.LightSource)
            {
                case SvgDistantLight svgDistantLight:
                    // TODO:
                    break;
                case SvgPointLight svgPointLight:
                    // TODO:
                    break;
                case SvgSpotLight svgSpotLight:
                    // TODO:
                    break;
                default:
                    // TODO:
                    break;
            }
            // TODO:
            return null;
        }

        public static SKImageFilter? CreateTile(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgTile svgTile, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            return SKImageFilter.CreateTile(skBounds, cropRect != null ? cropRect.Rect : skBounds, input);
        }

        public static SKImageFilter? CreateTurbulence(SvgVisualElement svgVisualElement, SKRect skBounds, FilterEffects.SvgTurbulence svgTurbulence, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            // TODO:
            return null;
        }
#endif
        private static SKImageFilter? GetInputFilter(string inputKey, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return lastResult;
            }

            if (results.ContainsKey(inputKey))
            {
                return results[inputKey];
            }

            switch (inputKey)
            {
                case "SourceGraphic":
                    // TODO:
                    break;
                case "SourceAlpha":
                    // TODO:
                    break;
                case "BackgroundImage":
                    // TODO:
                    break;
                case "BackgroundAlpha":
                    // TODO:
                    break;
                case "FillPaint":
                    // TODO:
                    break;
                case "StrokePaint":
                    // TODO:
                    break;
            }

            return null;
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
            var prevoiusFilterPrimitiveRegion = SKRect.Empty;

            // TODO: Handle filterUnits and primitiveUnits.

            float x = svgFilter.X.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilter, skBounds);
            float y = svgFilter.Y.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilter, skBounds);
            float width = svgFilter.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFilter, skBounds);
            float height = svgFilter.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFilter, skBounds);

            if (width <= 0f || height <= 0f)
            {
                // TODO: Disable visual element rendering.
                //return null;
            }

            // TOOD: FilterUnits and PrimitiveUnits

            var skFilterRegion = SKRect.Create(x, y, width, height);

            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill
            };

            foreach (var child in svgFilter.Children)
            {
                if (child is FilterEffects.SvgFilterPrimitive svgFilterPrimitive)
                {
#if USE_NEW_FILTERS
                    float xChild = svgFilterPrimitive.X.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilterPrimitive, skFilterRegion);
                    float yChild = svgFilterPrimitive.Y.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilterPrimitive, skFilterRegion);
                    float widthChild = svgFilterPrimitive.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFilterPrimitive, skFilterRegion);
                    float heightChild = svgFilterPrimitive.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFilterPrimitive, skFilterRegion);

                    var skFilterPrimitiveRegion = SKRect.Create(xChild, yChild, widthChild, heightChild);
                    var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
#else
                    var skFilterPrimitiveRegion = SKRect.Create(skFilterRegion.Left, skFilterRegion.Top, skFilterRegion.Width, skFilterRegion.Height);
                    var skCropRect = default(SKImageFilter.CropRect);
#endif

                    switch (svgFilterPrimitive)
                    {
#if USE_NEW_FILTERS
                        case FilterEffects.SvgBlend svgBlend:
                            {
                                var input1Key = svgBlend.Input;
                                var input1Filter = GetInputFilter(input1Key, results, lastResult);
                                var input2Key = svgBlend.Input2;
                                var input2Filter = GetInputFilter(input2Key, results, lastResult);
                                if (input2Filter == null)
                                {
                                    break;
                                }
                                var skImageFilter = CreateBlend(svgVisualElement, skFilterPrimitiveRegion, svgBlend, input2Filter, input1Filter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgBlend, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#endif
                        case FilterEffects.SvgColourMatrix svgColourMatrix:
                            {
                                var inputKey = svgColourMatrix.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateColorMatrix(svgVisualElement, skFilterPrimitiveRegion, svgColourMatrix, inputFilter, skCropRect);
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
                                var inputKey = svgComponentTransfer.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateComponentTransfer(svgVisualElement, skFilterPrimitiveRegion, svgComponentTransfer, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgComponentTransfer, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgComposite svgComposite:
                            {
                                // TODO:
                                var inputKey = svgComposite.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateComposite(svgVisualElement, skFilterPrimitiveRegion, svgComposite, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgComposite, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgConvolveMatrix svgConvolveMatrix:
                            {
                                // TODO:
                                var inputKey = svgConvolveMatrix.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateConvolveMatrix(svgVisualElement, skFilterPrimitiveRegion, svgConvolveMatrix, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgConvolveMatrix, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgDiffuseLighting svgDiffuseLighting:
                            {
                                // TODO:
                                var inputKey = svgDiffuseLighting.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateDiffuseLighting(svgVisualElement, skFilterPrimitiveRegion, svgDiffuseLighting, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgDiffuseLighting, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgDisplacementMap svgDisplacementMap:
                            {
                                // TODO:
                                var inputKey = svgDisplacementMap.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateDisplacementMap(svgVisualElement, skFilterPrimitiveRegion, svgDisplacementMap, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgDisplacementMap, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgFlood svgFlood:
                            {
                                var inputKey = svgFlood.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateFlood(svgVisualElement, skFilterPrimitiveRegion, svgFlood, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgFlood, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#endif
                        case FilterEffects.SvgGaussianBlur svgGaussianBlur:
                            {
                                var inputKey = svgGaussianBlur.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateBlur(svgVisualElement, skFilterPrimitiveRegion, svgGaussianBlur, inputFilter, skCropRect);
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
                                var inputKey = svgImage.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateImage(svgVisualElement, skFilterPrimitiveRegion, svgImage, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgImage, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#endif
                        case FilterEffects.SvgMerge svgMerge:
                            {
                                var skImageFilter = CreateMerge(svgVisualElement, skFilterPrimitiveRegion, svgMerge, results, skCropRect);
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
                                var inputKey = svgMorphology.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateMorphology(svgVisualElement, skFilterPrimitiveRegion, svgMorphology, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgMorphology, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#endif
                        case FilterEffects.SvgOffset svgOffset:
                            {
                                var inputKey = svgOffset.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateOffset(svgVisualElement, skFilterPrimitiveRegion, svgOffset, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgOffset, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#if USE_NEW_FILTERS
                        case FilterEffects.SvgSpecularLighting svgSpecularLighting:
                            {
                                // TODO:
                                var inputKey = svgSpecularLighting.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateSpecularLighting(svgVisualElement, skFilterPrimitiveRegion, svgSpecularLighting, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgSpecularLighting, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgTile svgTile:
                            {
                                var inputKey = svgTile.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateTile(svgVisualElement, prevoiusFilterPrimitiveRegion, svgTile, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgTile, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
                        case FilterEffects.SvgTurbulence svgTurbulence:
                            {
                                // TODO:
                                var inputKey = svgTurbulence.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult);
                                var skImageFilter = CreateTurbulence(svgVisualElement, skFilterPrimitiveRegion, svgTurbulence, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgTurbulence, skPaint, skImageFilter, results, disposable);
                                }
                            }
                            break;
#endif
                        default:
                            break;
                    }

                    prevoiusFilterPrimitiveRegion = skFilterPrimitiveRegion;
                }
            }

            disposable.Add(skPaint);
            return skPaint;
        }
    }
}
