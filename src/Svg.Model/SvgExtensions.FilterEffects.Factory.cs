using System;
using System.Collections.Generic;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Primitives;

namespace Svg.Model
{
    public static partial class SvgExtensions
    {
        internal static SKPaint? GetFilterPaint(SvgVisualElement svgVisualElement, SKRect skBounds, SKRect skViewport, IFilterSource filterSource, IAssetLoader assetLoader, out bool isValid, out SKRect? filterClip)
        {
            var filter = svgVisualElement.Filter;
            if (filter is null || IsNone(filter))
            {
                isValid = true;
                filterClip = default;
                return default;
            }

            var svgReferencedFilters = GetLinkedFilter(svgVisualElement, new HashSet<Uri>());
            if (svgReferencedFilters is null || svgReferencedFilters.Count < 0)
            {
                isValid = false;
                filterClip = default;
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
                filterClip = default;
                return default;
            }

            var xUnit = firstX?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
            var yUnit = firstY?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
            var widthUnit = firstWidth?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
            var heightUnit = firstHeight?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
            var filterUnits = firstFilterUnits?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
            var primitiveUnits = firstPrimitiveUnits?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

            var skFilterRegion = CalculateRect(xUnit, yUnit, widthUnit, heightUnit, filterUnits, skBounds, skViewport, svgFirstFilter);
            if (skFilterRegion is null)
            {
                isValid = false;
                filterClip = default;
                return default;
            }

            var primitives = GetFilterPrimitives(firstChildren, primitiveUnits, skFilterRegion.Value, skBounds, skViewport);

            var results = new Dictionary<string, SKImageFilter>();
            var regions = new Dictionary<SKImageFilter, SKRect>();
            var lastResult = default(SKImageFilter);

            for (var i = 0; i < primitives.Count; i++)
            {
                var (svgFilterPrimitive, skFilterPrimitiveRegion) = primitives[i];
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
                                skFilterPrimitiveRegion = SKRect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                                skFilterPrimitiveRegion = SKRect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                                skFilterPrimitiveRegion = SKRect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                            var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);
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
                var skPaint = new SKPaint
                {
                    Style = SKPaintStyle.StrokeAndFill,
                    ImageFilter = lastResult
                };
                isValid = true;
                filterClip = skFilterRegion;
                return skPaint;
            }

            isValid = false;
            filterClip = default;
            return default;
        }

        private static List<(SvgFilterPrimitive primitive, SKRect region)> GetFilterPrimitives(SvgFilter svgFilter, SvgCoordinateUnits primitiveUnits, SKRect skFilterRegion, SKRect skBounds, SKRect skViewport)
        {
            var primitives = new List<(SvgFilterPrimitive primitive, SKRect region)>();

            foreach (var child in svgFilter.Children)
            {
                if (child is not SvgFilterPrimitive svgFilterPrimitive)
                {
                    continue;
                }

                var xChild = skFilterRegion.Left;
                var yChild = skFilterRegion.Top;
                var widthChild = skFilterRegion.Width;
                var heightChild = skFilterRegion.Height;

                var primitiveUseBoundingBox = primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox;

                if (TryGetAttribute(svgFilterPrimitive, "x", out var xChildString))
                {
                    if (new SvgUnitConverter().ConvertFromString(xChildString) is SvgUnit xChildUnit)
                    {
                        xChild = xChildUnit.ToDeviceValue(
                            primitiveUseBoundingBox ? UnitRenderingType.Horizontal : UnitRenderingType.HorizontalOffset,
                            svgFilterPrimitive,
                            primitiveUseBoundingBox ? skBounds : skViewport);

                        if (primitiveUseBoundingBox)
                        {
                            if (xChildUnit.Type != SvgUnitType.Percentage)
                            {
                                xChild *= skBounds.Width;
                            }

                            xChild += skBounds.Left;
                        }
                    }
                }

                if (TryGetAttribute(svgFilterPrimitive, "y", out var yChildString))
                {
                    if (new SvgUnitConverter().ConvertFromString(yChildString) is SvgUnit yUnitChild)
                    {
                        yChild = yUnitChild.ToDeviceValue(
                            primitiveUseBoundingBox ? UnitRenderingType.Vertical : UnitRenderingType.VerticalOffset,
                            svgFilterPrimitive,
                            primitiveUseBoundingBox ? skBounds : skViewport);

                        if (primitiveUseBoundingBox)
                        {
                            if (yUnitChild.Type != SvgUnitType.Percentage)
                            {
                                yChild *= skBounds.Height;
                            }

                            yChild += skBounds.Top;
                        }
                    }
                }

                if (TryGetAttribute(svgFilterPrimitive, "width", out var widthChildString))
                {
                    if (new SvgUnitConverter().ConvertFromString(widthChildString) is SvgUnit widthUnitChild)
                    {
                        widthChild = widthUnitChild.ToDeviceValue(
                            UnitRenderingType.Horizontal,
                            svgFilterPrimitive,
                            primitiveUseBoundingBox ? skBounds : skViewport);

                        if (primitiveUseBoundingBox)
                        {
                            if (widthUnitChild.Type != SvgUnitType.Percentage)
                            {
                                widthChild *= skBounds.Width;
                            }
                        }
                    }
                }

                if (TryGetAttribute(svgFilterPrimitive, "height", out var heightChildString))
                {
                    if (new SvgUnitConverter().ConvertFromString(heightChildString) is SvgUnit heightUnitChild)
                    {
                        heightChild = heightUnitChild.ToDeviceValue(
                            UnitRenderingType.Vertical,
                            svgFilterPrimitive,
                            primitiveUseBoundingBox ? skBounds : skViewport);

                        if (primitiveUseBoundingBox)
                        {
                            if (heightUnitChild.Type != SvgUnitType.Percentage)
                            {
                                heightChild *= skBounds.Height;
                            }
                        }
                    }
                }

                if (widthChild <= 0 || heightChild <= 0)
                {
                    continue;
                }

                var skFilterPrimitiveRegion = SKRect.Create(xChild, yChild, widthChild, heightChild);

                primitives.Add((svgFilterPrimitive, skFilterPrimitiveRegion));
            }

            return primitives;
        }
    }
}
