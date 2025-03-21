﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using ShimSkiaSharp;

namespace Svg.Model;

internal class SvgFilterContext
{
    private static readonly char[] s_colorMatrixSplitChars = [' ', '\t', '\n', '\r', ','];

    private const string SourceGraphic = "SourceGraphic";

    private const string SourceAlpha = "SourceAlpha";

    private const string BackgroundImage = "BackgroundImage";

    private const string BackgroundAlpha = "BackgroundAlpha";

    private const string FillPaint = "FillPaint";

    private const string StrokePaint = "StrokePaint";

    private static SvgFuncA s_identitySvgFuncA = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = []
    };

    private static SvgFuncR s_identitySvgFuncR = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = []
    };

    private static SvgFuncG s_identitySvgFuncG = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = []
    };

    private static SvgFuncB s_identitySvgFuncB = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = []
    };

    private readonly SvgVisualElement _svgVisualElement;
    private readonly SKRect _skBounds;
    private readonly SKRect _skViewport;
    private readonly IFilterSource _filterSource;
    private readonly IAssetLoader _assetLoader;
    private readonly HashSet<Uri>? _references;
    private SKRect _skFilterRegion;
    private SvgCoordinateUnits _primitiveUnits;
    private readonly List<SvgFilterPrimitiveContext> _primitives;
    private readonly Dictionary<string, SvgFilterResult> _results;
    private SvgFilterResult? _lastResult;
    private readonly Dictionary<SKImageFilter, SKRect> _regions;

    public bool IsValid { get; private set; }

    public SKRect? FilterClip { get; private set; }

    public SKPaint? FilterPaint { get; private set; }

    public SvgFilterContext(SvgVisualElement svgVisualElement, SKRect skBounds, SKRect skViewport, IFilterSource filterSource, IAssetLoader assetLoader, HashSet<Uri>? references)
    { 
        _svgVisualElement = svgVisualElement;
        _skBounds = skBounds;
        _skViewport = skViewport;
        _filterSource = filterSource;
        _assetLoader = assetLoader;
        _references = references;

        _primitives = [];
        _results = new();
        _regions = new();

        FilterPaint = Initialize() ? CreateFilterPaint() : default;
    }

    private bool Initialize()
    {
        var filter = _svgVisualElement.Filter;
        if (filter is null || filter.IsNone())
        {
            IsValid = true;
            FilterClip = default;
            return false;
        }

        var svgReferencedFilters = GetLinkedFilter(_svgVisualElement, []);
        if (svgReferencedFilters is null || svgReferencedFilters.Count < 0)
        {
            IsValid = false;
            FilterClip = default;
            return false;
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

            if (firstX is null && SvgExtensions.TryGetAttribute(p, "x", out _))
            {
                firstX = p;
            }

            if (firstY is null && SvgExtensions.TryGetAttribute(p, "y", out _))
            {
                firstY = p;
            }

            if (firstWidth is null && SvgExtensions.TryGetAttribute(p, "width", out _))
            {
                firstWidth = p;
            }

            if (firstHeight is null && SvgExtensions.TryGetAttribute(p, "height", out _))
            {
                firstHeight = p;
            }

            if (firstFilterUnits is null && SvgExtensions.TryGetAttribute(p, "filterUnits", out _))
            {
                firstFilterUnits = p;
            }

            if (firstPrimitiveUnits is null && SvgExtensions.TryGetAttribute(p, "primitiveUnits", out _))
            {
                firstPrimitiveUnits = p;
            }
        }

        if (firstChildren is null)
        {
            IsValid = false;
            FilterClip = default;
            return false;
        }

        var xUnit = firstX?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var yUnit = firstY?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var widthUnit = firstWidth?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var heightUnit = firstHeight?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var filterUnits = firstFilterUnits?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        _primitiveUnits = firstPrimitiveUnits?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

        var skFilterRegion = SvgExtensions.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, filterUnits, _skBounds, _skViewport, svgFirstFilter);
        if (skFilterRegion is null)
        {
            IsValid = false;
            FilterClip = default;
            return false;
        }

        _skFilterRegion = skFilterRegion.Value;

        foreach (var child in firstChildren.Children)
        {
            if (child is not SvgFilterPrimitive svgFilterPrimitive)
            {
                continue;
            }

            var primitiveContext = new SvgFilterPrimitiveContext(svgFilterPrimitive);

            if (SvgExtensions.TryGetAttribute(svgFilterPrimitive, "x", out var xChildString))
            {
                primitiveContext.IsXValid = true;

                if (new SvgUnitConverter().ConvertFromString(xChildString) is SvgUnit x)
                {
                    primitiveContext.X = x;
                }
            }
            else
            {
                primitiveContext.X = new SvgUnit(SvgUnitType.Percentage, 0);
            }

            if (SvgExtensions.TryGetAttribute(svgFilterPrimitive, "y", out var yChildString))
            {
                primitiveContext.IsYValid = true;

                if (new SvgUnitConverter().ConvertFromString(yChildString) is SvgUnit y)
                {
                    primitiveContext.Y = y;
                }
            }
            else
            {
                primitiveContext.Y = new SvgUnit(SvgUnitType.Percentage, 0);
            }

            if (SvgExtensions.TryGetAttribute(svgFilterPrimitive, "width", out var widthChildString))
            {
                primitiveContext.IsWidthValid = true;

                if (new SvgUnitConverter().ConvertFromString(widthChildString) is SvgUnit width)
                {
                    primitiveContext.Width = width;
                }
            }
            else
            {
                primitiveContext.Width = new SvgUnit(SvgUnitType.Percentage, 100);
            }

            if (SvgExtensions.TryGetAttribute(svgFilterPrimitive, "height", out var heightChildString))
            {
                primitiveContext.IsHeightValid = true;

                if (new SvgUnitConverter().ConvertFromString(heightChildString) is SvgUnit height)
                {
                    primitiveContext.Height = height;
                }
            }
            else
            {
                primitiveContext.Height = new SvgUnit(SvgUnitType.Percentage, 100);
            }

            var boundaries = SvgExtensions.CalculateRect(
                primitiveContext.X,
                primitiveContext.Y,
                primitiveContext.Width,
                primitiveContext.Height,
                _primitiveUnits,
                _skBounds,
                _skViewport,
                primitiveContext.FilterPrimitive);

            if (boundaries is null)
            {
                continue;
            }

            primitiveContext.Boundaries = boundaries.Value;

            _primitives.Add(primitiveContext);
        }

        return true;
    }

    private SKPaint? CreateFilterPaint()
    {
        var i = 0;

        foreach (var primitive in _primitives)
        {
            CreateFilterPrimitiveFilter(primitive, i == 0);
            i++;
        }

        if (_lastResult is { })
        {
            var filter = _lastResult.Filter;
            if (_lastResult.ColorSpace != SvgColourInterpolation.SRGB)
            {
                filter = ApplyColourInterpolation(_lastResult, SvgColourInterpolation.SRGB);
            }

            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill,
                ImageFilter = filter
            };
            IsValid = true;
            FilterClip = _skFilterRegion;
            return skPaint;
        }

        IsValid = false;
        FilterClip = default;
        return default;
    }

    private void CreateFilterPrimitiveFilter(SvgFilterPrimitiveContext primitiveContext, bool isFirst)
    {;
        var svgFilterPrimitive = primitiveContext.FilterPrimitive;
        var colorInterpolationFilters = GetColorInterpolationFilters(svgFilterPrimitive);

        switch (svgFilterPrimitive)
        {
            case SvgBlend svgBlend:
            {
                var input1Key = svgBlend.Input;
                var input1FilterResult = GetInputFilter(input1Key, colorInterpolationFilters, _skFilterRegion, isFirst);
                var input2Key = svgBlend.Input2;
                var input2FilterResult = GetInputFilter(input2Key, colorInterpolationFilters, _skFilterRegion, false);
                if (input2FilterResult is null)
                {
                    break;
                }

                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, input1FilterResult, input2FilterResult, input1Key, input2Key);
                var skSKRect = skFilterPrimitiveRegion;
                var background = ApplyColourInterpolation(input2FilterResult, colorInterpolationFilters)!;
                var foreground = ApplyColourInterpolation(input1FilterResult, colorInterpolationFilters);
                var skImageFilter = CreateBlend(svgBlend, background, foreground, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgColourMatrix svgColourMatrix:
            {
                var inputKey = svgColourMatrix.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateColorMatrix(svgColourMatrix, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgComponentTransfer svgComponentTransfer:
            {
                var inputKey = svgComponentTransfer.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateComponentTransfer(svgComponentTransfer, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgComposite svgComposite:
            {
                var input1Key = svgComposite.Input;
                var input1FilterResult = GetInputFilter(input1Key, colorInterpolationFilters, _skFilterRegion, isFirst);
                var input2Key = svgComposite.Input2;
                var input2FilterResult = GetInputFilter(input2Key, colorInterpolationFilters, _skFilterRegion, false);
                if (input2FilterResult is null)
                {
                    break;
                }
                
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, input1FilterResult, input2FilterResult, input1Key, input2Key);
                var skSKRect = skFilterPrimitiveRegion;
                var background = ApplyColourInterpolation(input2FilterResult, colorInterpolationFilters)!;
                var foreground = ApplyColourInterpolation(input1FilterResult, colorInterpolationFilters);
                var skImageFilter = CreateComposite(svgComposite, background, foreground, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgConvolveMatrix svgConvolveMatrix:
            {
                var inputKey = svgConvolveMatrix.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }
                
                break;
            }
            case SvgDiffuseLighting svgDiffuseLighting:
            {
                var inputKey = svgDiffuseLighting.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, _svgVisualElement, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgDisplacementMap svgDisplacementMap:
            {
                var input1Key = svgDisplacementMap.Input;
                var input1FilterResult = GetInputFilter(input1Key, colorInterpolationFilters, _skFilterRegion, isFirst);
                var input2Key = svgDisplacementMap.Input2;
                var input2FilterResult = GetInputFilter(input2Key, colorInterpolationFilters, _skFilterRegion, false);
                if (input2FilterResult is null)
                {
                    break;
                }

                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, input1FilterResult, input2FilterResult, input1Key, input2Key);
                var skSKRect = skFilterPrimitiveRegion;
                var displacement = ApplyColourInterpolation(input2FilterResult, input2FilterResult.ColorSpace)!;
                var input = ApplyColourInterpolation(input1FilterResult, colorInterpolationFilters);
                var skImageFilter = CreateDisplacementMap(svgDisplacementMap, displacement, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgFlood svgFlood:
            {  
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, null);
                var skSKRect = skFilterPrimitiveRegion;
                var skImageFilter = CreateFlood(svgFlood, _svgVisualElement, null, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, SvgColourInterpolation.SRGB);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgGaussianBlur svgGaussianBlur:
            {
                var inputKey = svgGaussianBlur.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateBlur(svgGaussianBlur, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }
                
                break;
            }
            case FilterEffects.SvgImage svgImage:
            {
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, null);
                var skSKRect = skFilterPrimitiveRegion;
                var skImageFilter = CreateImage(svgImage, _assetLoader, _references, skFilterPrimitiveRegion, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, SvgColourInterpolation.SRGB);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgMerge svgMerge:
            {
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, null);
                var skSKRect = skFilterPrimitiveRegion;
                var skImageFilter = CreateMerge(svgMerge, colorInterpolationFilters, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgMorphology svgMorphology:
            {
                var inputKey = svgMorphology.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateMorphology(svgMorphology, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgOffset svgOffset:
            {
                var inputKey = svgOffset.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = inputFilterResult?.Filter;
                var skImageFilter = CreateOffset(svgOffset, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, inputFilterResult?.ColorSpace ?? colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgSpecularLighting svgSpecularLighting:
            {
                var inputKey = svgSpecularLighting.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                var skSKRect = skFilterPrimitiveRegion;
                var input = ApplyColourInterpolation(inputFilterResult, colorInterpolationFilters);
                var skImageFilter = CreateSpecularLighting(svgSpecularLighting, _svgVisualElement, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgTile svgTile:
            {
                var inputKey = svgTile.Input;
                var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);

                if (inputFilterResult is null)
                {
                    break;
                }

                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, null);
                var tileBounds = _regions[inputFilterResult.Filter];
                var cropBounds = skFilterPrimitiveRegion;
                var skSKRect = cropBounds;
                var input = inputFilterResult.Filter;
                var skImageFilter = CreateTile(svgTile, tileBounds, skFilterPrimitiveRegion, input, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, inputFilterResult?.ColorSpace ?? colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
            case SvgTurbulence svgTurbulence:
            {
                var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, null);
                var skSKRect = skFilterPrimitiveRegion;
                var skImageFilter = CreateTurbulence(svgTurbulence, skSKRect);
                _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                if (skImageFilter is { })
                {
                    _regions[skImageFilter] = skFilterPrimitiveRegion;
                }

                break;
            }
        }
    }

    private SKRect GetFilterPrimitiveRegion(SvgFilterPrimitiveContext primitiveContext, SvgFilterResult? inputFilterResult)
    {
        var defaultSubregion = SKRect.Empty;

        if (inputFilterResult is null || IsStandardInput(inputFilterResult))
        {
            defaultSubregion = _skFilterRegion;
        }
        else
        {
            defaultSubregion = _regions[inputFilterResult.Filter];
        }

        var skFilterPrimitiveRegion = SKRect.Create(
            primitiveContext.IsXValid ? primitiveContext.Boundaries.Left : defaultSubregion.Left,
            primitiveContext.IsYValid ? primitiveContext.Boundaries.Top : defaultSubregion.Top,
            primitiveContext.IsWidthValid ? primitiveContext.Boundaries.Width : defaultSubregion.Width,
            primitiveContext.IsHeightValid ? primitiveContext.Boundaries.Height : defaultSubregion.Height);

        return skFilterPrimitiveRegion;
    }

    private SKRect GetFilterPrimitiveRegion(SvgFilterPrimitiveContext primitiveContext, SvgFilterResult? input1FilterResult, SvgFilterResult input2FilterResult, string input1Key, string input2Key)
    {
        var defaultSubregion = SKRect.Empty;

        if (IsStandardInput(input1FilterResult) || IsStandardInput(input2FilterResult))
        {
            defaultSubregion = _skFilterRegion;
        }
        else
        {
            defaultSubregion = SKRect.Union(
                input1FilterResult is null ? SKRect.Empty : _regions[input1FilterResult.Filter],
                _regions[input2FilterResult.Filter]);
        }

        var skFilterPrimitiveRegion = SKRect.Create(
            primitiveContext.IsXValid ? primitiveContext.Boundaries.Left : defaultSubregion.Left,
            primitiveContext.IsYValid ? primitiveContext.Boundaries.Top : defaultSubregion.Top,
            primitiveContext.IsWidthValid ? primitiveContext.Boundaries.Width : defaultSubregion.Width,
            primitiveContext.IsHeightValid ? primitiveContext.Boundaries.Height : defaultSubregion.Height);

        return skFilterPrimitiveRegion;
    }

    private static SvgColourInterpolation GetColorInterpolationFilters(SvgFilterPrimitive svgFilterPrimitive)
    {
        return svgFilterPrimitive.ColorInterpolationFilters switch
        {
            SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
            _ => SvgColourInterpolation.LinearRGB,
        };
    }

    private SKImageFilter? GetGraphic(SKPicture skPicture, SKRect cullRect)
    {
        var skImageFilter = SKImageFilter.CreatePicture(skPicture, cullRect);
        return skImageFilter;
    }

    private SKImageFilter? GetAlpha(SKPicture skPicture, SKRect cullRect)
    {
        var skImageFilterGraphic = GetGraphic(skPicture, cullRect);

        var matrix = new float[20]
        {
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };

        var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
        var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);

        return skImageFilter;
    }

    private SKImageFilter? GetPaint(SKPaint skPaint)
    {
        var skImageFilter = SKImageFilter.CreatePaint(skPaint);
        return skImageFilter;
    }

    private SKImageFilter GetTransparentBlackImage()
    {
        var skPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = SvgExtensions.s_transparentBlack
        };
        var skImageFilter = SKImageFilter.CreatePaint(skPaint);
        return skImageFilter;
    }

    private SKImageFilter GetTransparentBlackAlpha()
    {
        var skPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = SvgExtensions.s_transparentBlack
        };

        var skImageFilterGraphic = SKImageFilter.CreatePaint(skPaint);

        var matrix = new float[20]
        {
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };

        var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
        var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);
        return skImageFilter;
    }

    private SvgFilterResult? GetInputFilter(string? inputKey, SvgColourInterpolation dstColorSpace, SKRect cullRect, bool isFirst)
    {
        if (string.IsNullOrWhiteSpace(inputKey))
        {
            if (!isFirst)
            {
                return _lastResult;
            }

            if (_results.ContainsKey(SourceGraphic))
            {
                return _results[SourceGraphic];
            }

            var skPicture = _filterSource.SourceGraphic(cullRect);
            if (skPicture is { })
            {
                var skImageFilter = GetGraphic(skPicture, cullRect);
                if (skImageFilter is { })
                {
                    var srcColorSpace = SvgColourInterpolation.SRGB;
                    _results[SourceGraphic] = new SvgFilterResult(SourceGraphic, skImageFilter, srcColorSpace);
                    return _results[SourceGraphic];
                }
            }
            return default;
        }

        if (_results.ContainsKey(inputKey!))
        {
            return _results[inputKey!];
        }

        switch (inputKey)
        {
            case SourceGraphic:
            {
                var skPicture = _filterSource.SourceGraphic(cullRect);
                if (skPicture is { })
                {
                    var skImageFilter = GetGraphic(skPicture, cullRect);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[SourceGraphic] = new SvgFilterResult(SourceGraphic, skImageFilter, srcColorSpace);
                        return _results[SourceGraphic];
                    }
                }

                break;
            }
            case SourceAlpha:
            {
                var skPicture = _filterSource.SourceGraphic(cullRect);
                if (skPicture is { })
                {
                    var skImageFilter = GetAlpha(skPicture, cullRect);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[SourceAlpha] = new SvgFilterResult(SourceAlpha, skImageFilter, srcColorSpace);
                        return _results[SourceAlpha];
                    }
                }

                break;
            }
            case BackgroundImage:
            {
                var skPicture = _filterSource.BackgroundImage(cullRect);
                if (skPicture is { })
                {
                    var skImageFilter = GetGraphic(skPicture, cullRect);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundImage] = new SvgFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                        return _results[BackgroundImage];
                    }
                }
                else
                {
                    var skImageFilter = GetTransparentBlackImage();
                    var srcColorSpace = SvgColourInterpolation.SRGB;
                    _results[BackgroundImage] = new SvgFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                    return _results[BackgroundImage];
                }

                break;
            }
            case BackgroundAlpha:
            {
                var skPicture = _filterSource.BackgroundImage(cullRect);
                if (skPicture is { })
                {
                    var skImageFilter = GetAlpha(skPicture, cullRect);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundAlpha] = new SvgFilterResult(BackgroundAlpha, skImageFilter, srcColorSpace);
                        return _results[BackgroundAlpha];
                    }
                }
                else
                {
                    var skImageFilter = GetTransparentBlackAlpha();
                    var srcColorSpace = SvgColourInterpolation.SRGB;
                    _results[BackgroundImage] = new SvgFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                    return _results[BackgroundImage];
                }

                break;
            }
            case FillPaint:
            {
                var skPaint = _filterSource.FillPaint();
                if (skPaint is { })
                {
                    var skImageFilter = GetPaint(skPaint);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[FillPaint] = new SvgFilterResult(FillPaint, skImageFilter, srcColorSpace);
                        return _results[FillPaint];
                    }
                }

                break;
            }
            case StrokePaint:
            {
                var skPaint = _filterSource.StrokePaint();
                if (skPaint is { })
                {
                    var skImageFilter = GetPaint(skPaint);
                    if (skImageFilter is { })
                    {
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[StrokePaint] = new SvgFilterResult(StrokePaint, skImageFilter, srcColorSpace);
                        return _results[StrokePaint];
                    }
                }
                
                break;
            }
        }

        return default;
    }

    private SvgFilterResult? GetFilterResult(SvgFilterPrimitive svgFilterPrimitive, SKImageFilter? skImageFilter, SvgColourInterpolation colorSpace)
    {
        if (skImageFilter is { })
        {
            var key = svgFilterPrimitive.Result;
            var result = new SvgFilterResult(key, skImageFilter, colorSpace);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _results[key] = result;
            }
            return result;
        }
        return default;
    }

    private List<SvgFilter>? GetLinkedFilter(SvgVisualElement svgVisualElement, HashSet<Uri> uris)
    {
        var currentFilter = svgVisualElement.GetReference<SvgFilter>(svgVisualElement.Filter);
        if (currentFilter is null)
        {
            return default;
        }

        var svgFilters = new List<SvgFilter>();
        do
        {
            if (currentFilter is { })
            {
                svgFilters.Add(currentFilter);
                if (currentFilter.HasRecursiveReference((e) => e.Href, uris))
                {
                    return svgFilters;
                }
                currentFilter = currentFilter.GetReference<SvgFilter>(currentFilter.Href);
            }
        } while (currentFilter is { });

        return svgFilters;
    }

    private SKImageFilter? ApplyColourInterpolation(SvgFilterResult? input, SvgColourInterpolation dst)
    {
        if (input is null)
        {
            return null;
        }

        var src = input.ColorSpace;

        if (src == dst)
        {
            return input.Filter;
        }

        if (src == SvgColourInterpolation.SRGB && dst == SvgColourInterpolation.LinearRGB)
        {
            return SKImageFilter.CreateColorFilter(SvgExtensions.SRGBToLinearGamma(), input.Filter);
        }

        if (src == SvgColourInterpolation.LinearRGB && dst == SvgColourInterpolation.SRGB)
        {
            return SKImageFilter.CreateColorFilter(SvgExtensions.LinearToSRGBGamma(), input.Filter);
        }

        return null;
    }

    private static bool IsStandardInput(SvgFilterResult? filterResult)
    {
        return filterResult?.Key switch
        {
            SourceGraphic => true,
            SourceAlpha => true,
            BackgroundImage => true,
            BackgroundAlpha => true,
            FillPaint => true,
            StrokePaint => true,
            _ => false
        };
    }

    private float CalculateHorizontal(SvgOffset svgElement, SvgUnit unit)
    {
        var useBoundingBox = _primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox;
        var type = useBoundingBox ? UnitRenderingType.Horizontal : UnitRenderingType.HorizontalOffset;
        var value = unit.ToDeviceValue(type, svgElement, useBoundingBox ? _skBounds : _skViewport);
        if (useBoundingBox)
        {
            if (unit.Type != SvgUnitType.Percentage)
            {
                value *= _skBounds.Width;
            }
        }

        return value;
    }

    private float CalculateVertical(SvgElement svgElement, SvgUnit unit)
    {
        var useBoundingBox = _primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox;
        var type = useBoundingBox ? UnitRenderingType.Vertical : UnitRenderingType.VerticalOffset;
        var value = unit.ToDeviceValue(type, svgElement, useBoundingBox ? _skBounds : _skViewport);
        if (useBoundingBox)
        {
            if (unit.Type != SvgUnitType.Percentage)
            {
                value *= _skBounds.Height;
            }
        }

        return value;
    }

    private SKBlendMode GetBlendMode(SvgBlendMode svgBlendMode)
    {
        return svgBlendMode switch
        {
            SvgBlendMode.Normal => SKBlendMode.SrcOver,
            SvgBlendMode.Multiply => SKBlendMode.Multiply,
            SvgBlendMode.Screen => SKBlendMode.Screen,
            SvgBlendMode.Overlay => SKBlendMode.Overlay,
            SvgBlendMode.Darken => SKBlendMode.Darken,
            SvgBlendMode.Lighten => SKBlendMode.Lighten,
            SvgBlendMode.ColorDodge => SKBlendMode.ColorDodge,
            SvgBlendMode.ColorBurn => SKBlendMode.ColorBurn,
            SvgBlendMode.HardLight => SKBlendMode.HardLight,
            SvgBlendMode.SoftLight => SKBlendMode.SoftLight,
            SvgBlendMode.Difference => SKBlendMode.Difference,
            SvgBlendMode.Exclusion => SKBlendMode.Exclusion,
            SvgBlendMode.Hue => SKBlendMode.Hue,
            SvgBlendMode.Saturation => SKBlendMode.Saturation,
            SvgBlendMode.Color => SKBlendMode.Color,
            SvgBlendMode.Luminosity => SKBlendMode.Luminosity,
            _ => SKBlendMode.SrcOver,
        };
    }

    private SKImageFilter? CreateBlend(SvgBlend svgBlend, SKImageFilter background, SKImageFilter? foreground = default, SKRect? cropRect = default)
    {
        var mode = GetBlendMode(svgBlend.Mode);
        return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
    }

    private float[] CreateIdentityColorMatrixArray()
    {
        return
        [
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        ];
    }

    private SKImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        SKColorFilter skColorFilter;

        switch (svgColourMatrix.Type)
        {
            case SvgColourMatrixType.HueRotate:
                {
                    var value = string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture);
                    var hue = (float)SvgExtensions.DegreeToRadian(value);
                    var cosHue = Math.Cos(hue);
                    var sinHue = Math.Sin(hue);
                    float[] matrix =
                    [
                        (float)(0.213 + cosHue * 0.787 - sinHue * 0.213),
                        (float)(0.715 - cosHue * 0.715 - sinHue * 0.715),
                        (float)(0.072 - cosHue * 0.072 + sinHue * 0.928), 0, 0,
                        (float)(0.213 - cosHue * 0.213 + sinHue * 0.143),
                        (float)(0.715 + cosHue * 0.285 + sinHue * 0.140),
                        (float)(0.072 - cosHue * 0.072 - sinHue * 0.283), 0, 0,
                        (float)(0.213 - cosHue * 0.213 - sinHue * 0.787),
                        (float)(0.715 - cosHue * 0.715 + sinHue * 0.715),
                        (float)(0.072 + cosHue * 0.928 + sinHue * 0.072), 0, 0,
                        0, 0, 0, 1, 0
                    ];
                    skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                }
                break;

            case SvgColourMatrixType.LuminanceToAlpha:
                {
                    float[] matrix =
                    [
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0.2125f, 0.7154f, 0.0721f, 0, 0
                    ];
                    skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                }
                break;

            case SvgColourMatrixType.Saturate:
                {
                    var value = string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture);
                    float[] matrix =
                    [
                        (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                        (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                        (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                        0, 0, 0, 1, 0
                    ];
                    skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                }
                break;

            default:
            case SvgColourMatrixType.Matrix:
                {
                    float[] matrix;
                    if (string.IsNullOrEmpty(svgColourMatrix.Values))
                    {
                        matrix = CreateIdentityColorMatrixArray();
                    }
                    else
                    {
                        var parts = svgColourMatrix.Values.Split(s_colorMatrixSplitChars, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 20)
                        {
                            matrix = new float[20];
                            for (var i = 0; i < 20; i++)
                            {
                                matrix[i] = float.Parse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture);
                            }
                            matrix[4] *= 255f;
                            matrix[9] *= 255f;
                            matrix[14] *= 255f;
                            matrix[19] *= 255f;
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

    private void Identity(byte[] values, SvgComponentTransferFunction transferFunction)
    {
    }

    private void Table(byte[] values, SvgComponentTransferFunction transferFunction)
    {
        var tableValues = transferFunction.TableValues;
        var n = tableValues.Count;
        if (n < 1)
        {
            return;
        }
        for (var i = 0; i < 256; i++)
        {
            var c = (double)i / 255.0;
            var k = (byte)(c * (n - 1));
            double v1 = tableValues[k];
            double v2 = tableValues[Math.Min(k + 1, n - 1)];
            var val = 255.0 * (v1 + (c * (n - 1) - k) * (v2 - v1));
            val = Math.Max(0.0, Math.Min(255.0, val));
            values[i] = (byte)val;
        }
    }

    private void Discrete(byte[] values, SvgComponentTransferFunction transferFunction)
    {
        var tableValues = transferFunction.TableValues;

        var n = tableValues.Count;
        if (n < 1)
        {
            return;
        }

        for (var i = 0; i < 256; i++)
        {
            var c = (double)i / 255.0;
            var k = (byte)(c * (double)n);
            k = (byte)Math.Min(k, n - 1);
            double val = 255 * tableValues[k];
            val = Math.Max(0.0, Math.Min(255.0, val));
            values[i] = (byte)val;
        }
    }

    private void Linear(byte[] values, SvgComponentTransferFunction transferFunction)
    {
        var slope = transferFunction.Slope;
        var intercept = transferFunction.Intercept;

        for (var i = 0; i < 256; i++)
        {
            double val = slope * i + 255 * intercept;
            val = Math.Max(0.0, Math.Min(255.0, val));
            values[i] = (byte)val;
        }
    }

    private void Gamma(byte[] values, SvgComponentTransferFunction transferFunction)
    {
        var amplitude = transferFunction.Amplitude;
        var offset = transferFunction.Offset;
        var exponent = transferFunction.Exponent;

        for (var i = 0; i < 256; i++)
        {
            var c = (double)i / 255.0;
            var val = 255.0 * (amplitude * Math.Pow(c, exponent) + offset);
            val = Math.Max(0.0, Math.Min(255.0, val));
            values[i] = (byte)val;
        }
    }

    private void Apply(byte[] values, SvgComponentTransferFunction transferFunction)
    {
        switch (transferFunction.Type)
        {
            case SvgComponentTransferType.Identity:
                Identity(values, transferFunction);
                break;

            case SvgComponentTransferType.Table:
                Table(values, transferFunction);
                break;

            case SvgComponentTransferType.Discrete:
                Discrete(values, transferFunction);
                break;

            case SvgComponentTransferType.Linear:
                Linear(values, transferFunction);
                break;

            case SvgComponentTransferType.Gamma:
                Gamma(values, transferFunction);
                break;
        }
    }

    private SKImageFilter? CreateComponentTransfer(SvgComponentTransfer svgComponentTransfer, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var svgFuncA = s_identitySvgFuncA;
        var svgFuncR = s_identitySvgFuncR;
        var svgFuncG = s_identitySvgFuncG;
        var svgFuncB = s_identitySvgFuncB;

        foreach (var child in svgComponentTransfer.Children)
        {
            switch (child)
            {
                case SvgFuncA a:
                    svgFuncA = a;
                    break;

                case SvgFuncR r:
                    svgFuncR = r;
                    break;

                case SvgFuncG g:
                    svgFuncG = g;
                    break;

                case SvgFuncB b:
                    svgFuncB = b;
                    break;
            }
        }

        byte[] tableA = new byte[256];
        byte[] tableR = new byte[256];
        byte[] tableG = new byte[256];
        byte[] tableB = new byte[256];

        for (var i = 0; i < 256; i++)
        {
            tableA[i] = tableR[i] = tableG[i] = tableB[i] = (byte)i;
        }

        Apply(tableA, svgFuncA);
        Apply(tableR, svgFuncR);
        Apply(tableG, svgFuncG);
        Apply(tableB, svgFuncB);

        var cf = SKColorFilter.CreateTable(tableA, tableR, tableG, tableB);

        return SKImageFilter.CreateColorFilter(cf, input, cropRect);
    }

    private SKImageFilter? CreateComposite(SvgComposite svgComposite, SKImageFilter background, SKImageFilter? foreground = default, SKRect? cropRect = default)
    {
        var oper = svgComposite.Operator;
        if (oper == SvgCompositeOperator.Arithmetic)
        {
            var k1 = svgComposite.K1;
            var k2 = svgComposite.K2;
            var k3 = svgComposite.K3;
            var k4 = svgComposite.K4;
            return SKImageFilter.CreateArithmetic(k1, k2, k3, k4, false, background, foreground, cropRect);
        }
        else
        {
            var mode = oper switch
            {
                SvgCompositeOperator.Over => SKBlendMode.SrcOver,
                SvgCompositeOperator.In => SKBlendMode.SrcIn,
                SvgCompositeOperator.Out => SKBlendMode.SrcOut,
                SvgCompositeOperator.Atop => SKBlendMode.SrcATop,
                SvgCompositeOperator.Xor => SKBlendMode.Xor,
                _ => SKBlendMode.SrcOver,
            };
            return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
        }
    }

    private SKImageFilter? CreateConvolveMatrix(SvgConvolveMatrix svgConvolveMatrix, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        svgConvolveMatrix.Order.GetOptionalNumbers(3f, 3f, out var orderX, out var orderY);

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            orderX *= _skBounds.Width;
            orderY *= _skBounds.Height;
        }

        if (orderX <= 0f || orderY <= 0f)
        {
            return default;
        }

        var kernelSize = new SKSizeI((int)orderX, (int)orderY);
        var kernelMatrix = svgConvolveMatrix.KernelMatrix;

        if (kernelMatrix is null)
        {
            return default;
        }

        if (kernelSize.Width * kernelSize.Height != kernelMatrix.Count)
        {
            return default;
        }

        float[] kernel = new float[kernelMatrix.Count];

        var count = kernelMatrix.Count;
        for (var i = 0; i < count; i++)
        {
            kernel[i] = kernelMatrix[count - 1 - i];
        }

        var divisor = svgConvolveMatrix.Divisor;
        if (divisor == 0f)
        {
            foreach (var value in kernel)
            {
                divisor += value;
            }
            if (divisor == 0f)
            {
                divisor = 1f;
            }
        }

        var gain = 1f / divisor;
        var bias = svgConvolveMatrix.Bias * 255f;
        var kernelOffset = new SKPointI(svgConvolveMatrix.TargetX, svgConvolveMatrix.TargetY);
        var tileMode = svgConvolveMatrix.EdgeMode switch
        {
            SvgEdgeMode.Duplicate => SKShaderTileMode.Clamp,
            SvgEdgeMode.Wrap => SKShaderTileMode.Repeat,
            SvgEdgeMode.None => SKShaderTileMode.Decal,
            _ => SKShaderTileMode.Clamp
        };
        var convolveAlpha = !svgConvolveMatrix.PreserveAlpha;

        return SKImageFilter.CreateMatrixConvolution(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);
    }

    private SKPoint3 GetDirection(SvgDistantLight svgDistantLight)
    {
        var azimuth = svgDistantLight.Azimuth;
        var elevation = svgDistantLight.Elevation;
        var azimuthRad = SvgExtensions.DegreeToRadian(azimuth);
        var elevationRad = SvgExtensions.DegreeToRadian(elevation);
        var x = (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad));
        var y = (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad));
        var z = (float)Math.Sin(elevationRad);
        return new SKPoint3(x, y, z);
    }

    private SKPoint3 GetPoint3(float x, float y, float z)
    {
        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            x *= _skBounds.Width;
            y *= _skBounds.Height;
            z *= _skBounds.CalculateOtherPercentageValue();
        }
        return new SKPoint3(x, y, z);
    }

    private SKImageFilter? CreateDiffuseLighting(SvgDiffuseLighting svgDiffuseLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var lightColor = SvgExtensions.GetColor(svgVisualElement, svgDiffuseLighting.LightingColor);
        if (lightColor is null)
        {
            return default;
        }

        var surfaceScale = svgDiffuseLighting.SurfaceScale;
        var diffuseConstant = svgDiffuseLighting.DiffuseConstant;
        // TODO: svgDiffuseLighting.KernelUnitLength

        if (diffuseConstant < 0f)
        {
            diffuseConstant = 0f;
        }

        switch (svgDiffuseLighting.LightSource)
        {
            case SvgDistantLight svgDistantLight:
                {
                    var direction = GetDirection(svgDistantLight);
                    return SKImageFilter.CreateDistantLitDiffuse(direction, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                }
            case SvgPointLight svgPointLight:
                {
                    var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z);
                    return SKImageFilter.CreatePointLitDiffuse(location, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                }
            case SvgSpotLight svgSpotLight:
                {
                    var location = GetPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z);
                    var target = GetPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ);
                    var specularExponentSpotLight = svgSpotLight.SpecularExponent;
                    var limitingConeAngle = svgSpotLight.LimitingConeAngle;
                    if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                    {
                        limitingConeAngle = 90f;
                    }
                    return SKImageFilter.CreateSpotLitDiffuse(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                }
        }
        return default;
    }

    private SKColorChannel GetColorChannel(SvgChannelSelector svgChannelSelector)
    {
        return svgChannelSelector switch
        {
            SvgChannelSelector.R => SKColorChannel.R,
            SvgChannelSelector.G => SKColorChannel.G,
            SvgChannelSelector.B => SKColorChannel.B,
            SvgChannelSelector.A => SKColorChannel.A,
            _ => SKColorChannel.A
        };
    }

    private SKImageFilter? CreateDisplacementMap(SvgDisplacementMap svgDisplacementMap, SKImageFilter displacement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var xChannelSelector = GetColorChannel(svgDisplacementMap.XChannelSelector);
        var yChannelSelector = GetColorChannel(svgDisplacementMap.YChannelSelector);
        var scale = svgDisplacementMap.Scale;

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            scale *= _skBounds.CalculateOtherPercentageValue();
        }

        return SKImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, input, cropRect);
    }

    private SKImageFilter? CreateFlood(SvgFlood svgFlood, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var floodColor = SvgExtensions.GetColor(svgVisualElement, svgFlood.FloodColor);
        if (floodColor is null)
        {
            return default;
        }

        var floodOpacity = svgFlood.FloodOpacity;
        var floodAlpha = SvgExtensions.CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
        floodColor = new SKColor(floodColor.Value.Red, floodColor.Value.Green, floodColor.Value.Blue, floodAlpha);

        if (cropRect is null)
        {
            cropRect = _skFilterRegion;
        }

        var cf = SKColorFilter.CreateBlendMode(floodColor.Value, SKBlendMode.Src);

        return SKImageFilter.CreateColorFilter(cf, input, cropRect);
    }

    private SKImageFilter? CreateBlur(SvgGaussianBlur svgGaussianBlur, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        svgGaussianBlur.StdDeviation.GetOptionalNumbers(0f, 0f, out var sigmaX, out var sigmaY);

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = _skBounds.CalculateOtherPercentageValue();
            sigmaX *= value;
            sigmaY *= value;
        }

        if (sigmaX < 0f && sigmaY < 0f)
        {
            return default;
        }

        return SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
    }

    private SKImageFilter? CreateImage(FilterEffects.SvgImage svgImage, IAssetLoader assetLoader, HashSet<Uri>? references, SKRect skFilterPrimitiveRegion, SKRect? cropRect = default)
    {
        var uri = SvgExtensions.GetImageUri(svgImage.Href, svgImage.OwnerDocument);
        if (references is { } && references.Contains(uri))
        {
            return default;
        }

        var image = SvgExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument, assetLoader);
        var skImage = image as SKImage;
        var svgFragment = image as SvgFragment;
        if (skImage is null && svgFragment is null)
        {
            return default;
        }

        var destClip = skFilterPrimitiveRegion;

        var srcRect = default(SKRect);

        if (skImage is { })
        {
            srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
        }

        if (svgFragment is { })
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
        }

        var destRect = SvgExtensions.CalculateRect(svgImage.AspectRatio, srcRect, destClip);

        if (skImage is { })
        {
            return SKImageFilter.CreateImage(skImage, srcRect, destRect, SKFilterQuality.High);
        }

        if (svgFragment is { })
        {
            var fragmentTransform = SKMatrix.CreateIdentity();
            var dx = destRect.Left;
            var dy = destRect.Top;
            var sx = destRect.Width / srcRect.Width;
            var sy = destRect.Height / srcRect.Height;
            var skTranslationMatrix = SKMatrix.CreateTranslation(dx, dy);
            var skScaleMatrix = SKMatrix.CreateScale(sx, sy);
            fragmentTransform = fragmentTransform.PreConcat(skTranslationMatrix);
            fragmentTransform = fragmentTransform.PreConcat(skScaleMatrix);
            // TODO: fragmentTransform

            var fragmentDrawable = FragmentDrawable.Create(svgFragment, destRect, null, assetLoader, references, DrawAttributes.None);
            // TODO: fragmentDrawable.Snapshot()
            var skPicture = fragmentDrawable.Snapshot();

            return SKImageFilter.CreatePicture(skPicture, destRect);
        }

        return default;
    }

    private SKImageFilter? CreateMerge(SvgMerge svgMerge, SvgColourInterpolation colorInterpolationFilters, SKRect? cropRect = default)
    {
        var children = new List<SvgMergeNode>();

        foreach (var child in svgMerge.Children)
        {
            if (child is SvgMergeNode svgMergeNode)
            {
                children.Add(svgMergeNode);
            }
        }

        var filters = new SKImageFilter[children.Count];

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var inputKey = child.Input;
            var inputFilter = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, false);
            if (inputFilter is { })
            {
                filters[i] = ApplyColourInterpolation(inputFilter, colorInterpolationFilters)!;
            }
            else
            {
                return default;
            }
        }

        return SKImageFilter.CreateMerge(filters, cropRect);
    }

    private SKImageFilter? CreateMorphology(SvgMorphology svgMorphology, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        svgMorphology.Radius.GetOptionalNumbers(0f, 0f, out var radiusX, out var radiusY);

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = _skBounds.CalculateOtherPercentageValue();
            radiusX *= value;
            radiusY *= value;
        }

        if (radiusX <= 0f && radiusY <= 0f)
        {
            return default;
        }

        return svgMorphology.Operator switch
        {
            SvgMorphologyOperator.Dilate => SKImageFilter.CreateDilate((int)radiusX, (int)radiusY, input, cropRect),
            SvgMorphologyOperator.Erode => SKImageFilter.CreateErode((int)radiusX, (int)radiusY, input, cropRect),
            _ => null,
        };
    }

    private SKImageFilter? CreateOffset(SvgOffset svgOffset, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var dxUnit = svgOffset.Dx;
        var dyUnit = svgOffset.Dy;
        var dx = CalculateHorizontal(svgOffset, dxUnit);
        var dy = CalculateVertical(svgOffset, dyUnit);
        return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
    }

    private SKImageFilter? CreateSpecularLighting(SvgSpecularLighting svgSpecularLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var lightColor = SvgExtensions.GetColor(svgVisualElement, svgSpecularLighting.LightingColor);
        if (lightColor is null)
        {
            return default;
        }

        var surfaceScale = svgSpecularLighting.SurfaceScale;
        var specularConstant = svgSpecularLighting.SpecularConstant;
        var specularExponent = svgSpecularLighting.SpecularExponent;
        // TODO: svgSpecularLighting.KernelUnitLength

        switch (svgSpecularLighting.LightSource)
        {
            case SvgDistantLight svgDistantLight:
                {
                    var direction = GetDirection(svgDistantLight);
                    return SKImageFilter.CreateDistantLitSpecular(direction, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                }
            case SvgPointLight svgPointLight:
                {
                    var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z);
                    return SKImageFilter.CreatePointLitSpecular(location, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                }
            case SvgSpotLight svgSpotLight:
                {
                    var location = GetPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z);
                    var target = GetPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ);
                    var specularExponentSpotLight = svgSpotLight.SpecularExponent;
                    var limitingConeAngle = svgSpotLight.LimitingConeAngle;
                    if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                    {
                        limitingConeAngle = 90f;
                    }
                    return SKImageFilter.CreateSpotLitSpecular(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                }
        }
        return default;
    }

    private SKImageFilter? CreateTile(SvgTile svgTile, SKRect srcBounds, SKRect dstBounds, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var src = srcBounds;
        var dst = dstBounds;
        return SKImageFilter.CreateTile(src, dst, input);
    }

    private SKImageFilter? CreateTurbulence(SvgTurbulence svgTurbulence, SKRect? cropRect = default)
    {
        svgTurbulence.BaseFrequency.GetOptionalNumbers(0f, 0f, out var baseFrequencyX, out var baseFrequencyY);

        if (baseFrequencyX < 0f || baseFrequencyY < 0f)
        {
            return default;
        }

        var numOctaves = svgTurbulence.NumOctaves;

        if (numOctaves < 0)
        {
            return default;
        }

        var seed = svgTurbulence.Seed;

        var skPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill
        };

        SKPointI tileSize;
        switch (svgTurbulence.StitchTiles)
        {
            default:
            case SvgStitchType.NoStitch:
                tileSize = SKPointI.Empty;
                break;

            case SvgStitchType.Stitch:
                // TODO: SvgStitchType.Stitch
                tileSize = new SKPointI();
                break;
        }

        SKShader skShader;
        switch (svgTurbulence.Type)
        {
            default:
            case SvgTurbulenceType.FractalNoise:
                skShader = SKShader.CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                break;

            case SvgTurbulenceType.Turbulence:
                skShader = SKShader.CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                break;
        }

        skPaint.Shader = skShader;

        if (cropRect is null)
        {
            cropRect = _skFilterRegion;
        }

        return SKImageFilter.CreatePaint(skPaint, cropRect);
    }
}
