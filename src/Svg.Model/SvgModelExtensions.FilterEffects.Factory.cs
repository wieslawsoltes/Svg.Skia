using System;
using System.Collections.Generic;
using System.Diagnostics;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        internal static Paint? GetFilterPaint(SvgVisualElement svgVisualElement, Rect skBounds, IFilterSource filterSource, IAssetLoader assetLoader, out bool isValid)
        {
            var filter = svgVisualElement.Filter;
            if (filter is null || IsNone(filter))
            {
                isValid = true;
                return default;
            }

            var svgReferencedFilters = GetLinkedFilter(svgVisualElement, new HashSet<Uri>());
            if (svgReferencedFilters is null || svgReferencedFilters.Count < 0)
            {
                isValid = false;
                return default;
            }

            var svgFirstFilter = svgReferencedFilters[0];

            SvgFilter? firstChildren = default;
            SvgFilter? firstX = default;
            SvgFilter? firstY = default;
            SvgFilter? firstWidth = default;
            SvgFilter? firstHeight = default;
            SvgFilter? firstFilterUnits = default;
            SvgFilter? firstPrimitiveUnits = default;

            foreach (var p in svgReferencedFilters)
            {
                if (firstChildren is null && p.Children.Count > 0)
                {
                    firstChildren = p;
                }

                if (firstX is null && TryGetAttribute(p, "x", out _))
                {
                    firstX = p;
                }

                if (firstY is null && TryGetAttribute(p, "y", out _))
                {
                    firstY = p;
                }

                if (firstWidth is null && TryGetAttribute(p, "width", out _))
                {
                    firstWidth = p;
                }

                if (firstHeight is null && TryGetAttribute(p, "height", out _))
                {
                    firstHeight = p;
                }

                if (firstFilterUnits is null && TryGetAttribute(p, "filterUnits", out _))
                {
                    firstFilterUnits = p;
                }

                if (firstPrimitiveUnits is null && TryGetAttribute(p, "primitiveUnits", out _))
                {
                    firstPrimitiveUnits = p;
                }
            }

            if (firstChildren is null)
            {
                isValid = false;
                return default;
            }

            var xUnit = firstX?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
            var yUnit = firstY?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
            var widthUnit = firstWidth?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
            var heightUnit = firstHeight?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
            var filterUnits = firstFilterUnits?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
            var primitiveUnits = firstPrimitiveUnits?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

            var skFilterRegion = CalculateRect(xUnit, yUnit, widthUnit, heightUnit, filterUnits, skBounds, svgFirstFilter);
            if (skFilterRegion is null)
            {
                isValid = false;
                return default;
            }

            var items = new List<(SvgFilterPrimitive primitive, Rect region)>();

            Debug.WriteLine($"-------------------------------------------------------------------------");
            Debug.WriteLine($"[ELEMENT]\t{svgVisualElement.GetType()} ({skBounds})");
            Debug.WriteLine($"[FILTER]\t{filter.ToString()} ({skFilterRegion}), filterUnits={filterUnits}, primitiveUnits={primitiveUnits}");

            foreach (var child in firstChildren.Children)
            {
                if (child is not SvgFilterPrimitive svgFilterPrimitive)
                {
                    continue;
                }

                // TODO: skFilterRegion, skBounds
                var skPrimitiveBounds = skFilterRegion.Value;

                var xUnitChild = svgFilterPrimitive.X;
                var yUnitChild = svgFilterPrimitive.Y;
                var widthUnitChild = svgFilterPrimitive.Width;
                var heightUnitChild = svgFilterPrimitive.Height;

                // TODO: primitiveUnits ==  SvgCoordinateUnits.UserSpaceOnUse
                var skFilterPrimitiveRegion = CalculateRect(xUnitChild, yUnitChild, widthUnitChild, heightUnitChild, primitiveUnits, skPrimitiveBounds, svgFilterPrimitive);
                if (skFilterPrimitiveRegion is null)
                {
                    // TODO:
                    continue;
                }

                Debug.WriteLine($"[PRIMITIVE]\t{svgFilterPrimitive.GetType().Name} ({skFilterPrimitiveRegion})");

                items.Add((svgFilterPrimitive, skFilterPrimitiveRegion.Value));
            }

            var results = new Dictionary<string, ImageFilter>();
            var regions = new Dictionary<ImageFilter, Rect>();
            var lastResult = default(ImageFilter);

            for (var i = 0; i < items.Count; i++)
            {
                var (svgFilterPrimitive, skFilterPrimitiveRegion) = items[i];
                var isFirst = i == 0;

                switch (svgFilterPrimitive)
                {
                    case SvgBlend svgBlend:
                        {
                            var input1Key = svgBlend.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgBlend.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateBlend(svgBlend, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgColourMatrix svgColourMatrix:
                        {
                            var inputKey = svgColourMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateColorMatrix(svgColourMatrix, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgComponentTransfer svgComponentTransfer:
                        {
                            var inputKey = svgComponentTransfer.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateComponentTransfer(svgComponentTransfer, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgComposite svgComposite:
                        {
                            var input1Key = svgComposite.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgComposite.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateComposite(svgComposite, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgConvolveMatrix svgConvolveMatrix:
                        {
                            var inputKey = svgConvolveMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgDiffuseLighting svgDiffuseLighting:
                        {
                            var inputKey = svgDiffuseLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgDisplacementMap svgDisplacementMap:
                        {
                            var input1Key = svgDisplacementMap.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgDisplacementMap.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateDisplacementMap(svgDisplacementMap, skFilterPrimitiveRegion, primitiveUnits, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgFlood svgFlood:
                        {
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateFlood(svgFlood, svgVisualElement, skFilterPrimitiveRegion, null, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgGaussianBlur svgGaussianBlur:
                        {
                            var inputKey = svgGaussianBlur.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateBlur(svgGaussianBlur, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case FilterEffects.SvgImage svgImage:
                        {
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateImage(svgImage, skFilterPrimitiveRegion, assetLoader, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgMerge svgMerge:
                        {
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateMerge(svgMerge, results, lastResult, filterSource, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgMorphology svgMorphology:
                        {
                            var inputKey = svgMorphology.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateMorphology(svgMorphology, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgOffset svgOffset:
                        {
                            var inputKey = svgOffset.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateOffset(svgOffset, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgSpecularLighting svgSpecularLighting:
                        {
                            var inputKey = svgSpecularLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateSpecularLighting(svgSpecularLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgTile svgTile:
                        {
                            var inputKey = svgTile.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var tileBounds = skFilterPrimitiveRegion;
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                tileBounds = regions[inputFilter];
                            }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateTile(svgTile, tileBounds, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgTurbulence svgTurbulence:
                        {
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateTurbulence(svgTurbulence, skFilterPrimitiveRegion, primitiveUnits, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;
                }
            }

            if (lastResult is { })
            {
                var skPaint = new Paint
                {
                    Style = PaintStyle.StrokeAndFill,
                    ImageFilter = lastResult
                };
                isValid = true;
                return skPaint;
            }

            isValid = false;
            return default;
        }
    }
}
