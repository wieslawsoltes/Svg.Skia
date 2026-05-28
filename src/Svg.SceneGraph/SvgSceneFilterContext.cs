// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;
using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal sealed class SvgSceneFilterContext
{
    private static readonly char[] s_colorMatrixSplitChars = { ' ', '\t', '\n', '\r', ',' };

    private const string SourceGraphic = "SourceGraphic";

    private const string SourceAlpha = "SourceAlpha";

    private const string BackgroundImage = "BackgroundImage";

    private const string BackgroundAlpha = "BackgroundAlpha";

    private const string FillPaint = "FillPaint";

    private const string StrokePaint = "StrokePaint";

    private static SvgFuncA s_identitySvgFuncA = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = new SvgNumberCollection()
    };

    private static SvgFuncR s_identitySvgFuncR = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = new SvgNumberCollection()
    };

    private static SvgFuncG s_identitySvgFuncG = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = new SvgNumberCollection()
    };

    private static SvgFuncB s_identitySvgFuncB = new()
    {
        Type = SvgComponentTransferType.Identity,
        TableValues = new SvgNumberCollection()
    };

    [ThreadStatic]
    private static HashSet<string>? s_activeLocalFeImageReferences;

    [ThreadStatic]
    private static HashSet<string>? s_activeFeImageDocumentReferences;

    private readonly SvgVisualElement _svgVisualElement;
    private readonly SKRect _skBounds;
    private readonly SKRect _skViewport;
    private readonly SvgSceneDocument _sceneDocument;
    private readonly ISvgSceneFilterSource _filterSource;
    private readonly ISvgAssetLoader _assetLoader;
    private readonly HashSet<Uri>? _references;
    private readonly Uri? _filterOverrideUri;
    private readonly SKMatrix _targetTransform;
    private SKRect _skFilterRegion;
    private SvgCoordinateUnits _filterUnits;
    private SvgCoordinateUnits _primitiveUnits;
    private readonly List<SvgSceneFilterPrimitiveContext> _primitives;
    private readonly Dictionary<string, SvgSceneFilterResult> _results;
    private SvgSceneFilterResult? _lastResult;
    private readonly Dictionary<SKImageFilter, SKRect> _regions;
    private Dictionary<string, object?>? _feImageResourceCache;
    private HashSet<SKImageFilter>? _linearPngGammaImageFilters;
    private bool _useTransparentBlackResult;
    private bool _usesGlobalFeImageCoordinates;
    private bool _usesNonAxisFeImageCoordinates;
    private bool _usesGlobalLayer;

    public bool IsValid { get; private set; }

    public SKRect? FilterClip { get; private set; }

    public SKPaint? FilterPaint { get; private set; }

    public bool UsesGlobalLayer => _usesGlobalLayer;

    public SKRect? GlobalClip => UsesGlobalLayer
        ? _targetTransform.MapRect(_skFilterRegion)
        : null;

    public SvgSceneFilterContext(
        SvgSceneDocument sceneDocument,
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        SKRect skViewport,
        ISvgSceneFilterSource filterSource,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Uri? filterOverrideUri = null,
        SvgSceneFilterResult? initialSourceGraphic = null,
        SKRect? initialSourceRegion = null,
        SKMatrix? targetTransform = null)
    {
        _sceneDocument = sceneDocument;
        _svgVisualElement = svgVisualElement;
        _skBounds = skBounds;
        _skViewport = skViewport;
        _filterSource = filterSource;
        _assetLoader = assetLoader;
        _references = references;
        _filterOverrideUri = filterOverrideUri;
        _targetTransform = targetTransform ?? SKMatrix.Identity;

        _primitives = new();
        _results = new();
        _regions = new();

        if (initialSourceGraphic is { })
        {
            var sourceRegion = initialSourceRegion ?? _skBounds;
            _results[SourceGraphic] = new SvgSceneFilterResult(SourceGraphic, initialSourceGraphic.Filter, initialSourceGraphic.ColorSpace);
            _results[SourceAlpha] = CreateSourceAlphaFilterResult(initialSourceGraphic.Filter, sourceRegion);
            _regions[initialSourceGraphic.Filter] = sourceRegion;
        }

        FilterPaint = Initialize() ? CreateFilterPaint() : default;
    }

    private bool Initialize()
    {
        if (_filterOverrideUri is null && TryGetCssFilterSteps(_svgVisualElement, _skBounds, out var cssFilterSteps))
        {
            if (cssFilterSteps.Count == 0)
            {
                IsValid = true;
                FilterClip = default;
                return false;
            }

            if (TryInitializeCssFilterList(cssFilterSteps, out var hasFilter))
            {
                return hasFilter;
            }

            IsValid = false;
            FilterClip = default;
            return false;
        }

        var filter = _filterOverrideUri ?? GetLegacyFilterReferenceUri(_svgVisualElement);
        if (filter is null)
        {
            IsValid = true;
            FilterClip = default;
            return false;
        }

        var svgReferencedFilters = GetLinkedFilter(_svgVisualElement, filter, new HashSet<Uri>());
        if (svgReferencedFilters is null || svgReferencedFilters.Count <= 0)
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

            if (firstX is null && SvgService.TryGetAttribute(p, "x", out _))
            {
                firstX = p;
            }

            if (firstY is null && SvgService.TryGetAttribute(p, "y", out _))
            {
                firstY = p;
            }

            if (firstWidth is null && SvgService.TryGetAttribute(p, "width", out _))
            {
                firstWidth = p;
            }

            if (firstHeight is null && SvgService.TryGetAttribute(p, "height", out _))
            {
                firstHeight = p;
            }

            if (firstFilterUnits is null && SvgService.TryGetAttribute(p, "filterUnits", out _))
            {
                firstFilterUnits = p;
            }

            if (firstPrimitiveUnits is null && SvgService.TryGetAttribute(p, "primitiveUnits", out _))
            {
                firstPrimitiveUnits = p;
            }
        }

        var xUnit = firstX?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var yUnit = firstY?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var widthUnit = firstWidth?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var heightUnit = firstHeight?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var filterUnits = firstFilterUnits?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        _filterUnits = filterUnits;
        _primitiveUnits = firstPrimitiveUnits?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

        var skFilterRegion = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, filterUnits, _skBounds, _skViewport, svgFirstFilter);
        if (skFilterRegion is null || !IsUsableRegion(skFilterRegion.Value))
        {
            IsValid = false;
            FilterClip = default;
            return false;
        }

        _skFilterRegion = skFilterRegion.Value;

        if (firstChildren is null)
        {
            _useTransparentBlackResult = true;
            return true;
        }

        foreach (var child in firstChildren.Children)
        {
            if (child is not SvgFilterPrimitive svgFilterPrimitive)
            {
                continue;
            }

            var primitiveContext = new SvgSceneFilterPrimitiveContext(svgFilterPrimitive);

            if (SvgService.TryGetAttribute(svgFilterPrimitive, "x", out var xChildString))
            {
                if (TryConvertUnit(xChildString, out var x))
                {
                    primitiveContext.IsXValid = true;
                    primitiveContext.X = x;
                }
            }
            else
            {
                primitiveContext.X = new SvgUnit(SvgUnitType.Percentage, 0);
            }

            if (SvgService.TryGetAttribute(svgFilterPrimitive, "y", out var yChildString))
            {
                if (TryConvertUnit(yChildString, out var y))
                {
                    primitiveContext.IsYValid = true;
                    primitiveContext.Y = y;
                }
            }
            else
            {
                primitiveContext.Y = new SvgUnit(SvgUnitType.Percentage, 0);
            }

            if (SvgService.TryGetAttribute(svgFilterPrimitive, "width", out var widthChildString))
            {
                if (TryConvertUnit(widthChildString, out var width))
                {
                    primitiveContext.IsWidthValid = true;
                    primitiveContext.Width = width;
                }
            }
            else
            {
                primitiveContext.Width = new SvgUnit(SvgUnitType.Percentage, 100);
            }

            if (SvgService.TryGetAttribute(svgFilterPrimitive, "height", out var heightChildString))
            {
                if (TryConvertUnit(heightChildString, out var height))
                {
                    primitiveContext.IsHeightValid = true;
                    primitiveContext.Height = height;
                }
            }
            else
            {
                primitiveContext.Height = new SvgUnit(SvgUnitType.Percentage, 100);
            }

            var boundaries = TransformsService.CalculateRect(
                primitiveContext.X,
                primitiveContext.Y,
                primitiveContext.Width,
                primitiveContext.Height,
                _primitiveUnits,
                _skBounds,
                _skViewport,
                primitiveContext.FilterPrimitive);

            if (boundaries is null || !IsUsableRegion(boundaries.Value))
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
        if (_useTransparentBlackResult)
        {
            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill,
                ImageFilter = GetTransparentBlackImage()
            };
            IsValid = true;
            FilterClip = GetFilterClip();
            return skPaint;
        }

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
            FilterClip = GetFilterClip();
            return skPaint;
        }

        IsValid = false;
        FilterClip = default;
        return default;
    }

    private SKRect? GetFilterClip()
    {
        return _usesGlobalFeImageCoordinates && !IsAxisAlignedScaleTranslate(_targetTransform)
            ? null
            : _skFilterRegion;
    }

    private bool TryInitializeTransparentBlackFilterRegion(SvgFilter? filter)
    {
        var xUnit = filter?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var yUnit = filter?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var widthUnit = filter?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var heightUnit = filter?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var filterUnits = filter?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        _filterUnits = filterUnits;
        _primitiveUnits = filter?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

        SvgElement owner = (SvgElement?)filter ?? _svgVisualElement;

        var skFilterRegion = TransformsService.CalculateRect(
            xUnit,
            yUnit,
            widthUnit,
            heightUnit,
            filterUnits,
            _skBounds,
            _skViewport,
            owner);

        if (skFilterRegion is null)
        {
            IsValid = false;
            FilterClip = default;
            return false;
        }

        _skFilterRegion = skFilterRegion.Value;
        _useTransparentBlackResult = true;
        return true;
    }

    private void CreateFilterPrimitiveFilter(SvgSceneFilterPrimitiveContext primitiveContext, bool isFirst)
    {
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var background = ApplyColourInterpolationAndClip(input2FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion)!;
                    var foreground = ApplyColourInterpolationAndClip(input1FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion, allowImplicitSourceGraphic: true);
                    var skImageFilter = CreateBlend(svgBlend, background, foreground, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateColorMatrix(svgColourMatrix, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateComponentTransfer(svgComponentTransfer, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var background = ApplyColourInterpolationAndClip(input2FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion)!;
                    var foreground = ApplyColourInterpolationAndClip(input1FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion, allowImplicitSourceGraphic: true);
                    var skImageFilter = CreateComposite(svgComposite, background, foreground, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, _svgVisualElement, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var displacement = ApplyDisplacementMapInterpolationAndClip(input2FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion)!;
                    var input = ApplyColourInterpolationAndClip(input1FilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateDisplacementMap(svgDisplacementMap, displacement, input, skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                    }

                    break;
                }
            case SvgDropShadow svgDropShadow:
                {
                    var inputKey = svgDropShadow.Input;
                    var inputFilterResult = GetInputFilter(inputKey, colorInterpolationFilters, _skFilterRegion, isFirst);
                    if (IsExplicitUnresolvedInput(inputKey, inputFilterResult))
                    {
                        var transparentImageFilter = GetTransparentBlackImage();
                        _lastResult = GetFilterResult(svgFilterPrimitive, transparentImageFilter, colorInterpolationFilters);
                        if (transparentImageFilter is { })
                        {
                            _regions[transparentImageFilter] = _skFilterRegion;
                        }

                        break;
                    }

                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, inputFilterResult);
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion, allowImplicitSourceGraphic: true);
                    var mergeInput = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateDropShadow(svgDropShadow, colorInterpolationFilters, input, mergeInput, skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                    }

                    break;
                }
            case SvgFlood svgFlood:
                {
                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, (SvgSceneFilterResult?)null);
                    var skCropRect = skFilterPrimitiveRegion;
                    var skImageFilter = CreateFlood(svgFlood, null, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateBlur(svgGaussianBlur, input, skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                    }

                    break;
                }
            case FilterEffects.SvgImage svgImage:
                {
                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, (SvgSceneFilterResult?)null);
                    var skCropRect = skFilterPrimitiveRegion;
                    var skImageFilter = CreateImage(
                        svgImage,
                        _assetLoader,
                        _references,
                        skFilterPrimitiveRegion,
                        out var hasLinearPngGamma,
                        skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, SvgColourInterpolation.SRGB);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                        if (hasLinearPngGamma)
                        {
                            _linearPngGammaImageFilters ??= new HashSet<SKImageFilter>();
                            _linearPngGammaImageFilters.Add(skImageFilter);
                        }
                    }

                    break;
                }
            case SvgMerge svgMerge:
                {
                    var mergeInputs = GetMergeInputFilters(svgMerge, colorInterpolationFilters);
                    if (mergeInputs is null)
                    {
                        break;
                    }

                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, mergeInputs);
                    var skCropRect = skFilterPrimitiveRegion;
                    var skImageFilter = CreateMerge(mergeInputs, colorInterpolationFilters, skFilterPrimitiveRegion, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateMorphology(svgMorphology, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ClipFilterInput(inputFilterResult, skFilterPrimitiveRegion);
                    var skImageFilter = CreateOffset(svgOffset, input, skCropRect);
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
                    var skCropRect = skFilterPrimitiveRegion;
                    var input = ApplyColourInterpolationAndClip(inputFilterResult, colorInterpolationFilters, skFilterPrimitiveRegion);
                    var skImageFilter = CreateSpecularLighting(svgSpecularLighting, _svgVisualElement, input, skCropRect);
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

                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, (SvgSceneFilterResult?)null);
                    var tileBounds = GetFilterResultRegion(inputFilterResult);
                    var cropBounds = skFilterPrimitiveRegion;
                    var skCropRect = cropBounds;
                    var input = inputFilterResult.Filter;
                    var skImageFilter = CreateTile(svgTile, tileBounds, skFilterPrimitiveRegion, input, skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, inputFilterResult?.ColorSpace ?? colorInterpolationFilters);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                    }

                    break;
                }
            case SvgTurbulence svgTurbulence:
                {
                    var skFilterPrimitiveRegion = GetFilterPrimitiveRegion(primitiveContext, (SvgSceneFilterResult?)null);
                    var skCropRect = skFilterPrimitiveRegion;
                    var skImageFilter = CreateTurbulence(svgTurbulence, skCropRect);
                    _lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, colorInterpolationFilters);
                    if (skImageFilter is { })
                    {
                        _regions[skImageFilter] = skFilterPrimitiveRegion;
                    }

                    break;
                }
        }
    }

    private SKRect GetFilterPrimitiveRegion(SvgSceneFilterPrimitiveContext primitiveContext, SvgSceneFilterResult? inputFilterResult)
    {
        var defaultSubregion = GetDefaultFilterPrimitiveSubregion(inputFilterResult);

        var skFilterPrimitiveRegion = SKRect.Create(
            primitiveContext.IsXValid ? primitiveContext.Boundaries.Left : defaultSubregion.Left,
            primitiveContext.IsYValid ? primitiveContext.Boundaries.Top : defaultSubregion.Top,
            primitiveContext.IsWidthValid ? primitiveContext.Boundaries.Width : defaultSubregion.Width,
            primitiveContext.IsHeightValid ? primitiveContext.Boundaries.Height : defaultSubregion.Height);

        return NormalizeFilterPrimitiveRegion(skFilterPrimitiveRegion);
    }

    private SKRect GetFilterPrimitiveRegion(SvgSceneFilterPrimitiveContext primitiveContext, SvgSceneFilterResult? input1FilterResult, SvgSceneFilterResult input2FilterResult, string input1Key, string input2Key)
    {
        var defaultSubregion = GetDefaultFilterPrimitiveSubregion(input1FilterResult, input2FilterResult);

        var skFilterPrimitiveRegion = SKRect.Create(
            primitiveContext.IsXValid ? primitiveContext.Boundaries.Left : defaultSubregion.Left,
            primitiveContext.IsYValid ? primitiveContext.Boundaries.Top : defaultSubregion.Top,
            primitiveContext.IsWidthValid ? primitiveContext.Boundaries.Width : defaultSubregion.Width,
            primitiveContext.IsHeightValid ? primitiveContext.Boundaries.Height : defaultSubregion.Height);

        return NormalizeFilterPrimitiveRegion(skFilterPrimitiveRegion);
    }

    private SKRect GetFilterPrimitiveRegion(SvgSceneFilterPrimitiveContext primitiveContext, IReadOnlyList<SvgSceneFilterResult> inputFilterResults)
    {
        var defaultSubregion = GetDefaultFilterPrimitiveSubregion(inputFilterResults);

        var skFilterPrimitiveRegion = SKRect.Create(
            primitiveContext.IsXValid ? primitiveContext.Boundaries.Left : defaultSubregion.Left,
            primitiveContext.IsYValid ? primitiveContext.Boundaries.Top : defaultSubregion.Top,
            primitiveContext.IsWidthValid ? primitiveContext.Boundaries.Width : defaultSubregion.Width,
            primitiveContext.IsHeightValid ? primitiveContext.Boundaries.Height : defaultSubregion.Height);

        return NormalizeFilterPrimitiveRegion(skFilterPrimitiveRegion);
    }

    private SKRect GetDefaultFilterPrimitiveSubregion(params SvgSceneFilterResult?[] inputFilterResults)
        => GetDefaultFilterPrimitiveSubregion((IReadOnlyList<SvgSceneFilterResult?>)inputFilterResults);

    private SKRect GetDefaultFilterPrimitiveSubregion(IReadOnlyList<SvgSceneFilterResult?> inputFilterResults)
    {
        if (inputFilterResults.Count == 0)
        {
            return _skFilterRegion;
        }

        var hasUnion = false;
        var union = SKRect.Empty;

        for (var i = 0; i < inputFilterResults.Count; i++)
        {
            var inputFilterResult = inputFilterResults[i];
            if (inputFilterResult is null)
            {
                return _skFilterRegion;
            }

            if (IsStandardInput(inputFilterResult))
            {
                return _skFilterRegion;
            }

            var region = GetFilterResultRegion(inputFilterResult);
            if (!IsUsableRegion(region))
            {
                continue;
            }

            union = hasUnion ? SKRect.Union(union, region) : region;
            hasUnion = true;
        }

        return hasUnion ? union : SKRect.Empty;
    }

    private SKRect GetFilterResultRegion(SvgSceneFilterResult? inputFilterResult)
    {
        if (inputFilterResult is null || IsStandardInput(inputFilterResult))
        {
            return _skFilterRegion;
        }

        return _regions.TryGetValue(inputFilterResult.Filter, out var region)
            ? region
            : _skFilterRegion;
    }

    private static SvgColourInterpolation GetColorInterpolationFilters(SvgFilterPrimitive svgFilterPrimitive)
    {
        return svgFilterPrimitive.ColorInterpolationFilters switch
        {
            // SVG filter primitives default color-interpolation-filters to linearRGB.
            SvgColourInterpolation.Auto => SvgColourInterpolation.LinearRGB,
            SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
            SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
            _ => SvgColourInterpolation.LinearRGB,
        };
    }

    internal static bool HasFilterDeclaration(SvgVisualElement visualElement)
    {
        if (TryGetCssFilterSteps(visualElement, SKRect.Empty, out var cssFilterSteps))
        {
            return cssFilterSteps.Count > 0;
        }

        var filter = GetLegacyFilterReferenceUri(visualElement);
        if (filter is { } &&
            ResolveFilterReference(visualElement, filter) is not null)
        {
            return true;
        }

        return false;
    }

    internal static Uri? GetFilterReferenceUri(SvgVisualElement visualElement)
    {
        foreach (var filterUri in GetFilterReferenceUris(visualElement))
        {
            return filterUri;
        }

        return null;
    }

    internal static IEnumerable<Uri> GetFilterReferenceUris(SvgVisualElement visualElement)
    {
        if (TryGetCssFilterSteps(visualElement, SKRect.Empty, out var cssFilterSteps))
        {
            foreach (var step in cssFilterSteps)
            {
                if (step.Uri is { } uri)
                {
                    yield return uri;
                }
            }

            yield break;
        }

        if (GetLegacyFilterReferenceUri(visualElement) is { } filter)
        {
            yield return filter;
        }
    }

    private static Uri? GetLegacyFilterReferenceUri(SvgVisualElement visualElement)
    {
        if (visualElement.Filter is { } filter &&
            !FilterEffectsService.IsNone(filter) &&
            NormalizeFilterReferenceUri(filter) is { } normalizedFilter)
        {
            return normalizedFilter;
        }

        return TryGetRawFilterReferenceUri(visualElement, out var rawFilterUri)
            ? rawFilterUri
            : null;
    }

    private static bool TryGetRawFilterReferenceUri(SvgVisualElement visualElement, out Uri? filterUri)
    {
        filterUri = null;
        if (!SvgService.TryGetAttribute(visualElement, "filter", out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = 0;
        if (TryConsumeCssUrl(value, ref index, out var cssUrl) &&
            cssUrl is { } &&
            IsOnlyCssWhitespace(value, index))
        {
            filterUri = NormalizeFilterReferenceUri(cssUrl);
            return filterUri is { };
        }

        if (value.IndexOf('#') < 0 ||
            value.IndexOf('(') >= 0 ||
            !Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var rawUri))
        {
            return false;
        }

        filterUri = NormalizeFilterReferenceUri(rawUri);
        return filterUri is { };
    }

    private bool TryInitializeCssFilterList(List<CssFilterStep> steps, out bool hasFilter)
    {
        hasFilter = false;
        SKImageFilter? current = null;
        var currentRegion = _skBounds;

        foreach (var step in steps)
        {
            if (step.Function is { } function)
            {
                var filterRegion = CalculateCssFilterRegion(currentRegion, new List<CssFilterFunction> { function });
                current = CreateCssFilterFunction(function, current, filterRegion);
                if (current is null)
                {
                    return false;
                }

                currentRegion = filterRegion;
                continue;
            }

            if (step.Uri is not { } uri)
            {
                return false;
            }

            if (ResolveFilterReference(_svgVisualElement, uri) is null)
            {
                continue;
            }

            var sourceGraphic = current is { }
                ? new SvgSceneFilterResult(SourceGraphic, current, SvgColourInterpolation.SRGB)
                : null;
            var filterContext = new SvgSceneFilterContext(
                _sceneDocument,
                _svgVisualElement,
                _skBounds,
                _skViewport,
                _filterSource,
                _assetLoader,
                _references,
                uri,
                sourceGraphic,
                currentRegion,
                _targetTransform);

            if (!filterContext.IsValid || filterContext.FilterPaint?.ImageFilter is not { } imageFilter)
            {
                return false;
            }

            _usesGlobalFeImageCoordinates |= filterContext._usesGlobalFeImageCoordinates;
            _usesNonAxisFeImageCoordinates |= filterContext._usesNonAxisFeImageCoordinates;
            _usesGlobalLayer |= filterContext.UsesGlobalLayer && steps.Count == 1 && sourceGraphic is null;
            current = imageFilter;
            currentRegion = filterContext.FilterClip ?? currentRegion;
        }

        if (current is null)
        {
            IsValid = true;
            FilterClip = default;
            return true;
        }

        _skFilterRegion = currentRegion;
        _lastResult = new SvgSceneFilterResult(null, current, SvgColourInterpolation.SRGB);
        hasFilter = true;
        return true;
    }

    private SKImageFilter? CreateCssFilterFunction(CssFilterFunction function, SKImageFilter? input, SKRect cropRect)
    {
        switch (function.Name)
        {
            case "blur":
                return SKImageFilter.CreateBlur(function.Values[0], function.Values[0], input, cropRect);
            case "brightness":
                return CreateCssColorMatrixFilter(CreateBrightnessMatrix(function.Values[0]), input, cropRect);
            case "contrast":
                return CreateCssColorMatrixFilter(CreateContrastMatrix(function.Values[0]), input, cropRect);
            case "grayscale":
                return CreateCssColorMatrixFilter(CreateGrayscaleMatrix(function.Values[0]), input, cropRect);
            case "hue-rotate":
                return CreateCssColorMatrixFilter(CreateHueRotateMatrix(function.Values[0]), input, cropRect);
            case "invert":
                return CreateCssColorMatrixFilter(CreateInvertMatrix(function.Values[0]), input, cropRect);
            case "opacity":
                return CreateCssColorMatrixFilter(CreateOpacityMatrix(function.Values[0]), input, cropRect);
            case "saturate":
                return CreateCssColorMatrixFilter(CreateSaturateMatrix(function.Values[0]), input, cropRect);
            case "sepia":
                return CreateCssColorMatrixFilter(CreateSepiaMatrix(function.Values[0]), input, cropRect);
            case "drop-shadow":
                return CreateCssDropShadow(function, input, cropRect);
            default:
                return null;
        }
    }

    private static SKImageFilter CreateCssColorMatrixFilter(float[] matrix, SKImageFilter? input, SKRect cropRect)
        => SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(matrix), input, cropRect);

    private static SKImageFilter? CreateCssDropShadow(CssFilterFunction function, SKImageFilter? input, SKRect cropRect)
    {
        var dx = function.Values[0];
        var dy = function.Values[1];
        var sigma = function.Values[2];
        var color = function.Color;

        var alphaMatrix = new float[]
        {
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };
        var alpha = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(alphaMatrix), input);
        var blurredAlpha = SKImageFilter.CreateBlur(sigma, sigma, alpha, cropRect);
        var offsetAlpha = SKImageFilter.CreateOffset(dx, dy, blurredAlpha, cropRect);

        var colorizeMatrix = new float[]
        {
            0f, 0f, 0f, color.Red / 255f, 0f,
            0f, 0f, 0f, color.Green / 255f, 0f,
            0f, 0f, 0f, color.Blue / 255f, 0f,
            0f, 0f, 0f, color.Alpha / 255f, 0f
        };
        var shadow = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(colorizeMatrix), offsetAlpha, cropRect);
        var source = input ?? CreateCssColorMatrixFilter(CreateIdentityColorMatrix(), null, cropRect);

        return SKImageFilter.CreateMerge(new[] { shadow, source }, cropRect);
    }

    private static SKRect CalculateCssFilterRegion(SKRect sourceBounds, List<CssFilterFunction> functions)
    {
        var region = sourceBounds;

        if (!IsUsableRegion(region))
        {
            return region;
        }

        foreach (var function in functions)
        {
            switch (function.Name)
            {
                case "blur":
                    region = Inflate(region, function.Values[0] * 3f, function.Values[0] * 3f);
                    break;
                case "drop-shadow":
                    var shadow = Offset(region, function.Values[0], function.Values[1]);
                    shadow = Inflate(shadow, function.Values[2] * 3f, function.Values[2] * 3f);
                    region = SKRect.Union(region, shadow);
                    break;
            }
        }

        return region;
    }

    private static bool TryGetCssFilterSteps(SvgVisualElement visualElement, SKRect bounds, out List<CssFilterStep> steps)
    {
        steps = new List<CssFilterStep>();

        if (visualElement.TryGetOwnCascadedCssDeclarationValue("filter", out var cssValue))
        {
            return TryGetCssFilterStepsFromValue(cssValue, visualElement, bounds, invalidVarComputesToNone: true, allowUnitlessLengths: false, out steps);
        }

        if (TryGetInlineStyleDeclarationValue(visualElement, "filter", out cssValue))
        {
            return TryGetCssFilterStepsFromValue(cssValue, visualElement, bounds, invalidVarComputesToNone: true, allowUnitlessLengths: false, out steps);
        }

        if (!visualElement.TryGetOwnCascadedStyleValue("filter", out var value))
        {
            return false;
        }

        var containsVar = ContainsCssVarFunction(value);
        var isPresentationAttribute =
            (SvgService.TryGetAttribute(visualElement, "filter", out var attributeValue) &&
             string.Equals(value.Trim(), attributeValue.Trim(), StringComparison.Ordinal)) ||
            (visualElement.TryGetOwnPresentationStyleValue("filter", out var presentationValue) &&
             string.Equals(value.Trim(), presentationValue.Trim(), StringComparison.Ordinal));
        var useCssValueRules = containsVar || !isPresentationAttribute;
        return TryGetCssFilterStepsFromValue(
            value,
            visualElement,
            bounds,
            invalidVarComputesToNone: useCssValueRules,
            allowUnitlessLengths: !useCssValueRules,
            out steps);
    }

    private static bool TryGetInlineStyleDeclarationValue(SvgElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.CustomAttributes.TryGetValue("style", out var styleText) ||
            string.IsNullOrWhiteSpace(styleText))
        {
            return false;
        }

        var found = false;
        var index = 0;
        while (index < styleText.Length)
        {
            var declarationStart = index;
            if (!TryFindInlineStyleDeclarationEnd(styleText, ref index, out var declarationEnd))
            {
                return false;
            }

            if (TryFindInlineStyleDeclarationSeparator(styleText, declarationStart, declarationEnd, out var separatorIndex))
            {
                var name = styleText.Substring(declarationStart, separatorIndex - declarationStart).Trim();
                if (string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = styleText.Substring(separatorIndex + 1, declarationEnd - separatorIndex - 1).Trim();
                    found = true;
                }
            }

            index++;
        }

        return found;
    }

    private static bool TryFindInlineStyleDeclarationEnd(string styleText, ref int index, out int declarationEnd)
    {
        var quote = '\0';
        var escape = false;
        var depth = 0;
        for (; index < styleText.Length; index++)
        {
            var current = styleText[index];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (current == '\\')
            {
                escape = true;
                continue;
            }

            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (current == ';' && depth == 0)
            {
                declarationEnd = index;
                return true;
            }
        }

        declarationEnd = styleText.Length;
        return quote == '\0' && depth == 0;
    }

    private static bool TryFindInlineStyleDeclarationSeparator(string styleText, int start, int end, out int separatorIndex)
    {
        var quote = '\0';
        var escape = false;
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var current = styleText[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (current == '\\')
            {
                escape = true;
                continue;
            }

            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (current == ':' && depth == 0)
            {
                separatorIndex = i;
                return true;
            }
        }

        separatorIndex = -1;
        return false;
    }

    private static bool TryGetCssFilterStepsFromValue(
        string value,
        SvgVisualElement visualElement,
        SKRect bounds,
        bool invalidVarComputesToNone,
        bool allowUnitlessLengths,
        out List<CssFilterStep> steps)
    {
        steps = new List<CssFilterStep>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return invalidVarComputesToNone;
        }

        value = value.Trim();
        if (string.Equals(value, "initial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "revert", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "revert-layer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var containsVar = ContainsCssVarFunction(value);
        if (SvgCssVariableResolver.TryResolveValue(visualElement, value, out var resolvedValue))
        {
            value = resolvedValue.Trim();
        }
        else if (containsVar && invalidVarComputesToNone)
        {
            return true;
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryParseCssFilterList(value, visualElement, bounds, allowUnitlessLengths, out steps) ||
            steps.Count == 0)
        {
            steps.Clear();
            return invalidVarComputesToNone;
        }

        return true;
    }

    private static bool ContainsCssVarFunction(string value)
    {
        for (var i = 0; i + 4 <= value.Length; i++)
        {
            if (i > 0 && IsCssIdentifierCharacter(value[i - 1]))
            {
                continue;
            }

            if (value.AsSpan(i, 3).Equals("var".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                value[i + 3] == '(')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCssIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '-' or '_';
    }

    private static bool TryConsumeCssUrl(string value, ref int index, out Uri? filterUri)
    {
        filterUri = null;
        SkipCssWhitespace(value, ref index);
        if (value.Length - index < 4 ||
            !value.Substring(index, 4).Equals("url(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = index + 4;
        var current = start;
        var quote = '\0';
        while (current < value.Length)
        {
            var ch = value[current];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                current++;
                continue;
            }

            if (ch == ')')
            {
                var inner = value.Substring(start, current - start).Trim();
                if (inner.Length >= 2 &&
                    ((inner[0] == '"' && inner[inner.Length - 1] == '"') ||
                     (inner[0] == '\'' && inner[inner.Length - 1] == '\'')))
                {
                    inner = inner.Substring(1, inner.Length - 2).Trim();
                }

                if (inner.Length <= 0 || !Uri.TryCreate(inner, UriKind.RelativeOrAbsolute, out filterUri))
                {
                    filterUri = null;
                    return false;
                }

                index = current + 1;
                return true;
            }

            current++;
        }

        return false;
    }

    private static bool TryParseCssFilterList(string value, SvgVisualElement? visualElement, SKRect bounds, bool allowUnitlessLengths, out List<CssFilterStep> steps)
    {
        steps = new List<CssFilterStep>();
        var index = 0;

        while (index < value.Length)
        {
            SkipCssWhitespace(value, ref index);
            if (index >= value.Length)
            {
                break;
            }

            if (TryConsumeCssUrl(value, ref index, out var filterUri))
            {
                if (filterUri is null)
                {
                    return false;
                }

                steps.Add(new CssFilterStep(filterUri));
                continue;
            }

            var nameStart = index;
            while (index < value.Length && (char.IsLetter(value[index]) || value[index] == '-'))
            {
                index++;
            }

            if (index == nameStart)
            {
                return false;
            }

            var name = value.Substring(nameStart, index - nameStart).ToLowerInvariant();
            SkipCssWhitespace(value, ref index);
            if (index >= value.Length || value[index] != '(')
            {
                return false;
            }

            var argsStart = ++index;
            var depth = 1;
            while (index < value.Length && depth > 0)
            {
                if (value[index] == '(')
                {
                    depth++;
                }
                else if (value[index] == ')')
                {
                    depth--;
                }

                index++;
            }

            if (depth != 0)
            {
                return false;
            }

            var args = value.Substring(argsStart, index - argsStart - 1).Trim();
            if (!TryParseCssFilterFunction(name, args, visualElement, bounds, allowUnitlessLengths, out var function))
            {
                return false;
            }

            steps.Add(new CssFilterStep(function));
        }

        return true;
    }

    private static bool TryParseCssFilterFunction(string name, string args, SvgVisualElement? visualElement, SKRect bounds, bool allowUnitlessLengths, out CssFilterFunction function)
    {
        function = default;

        switch (name)
        {
            case "blur":
                return TryParseCssLengthOrDefault(args, 0f, visualElement, bounds, allowUnitlessLengths, out var blur) &&
                       blur >= 0f &&
                       TryCreateFunction(name, new[] { blur }, out function);
            case "brightness":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: false, out var brightness) &&
                       TryCreateFunction(name, new[] { brightness }, out function);
            case "contrast":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: false, out var contrast) &&
                       TryCreateFunction(name, new[] { contrast }, out function);
            case "grayscale":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: true, out var grayscale) &&
                       TryCreateFunction(name, new[] { grayscale }, out function);
            case "hue-rotate":
                return TryParseCssAngleOrDefault(args, 0f, out var radians) &&
                       TryCreateFunction(name, new[] { radians }, out function);
            case "invert":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: true, out var invert) &&
                       TryCreateFunction(name, new[] { invert }, out function);
            case "opacity":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: true, out var opacity) &&
                       TryCreateFunction(name, new[] { opacity }, out function);
            case "saturate":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: false, out var saturate) &&
                       TryCreateFunction(name, new[] { saturate }, out function);
            case "sepia":
                return TryParseCssFactorOrDefault(args, 1f, allowNegative: false, clampUnit: true, out var sepia) &&
                       TryCreateFunction(name, new[] { sepia }, out function);
            case "drop-shadow":
                return TryParseCssDropShadow(args, visualElement, bounds, allowUnitlessLengths, out function);
            default:
                return false;
        }
    }

    private static bool TryParseCssDropShadow(string args, SvgVisualElement? visualElement, SKRect bounds, bool allowUnitlessLengths, out CssFilterFunction function)
    {
        function = default;
        var tokens = SplitCssFilterArgs(args);
        if (tokens.Count < 2)
        {
            return false;
        }

        SKColor color = GetCurrentColor(visualElement);
        var lengths = new List<float>(3);
        foreach (var token in tokens)
        {
            if (lengths.Count < 3 && TryParseCssLength(token, visualElement, bounds, allowUnitlessLengths, out var length))
            {
                lengths.Add(length);
                continue;
            }

            if (!TryParseCssColor(token, visualElement, out color))
            {
                return false;
            }
        }

        if (lengths.Count < 2)
        {
            return false;
        }

        var blur = lengths.Count >= 3 ? lengths[2] : 0f;
        if (blur < 0f)
        {
            return false;
        }

        function = new CssFilterFunction("drop-shadow", new[] { lengths[0], lengths[1], blur }, color);
        return true;
    }

    private static bool TryCreateFunction(string name, float[] values, out CssFilterFunction function)
    {
        function = new CssFilterFunction(name, values, new SKColor(0, 0, 0, 255));
        return true;
    }

    private static bool TryParseCssLengthOrDefault(string value, float defaultValue, SvgVisualElement? visualElement, SKRect bounds, bool allowUnitlessLengths, out float length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            length = defaultValue;
            return true;
        }

        return TryParseCssLength(value, visualElement, bounds, allowUnitlessLengths, out length);
    }

    private static bool TryParseCssLength(string value, SvgVisualElement? visualElement, SKRect bounds, bool allowUnitlessLengths, out float length)
    {
        value = value.Trim();
        if (TryEvaluateCssMath(value, (string token, out CssCalcValue parsed) => TryParseCssLengthToken(token, visualElement, bounds, out parsed), out var calcValue))
        {
            length = calcValue.Value;
            return (allowUnitlessLengths || IsCssLengthValue(calcValue)) && IsFinite(length);
        }

        if (StartsWithCssMathFunction(value))
        {
            length = default;
            return false;
        }

        if (TryParseCssLengthToken(value, visualElement, bounds, out var parsedLength))
        {
            length = parsedLength.Value;
            return allowUnitlessLengths || IsCssLengthValue(parsedLength);
        }

        length = default;
        return false;
    }

    private static bool IsCssLengthValue(CssCalcValue value)
    {
        return value.Kind == CssCalcValueKind.Length ||
               (value.Kind == CssCalcValueKind.Number && Math.Abs(value.Value) <= float.Epsilon);
    }

    private static bool TryParseCssLengthToken(string value, SvgVisualElement? visualElement, SKRect bounds, out CssCalcValue length)
    {
        length = default;
        value = value.Trim();
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) && IsFinite(amount))
        {
            length = new CssCalcValue(amount, CssCalcValueKind.Number);
            return true;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan());
            if (unit.Type == SvgUnitType.Percentage || unit.IsEmpty || unit.IsNone)
            {
                return false;
            }

            var deviceValue = unit.ToDeviceValue(UnitRenderingType.Other, visualElement, bounds);
            if (IsFinite(deviceValue))
            {
                length = new CssCalcValue(deviceValue, CssCalcValueKind.Length);
                return true;
            }
        }
        catch (FormatException)
        {
            // Invalid lengths make the whole CSS filter function invalid.
        }

        return false;
    }

    private static bool TryParseCssFactorOrDefault(string value, float defaultValue, bool allowNegative, bool clampUnit, out float factor)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            factor = defaultValue;
            return true;
        }

        if (TryEvaluateCssMath(value, TryParseCssFactorToken, out var calcValue))
        {
            factor = calcValue.Value;
            if (!allowNegative && factor < 0f)
            {
                return false;
            }

            if (clampUnit)
            {
                factor = Math.Min(Math.Max(factor, 0f), 1f);
            }

            return true;
        }

        if (StartsWithCssMathFunction(value))
        {
            factor = default;
            return false;
        }

        if (!TryParseCssFactorToken(value, out var parsedFactor))
        {
            factor = default;
            return false;
        }

        factor = parsedFactor.Value;
        if (!allowNegative && factor < 0f)
        {
            return false;
        }

        if (clampUnit)
        {
            factor = Math.Min(Math.Max(factor, 0f), 1f);
        }

        return true;
    }

    private static bool TryParseCssFactorToken(string value, out CssCalcValue factor)
    {
        value = value.Trim();
        var isPercent = value.EndsWith("%", StringComparison.Ordinal);
        if (isPercent)
        {
            value = value.Substring(0, value.Length - 1);
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || !IsFinite(amount))
        {
            factor = default;
            return false;
        }

        if (isPercent)
        {
            amount /= 100f;
        }

        factor = new CssCalcValue(amount, CssCalcValueKind.Number);
        return true;
    }

    private static bool TryParseCssAngleOrDefault(string value, float defaultValue, out float radians)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            radians = defaultValue;
            return true;
        }

        if (TryEvaluateCssMath(value, TryParseCssAngleToken, out var calcValue))
        {
            radians = calcValue.Kind == CssCalcValueKind.Number
                ? calcValue.Value * ((float)Math.PI / 180f)
                : calcValue.Value;
            return IsFinite(radians);
        }

        if (StartsWithCssMathFunction(value))
        {
            radians = default;
            return false;
        }

        if (TryParseCssAngleToken(value, out var parsedAngle))
        {
            radians = parsedAngle.Kind == CssCalcValueKind.Number
                ? parsedAngle.Value * ((float)Math.PI / 180f)
                : parsedAngle.Value;
            return true;
        }

        radians = default;
        return false;
    }

    private static bool TryParseCssAngleToken(string value, out CssCalcValue radians)
    {
        value = value.Trim();
        var multiplier = (float)Math.PI / 180f;
        var hasAngleUnit = false;
        if (value.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3);
            hasAngleUnit = true;
        }
        else if (value.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 4);
            multiplier = (float)Math.PI / 200f;
            hasAngleUnit = true;
        }
        else if (value.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3);
            multiplier = 1f;
            hasAngleUnit = true;
        }
        else if (value.EndsWith("turn", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 4);
            multiplier = (float)Math.PI * 2f;
            hasAngleUnit = true;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || !IsFinite(amount))
        {
            radians = default;
            return false;
        }

        radians = hasAngleUnit
            ? new CssCalcValue(amount * multiplier, CssCalcValueKind.Angle)
            : new CssCalcValue(amount, CssCalcValueKind.Number);
        return true;
    }

    private static bool TryEvaluateCssMath(string value, CssCalcTokenParser tokenParser, out CssCalcValue result)
    {
        result = default;
        value = value.Trim();
        if (!StartsWithCssMathFunction(value))
        {
            return false;
        }

        var parser = new CssCalcParser(value, tokenParser);
        return parser.TryParse(out result);
    }

    private static bool StartsWithCssMathFunction(string value)
    {
        value = value.TrimStart();
        return value.StartsWith("calc(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("min(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("max(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("clamp(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseCssColor(string value, SvgVisualElement? visualElement, out SKColor color)
    {
        value = value.Trim();
        if (string.Equals(value, "currentColor", StringComparison.OrdinalIgnoreCase))
        {
            color = GetCurrentColor(visualElement);
            return true;
        }

        if (SvgPaintServerFactory.TryParseCssConcreteColor(value, out var parsed))
        {
            color = new SKColor(parsed.R, parsed.G, parsed.B, parsed.A);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryParseCssFunctionalColor(string value, out SKColor color)
    {
        return TryParseCssRgbColorFunction(value, out color) ||
               TryParseCssHslColorFunction(value, out color);
    }

    private static bool TryParseCssRgbColorFunction(string value, out SKColor color)
    {
        color = default;
        if (!TryGetCssFunctionContent(value, "rgb", "rgba", out var content) ||
            !TrySplitCssColorComponents(content, out var components, out var alphaToken) ||
            components.Count != 3)
        {
            return false;
        }

        if (!TryParseCssRgbComponent(components[0], out var red) ||
            !TryParseCssRgbComponent(components[1], out var green) ||
            !TryParseCssRgbComponent(components[2], out var blue) ||
            !TryParseCssAlpha(alphaToken, out var alpha))
        {
            return false;
        }

        color = new SKColor(red, green, blue, alpha);
        return true;
    }

    private static bool TryParseCssHslColorFunction(string value, out SKColor color)
    {
        color = default;
        if (!TryGetCssFunctionContent(value, "hsl", "hsla", out var content) ||
            !TrySplitCssColorComponents(content, out var components, out var alphaToken) ||
            components.Count != 3)
        {
            return false;
        }

        if (!TryParseCssHue(components[0], out var hue) ||
            !TryParseCssPercentage01(components[1], out var saturation) ||
            !TryParseCssPercentage01(components[2], out var lightness) ||
            !TryParseCssAlpha(alphaToken, out var alpha))
        {
            return false;
        }

        color = CreateColorFromHsl(hue, saturation, lightness, alpha);
        return true;
    }

    private static bool TryGetCssFunctionContent(string value, string name, string alias, out string content)
    {
        content = string.Empty;
        var openParenthesis = value.IndexOf('(');
        if (openParenthesis <= 0 ||
            !TryFindCssFunctionEnd(value, openParenthesis, out var closeParenthesis) ||
            closeParenthesis != value.Length - 1)
        {
            return false;
        }

        var functionName = value.Substring(0, openParenthesis).Trim();
        if (!functionName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            !functionName.Equals(alias, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        content = value.Substring(openParenthesis + 1, closeParenthesis - openParenthesis - 1).Trim();
        return content.Length > 0;
    }

    private static bool TryFindCssFunctionEnd(string value, int openParenthesisIndex, out int closeParenthesisIndex)
    {
        closeParenthesisIndex = -1;
        var quote = '\0';
        var escape = false;
        var depth = 0;
        for (var i = openParenthesisIndex; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    closeParenthesisIndex = i;
                    return true;
                }

                if (depth < 0)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static bool TrySplitCssColorComponents(string value, out List<string> components, out string? alpha)
    {
        components = new List<string>(3);
        alpha = null;

        var commaParts = SplitTopLevelCssArguments(value, ',');
        if (commaParts.Count > 1)
        {
            if (commaParts.Count != 3 && commaParts.Count != 4)
            {
                return false;
            }

            AddFirstThree(components, commaParts);
            alpha = commaParts.Count == 4 ? commaParts[3] : null;
            return true;
        }

        var tokens = SplitCssColorSpaceTokens(value);
        var slashIndex = tokens.IndexOf("/");
        if (slashIndex >= 0)
        {
            if (slashIndex != 3 ||
                tokens.Count != 5 ||
                tokens.LastIndexOf("/") != slashIndex)
            {
                return false;
            }

            AddFirstThree(components, tokens);
            alpha = tokens[4];
            return true;
        }

        if (tokens.Count != 3)
        {
            return false;
        }

        components.AddRange(tokens);
        return true;
    }

    private static void AddFirstThree(List<string> target, List<string> source)
    {
        target.Add(source[0]);
        target.Add(source[1]);
        target.Add(source[2]);
    }

    private static List<string> SplitTopLevelCssArguments(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (ch == separator && depth == 0)
            {
                parts.Add(value.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        parts.Add(value.Substring(start).Trim());
        return parts;
    }

    private static List<string> SplitCssColorSpaceTokens(string value)
    {
        var tokens = new List<string>();
        var start = -1;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }

            if ((char.IsWhiteSpace(ch) || ch == '/') && depth == 0)
            {
                if (start >= 0)
                {
                    tokens.Add(value.Substring(start, i - start));
                    start = -1;
                }

                if (ch == '/')
                {
                    tokens.Add("/");
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(value.Substring(start));
        }

        return tokens;
    }

    private static bool TryParseCssRgbComponent(string value, out byte component)
    {
        component = 0;
        var componentText = value.Trim();
        var isPercent = componentText.EndsWith("%", StringComparison.Ordinal);
        if (isPercent)
        {
            componentText = componentText.Substring(0, componentText.Length - 1).Trim();
        }

        if (!float.TryParse(componentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !IsFinite(parsed))
        {
            return false;
        }

        var scaled = isPercent ? parsed * 255f / 100f : parsed;
        component = ToByte(scaled);
        return true;
    }

    private static bool TryParseCssAlpha(string? value, out byte alpha)
    {
        alpha = 255;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var alphaText = value!.Trim();
        var isPercent = alphaText.EndsWith("%", StringComparison.Ordinal);
        if (isPercent)
        {
            alphaText = alphaText.Substring(0, alphaText.Length - 1).Trim();
        }

        if (!float.TryParse(alphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !IsFinite(parsed))
        {
            return false;
        }

        var normalized = isPercent ? parsed / 100f : parsed;
        alpha = ToByte(Clamp01(normalized) * 255f);
        return true;
    }

    private static bool TryParseCssHue(string value, out float degrees)
    {
        degrees = default;
        if (!TryParseCssAngleToken(value, out var parsed))
        {
            return false;
        }

        degrees = parsed.Kind == CssCalcValueKind.Angle
            ? parsed.Value * 180f / (float)Math.PI
            : parsed.Value;
        return IsFinite(degrees);
    }

    private static bool TryParseCssPercentage01(string value, out float normalized)
    {
        normalized = default;
        value = value.Trim();
        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        value = value.Substring(0, value.Length - 1).Trim();
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !IsFinite(parsed))
        {
            return false;
        }

        normalized = Clamp01(parsed / 100f);
        return true;
    }

    private static SKColor CreateColorFromHsl(float hueDegrees, float saturation, float lightness, byte alpha)
    {
        var hue = hueDegrees % 360f;
        if (hue < 0f)
        {
            hue += 360f;
        }

        var h = hue / 360f;
        var s = Clamp01(saturation);
        var l = Clamp01(lightness);
        if (Math.Abs(s) <= float.Epsilon)
        {
            var gray = ToByte(l * 255f);
            return new SKColor(gray, gray, gray, alpha);
        }

        var q = l < 0.5f ? l * (1f + s) : l + s - (l * s);
        var p = (2f * l) - q;
        var r = HueToRgb(p, q, h + (1f / 3f));
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - (1f / 3f));

        return new SKColor(ToByte(r * 255f), ToByte(g * 255f), ToByte(b * 255f), alpha);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + ((q - p) * 6f * t);
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + ((q - p) * ((2f / 3f) - t) * 6f);
        }

        return p;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Round(Clamp(value, 0f, 255f));
    }

    private static float Clamp01(float value)
        => Clamp(value, 0f, 1f);

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static SKColor GetCurrentColor(SvgVisualElement? visualElement)
    {
        if (visualElement is { })
        {
            var colorServer = SvgDeferredPaintServer.TryGet<SvgColourServer>(visualElement.Color, visualElement);
            if (colorServer is { })
            {
                var color = colorServer.Colour;
                return new SKColor(color.R, color.G, color.B, color.A);
            }
        }

        return new SKColor(0, 0, 0, 255);
    }

    private static bool TryParseCssHexColorWithAlpha(string value, out SKColor color)
    {
        color = default;
        if (value.Length != 5 && value.Length != 9)
        {
            return false;
        }

        if (value[0] != '#')
        {
            return false;
        }

        if (value.Length == 5)
        {
            var red = FromHex(value[1]);
            var green = FromHex(value[2]);
            var blue = FromHex(value[3]);
            var alpha = FromHex(value[4]);
            if (red < 0 || green < 0 || blue < 0 || alpha < 0)
            {
                return false;
            }

            color = new SKColor(ExpandHex(red), ExpandHex(green), ExpandHex(blue), ExpandHex(alpha));
            return true;
        }

        if (!TryParseHexByte(value, 1, out var r) ||
            !TryParseHexByte(value, 3, out var g) ||
            !TryParseHexByte(value, 5, out var b) ||
            !TryParseHexByte(value, 7, out var a))
        {
            return false;
        }

        color = new SKColor(r, g, b, a);
        return true;
    }

    private static byte ExpandHex(int value)
        => (byte)((value << 4) | value);

    private static bool TryParseHexByte(string value, int index, out byte result)
    {
        var high = FromHex(value[index]);
        var low = FromHex(value[index + 1]);
        if (high < 0 || low < 0)
        {
            result = 0;
            return false;
        }

        result = (byte)((high << 4) | low);
        return true;
    }

    private static int FromHex(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }

        if (ch >= 'a' && ch <= 'f')
        {
            return ch - 'a' + 10;
        }

        if (ch >= 'A' && ch <= 'F')
        {
            return ch - 'A' + 10;
        }

        return -1;
    }

    private static List<string> SplitCssFilterArgs(string args)
    {
        var tokens = new List<string>();
        var start = -1;
        var depth = 0;

        for (var i = 0; i < args.Length; i++)
        {
            var ch = args[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }

            if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (start >= 0)
                {
                    tokens.Add(args.Substring(start, i - start));
                    start = -1;
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(args.Substring(start));
        }

        return tokens;
    }

    private static void SkipCssWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static bool IsOnlyCssWhitespace(string value, int index)
    {
        while (index < value.Length)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static float[] CreateIdentityColorMatrix()
        =>
        [
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ];

    private static float[] CreateBrightnessMatrix(float amount)
        =>
        [
            amount, 0f, 0f, 0f, 0f,
            0f, amount, 0f, 0f, 0f,
            0f, 0f, amount, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ];

    private static float[] CreateContrastMatrix(float amount)
    {
        var intercept = 0.5f * (1f - amount);
        return
        [
            amount, 0f, 0f, 0f, intercept,
            0f, amount, 0f, 0f, intercept,
            0f, 0f, amount, 0f, intercept,
            0f, 0f, 0f, 1f, 0f
        ];
    }

    private static float[] CreateGrayscaleMatrix(float amount)
        => InterpolateColorMatrices(CreateIdentityColorMatrix(), CreateSaturateMatrix(0f), amount);

    private static float[] CreateHueRotateMatrix(float radians)
    {
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);
        return
        [
            0.213f + (0.787f * cos) - (0.213f * sin),
            0.715f - (0.715f * cos) - (0.715f * sin),
            0.072f - (0.072f * cos) + (0.928f * sin),
            0f,
            0f,
            0.213f - (0.213f * cos) + (0.143f * sin),
            0.715f + (0.285f * cos) + (0.140f * sin),
            0.072f - (0.072f * cos) - (0.283f * sin),
            0f,
            0f,
            0.213f - (0.213f * cos) - (0.787f * sin),
            0.715f - (0.715f * cos) + (0.715f * sin),
            0.072f + (0.928f * cos) + (0.072f * sin),
            0f,
            0f,
            0f, 0f, 0f, 1f, 0f
        ];
    }

    private static float[] CreateInvertMatrix(float amount)
    {
        var slope = 1f - (2f * amount);
        var intercept = amount;
        return
        [
            slope, 0f, 0f, 0f, intercept,
            0f, slope, 0f, 0f, intercept,
            0f, 0f, slope, 0f, intercept,
            0f, 0f, 0f, 1f, 0f
        ];
    }

    private static float[] CreateOpacityMatrix(float amount)
        =>
        [
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, amount, 0f
        ];

    private static float[] CreateSaturateMatrix(float amount)
        =>
        [
            0.213f + (0.787f * amount), 0.715f - (0.715f * amount), 0.072f - (0.072f * amount), 0f, 0f,
            0.213f - (0.213f * amount), 0.715f + (0.285f * amount), 0.072f - (0.072f * amount), 0f, 0f,
            0.213f - (0.213f * amount), 0.715f - (0.715f * amount), 0.072f + (0.928f * amount), 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ];

    private static float[] CreateSepiaMatrix(float amount)
    {
        var sepia = new float[]
        {
            0.393f, 0.769f, 0.189f, 0f, 0f,
            0.349f, 0.686f, 0.168f, 0f, 0f,
            0.272f, 0.534f, 0.131f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };
        return InterpolateColorMatrices(CreateIdentityColorMatrix(), sepia, amount);
    }

    private static float[] InterpolateColorMatrices(float[] from, float[] to, float amount)
    {
        var matrix = new float[20];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = from[i] + ((to[i] - from[i]) * amount);
        }

        return matrix;
    }

    private static SKRect Inflate(SKRect rect, float dx, float dy)
        => new(rect.Left - dx, rect.Top - dy, rect.Right + dx, rect.Bottom + dy);

    private static SKRect Offset(SKRect rect, float dx, float dy)
        => new(rect.Left + dx, rect.Top + dy, rect.Right + dx, rect.Bottom + dy);

    private delegate bool CssCalcTokenParser(string token, out CssCalcValue value);

    private enum CssCalcValueKind
    {
        Number,
        Length,
        Angle
    }

    private readonly struct CssCalcValue
    {
        public CssCalcValue(float value, CssCalcValueKind kind)
        {
            Value = value;
            Kind = kind;
        }

        public float Value { get; }

        public CssCalcValueKind Kind { get; }
    }

    private sealed class CssCalcParser
    {
        private readonly string _value;
        private readonly CssCalcTokenParser _tokenParser;
        private int _index;

        public CssCalcParser(string value, CssCalcTokenParser tokenParser)
        {
            _value = value;
            _tokenParser = tokenParser;
        }

        public bool TryParse(out CssCalcValue result)
        {
            result = default;
            if (!TryParseExpression(out result))
            {
                return false;
            }

            SkipWhitespace();
            return _index == _value.Length && IsFinite(result.Value);
        }

        private bool TryParseExpression(out CssCalcValue result)
        {
            if (!TryParseTerm(out result))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] is not ('+' or '-'))
                {
                    return true;
                }

                var op = _value[_index++];
                if (!TryParseTerm(out var right))
                {
                    return false;
                }

                if (!TryAdd(result, right, op, out result))
                {
                    return false;
                }
            }
        }

        private bool TryParseTerm(out CssCalcValue result)
        {
            if (!TryParsePrimary(out result))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] is not ('*' or '/'))
                {
                    return true;
                }

                var op = _value[_index++];
                if (!TryParsePrimary(out var right))
                {
                    return false;
                }

                if (!TryMultiply(result, right, op, out result))
                {
                    return false;
                }
            }
        }

        private bool TryParsePrimary(out CssCalcValue result)
        {
            result = default;
            SkipWhitespace();
            if (_index >= _value.Length)
            {
                return false;
            }

            if (_value[_index] == '+')
            {
                _index++;
                return TryParsePrimary(out result);
            }

            if (_value[_index] == '-')
            {
                _index++;
                if (!TryParsePrimary(out result))
                {
                    return false;
                }

                result = new CssCalcValue(-result.Value, result.Kind);
                return true;
            }

            if (_value[_index] == '(')
            {
                _index++;
                if (!TryParseExpression(out result))
                {
                    return false;
                }

                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] != ')')
                {
                    return false;
                }

                _index++;
                return true;
            }

            if (StartsWithFunction("calc"))
            {
                if (!TryConsumeFunctionStart("calc") ||
                    !TryParseExpression(out result))
                {
                    return false;
                }

                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] != ')')
                {
                    return false;
                }

                _index++;
                return true;
            }

            if (StartsWithFunction("min"))
            {
                return TryParseExtremumFunction("min", static (left, right) => Math.Min(left, right), out result);
            }

            if (StartsWithFunction("max"))
            {
                return TryParseExtremumFunction("max", static (left, right) => Math.Max(left, right), out result);
            }

            if (StartsWithFunction("clamp"))
            {
                return TryParseClampFunction(out result);
            }

            var tokenStart = _index;
            while (_index < _value.Length)
            {
                if ((_value[_index] == '+' || _value[_index] == '-') &&
                    _index > tokenStart &&
                    (_value[_index - 1] == 'e' || _value[_index - 1] == 'E'))
                {
                    _index++;
                    continue;
                }

                if (_value[_index] == '+' || _value[_index] == '-')
                {
                    break;
                }

                if (_value[_index] is '*' or '/' or '(' or ')' or ',')
                {
                    break;
                }

                _index++;
            }

            var token = _value.Substring(tokenStart, _index - tokenStart).Trim();
            return token.Length > 0 && _tokenParser(token, out result);
        }

        private bool TryParseExtremumFunction(string name, Func<float, float, float> selector, out CssCalcValue result)
        {
            result = default;
            if (!TryParseFunctionArguments(name, out var arguments) || arguments.Count == 0)
            {
                return false;
            }

            result = arguments[0];
            for (var i = 1; i < arguments.Count; i++)
            {
                var next = arguments[i];
                if (!TryUnifyAdditiveKinds(result, next, out var kind))
                {
                    result = default;
                    return false;
                }

                result = new CssCalcValue(selector(result.Value, next.Value), kind);
                if (!IsFinite(result.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryParseClampFunction(out CssCalcValue result)
        {
            result = default;
            if (!TryParseFunctionArguments("clamp", out var arguments) || arguments.Count != 3)
            {
                return false;
            }

            if (!TryUnifyAdditiveKinds(arguments[0], arguments[1], out var minValueKind) ||
                !TryUnifyAdditiveKinds(new CssCalcValue(arguments[1].Value, minValueKind), arguments[2], out var kind))
            {
                return false;
            }

            var value = Math.Max(arguments[0].Value, Math.Min(arguments[1].Value, arguments[2].Value));
            result = new CssCalcValue(value, kind);
            return IsFinite(result.Value);
        }

        private bool TryParseFunctionArguments(string name, out List<CssCalcValue> arguments)
        {
            arguments = new List<CssCalcValue>();
            if (!TryConsumeFunctionStart(name))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] == ')')
                {
                    return false;
                }

                if (!TryParseExpression(out var argument))
                {
                    return false;
                }

                arguments.Add(argument);
                SkipWhitespace();
                if (_index < _value.Length && _value[_index] == ',')
                {
                    _index++;
                    continue;
                }

                if (_index < _value.Length && _value[_index] == ')')
                {
                    _index++;
                    return true;
                }

                return false;
            }
        }

        private bool TryConsumeFunctionStart(string name)
        {
            SkipWhitespace();
            if (!StartsWithFunction(name))
            {
                return false;
            }

            _index += name.Length + 1;
            return true;
        }

        private bool StartsWithFunction(string name)
        {
            return _value.Length >= _index + name.Length + 1 &&
                   _value.AsSpan(_index, name.Length).Equals(name.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                   _value[_index + name.Length] == '(';
        }

        private void SkipWhitespace()
        {
            while (_index < _value.Length && char.IsWhiteSpace(_value[_index]))
            {
                _index++;
            }
        }

        private static bool TryAdd(CssCalcValue left, CssCalcValue right, char op, out CssCalcValue result)
        {
            if (!TryUnifyAdditiveKinds(left, right, out var kind))
            {
                result = default;
                return false;
            }

            result = new CssCalcValue(op == '+' ? left.Value + right.Value : left.Value - right.Value, kind);
            return IsFinite(result.Value);
        }

        private static bool TryMultiply(CssCalcValue left, CssCalcValue right, char op, out CssCalcValue result)
        {
            result = default;
            if (op == '/')
            {
                if (right.Kind != CssCalcValueKind.Number || Math.Abs(right.Value) <= float.Epsilon)
                {
                    return false;
                }

                result = new CssCalcValue(left.Value / right.Value, left.Kind);
                return IsFinite(result.Value);
            }

            if (left.Kind == CssCalcValueKind.Number)
            {
                result = new CssCalcValue(left.Value * right.Value, right.Kind);
                return IsFinite(result.Value);
            }

            if (right.Kind == CssCalcValueKind.Number)
            {
                result = new CssCalcValue(left.Value * right.Value, left.Kind);
                return IsFinite(result.Value);
            }

            return false;
        }

        private static bool TryUnifyAdditiveKinds(CssCalcValue left, CssCalcValue right, out CssCalcValueKind kind)
        {
            if (left.Kind == right.Kind)
            {
                kind = left.Kind;
                return true;
            }

            if (left.Kind == CssCalcValueKind.Number && Math.Abs(left.Value) <= float.Epsilon)
            {
                kind = right.Kind;
                return true;
            }

            if (right.Kind == CssCalcValueKind.Number && Math.Abs(right.Value) <= float.Epsilon)
            {
                kind = left.Kind;
                return true;
            }

            kind = default;
            return false;
        }
    }

    private readonly struct CssFilterFunction
    {
        public CssFilterFunction(string name, float[] values, SKColor color)
        {
            Name = name;
            Values = values;
            Color = color;
        }

        public string Name { get; }

        public float[] Values { get; }

        public SKColor Color { get; }
    }

    private readonly struct CssFilterStep
    {
        public CssFilterStep(Uri uri)
        {
            Uri = uri;
            Function = null;
        }

        public CssFilterStep(CssFilterFunction function)
        {
            Uri = null;
            Function = function;
        }

        public Uri? Uri { get; }

        public CssFilterFunction? Function { get; }
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

    private static SvgSceneFilterResult CreateSourceAlphaFilterResult(SKImageFilter sourceGraphic, SKRect cullRect)
    {
        var matrix = new float[20]
        {
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };

        var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
        var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, sourceGraphic, cullRect);
        return new SvgSceneFilterResult(SourceAlpha, skImageFilter, SvgColourInterpolation.SRGB);
    }

    private SKImageFilter? GetPaint(SKPaint skPaint, SKRect cropRect)
    {
        var skImageFilter = SKImageFilter.CreatePaint(skPaint, cropRect);
        return skImageFilter;
    }

    private SKImageFilter GetTransparentBlackImage()
        => GetTransparentBlackImage(_skFilterRegion);

    private static SKImageFilter GetTransparentBlackImage(SKRect region)
    {
        var recorder = new SKPictureRecorder();
        recorder.BeginRecording(region);
        var picture = recorder.EndRecording();
        return SKImageFilter.CreatePicture(picture, region);
    }

    private SKImageFilter GetTransparentBlackAlpha()
        => GetTransparentBlackAlpha(_skFilterRegion);

    private static SKImageFilter GetTransparentBlackAlpha(SKRect region)
    {
        var recorder = new SKPictureRecorder();
        recorder.BeginRecording(region);
        var picture = recorder.EndRecording();
        var skImageFilterGraphic = SKImageFilter.CreatePicture(picture, region);

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

    private SvgSceneFilterResult? GetInputFilter(string? inputKey, SvgColourInterpolation dstColorSpace, SKRect cullRect, bool isFirst)
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
                    _results[SourceGraphic] = new SvgSceneFilterResult(SourceGraphic, skImageFilter, srcColorSpace);
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
                            _results[SourceGraphic] = new SvgSceneFilterResult(SourceGraphic, skImageFilter, srcColorSpace);
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
                            _results[SourceAlpha] = new SvgSceneFilterResult(SourceAlpha, skImageFilter, srcColorSpace);
                            return _results[SourceAlpha];
                        }
                    }

                    break;
                }
            case BackgroundImage:
                {
                    if (!IsFilterBackgroundInputEnabled(_assetLoader))
                    {
                        var skImageFilter = GetTransparentBlackImage(cullRect);
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundImage] = new SvgSceneFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                        return _results[BackgroundImage];
                    }

                    var skPicture = _filterSource.BackgroundImage(cullRect);
                    if (skPicture is { })
                    {
                        var skImageFilter = GetGraphic(skPicture, cullRect);
                        if (skImageFilter is { })
                        {
                            var srcColorSpace = SvgColourInterpolation.SRGB;
                            _results[BackgroundImage] = new SvgSceneFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                            return _results[BackgroundImage];
                        }
                    }
                    else
                    {
                        var skImageFilter = GetTransparentBlackImage(cullRect);
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundImage] = new SvgSceneFilterResult(BackgroundImage, skImageFilter, srcColorSpace);
                        return _results[BackgroundImage];
                    }

                    break;
                }
            case BackgroundAlpha:
                {
                    if (!IsFilterBackgroundInputEnabled(_assetLoader))
                    {
                        var skImageFilter = GetTransparentBlackAlpha(cullRect);
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundAlpha] = new SvgSceneFilterResult(BackgroundAlpha, skImageFilter, srcColorSpace);
                        return _results[BackgroundAlpha];
                    }

                    var skPicture = _filterSource.BackgroundImage(cullRect);
                    if (skPicture is { })
                    {
                        var skImageFilter = GetAlpha(skPicture, cullRect);
                        if (skImageFilter is { })
                        {
                            var srcColorSpace = SvgColourInterpolation.SRGB;
                            _results[BackgroundAlpha] = new SvgSceneFilterResult(BackgroundAlpha, skImageFilter, srcColorSpace);
                            return _results[BackgroundAlpha];
                        }
                    }
                    else
                    {
                        var skImageFilter = GetTransparentBlackAlpha(cullRect);
                        var srcColorSpace = SvgColourInterpolation.SRGB;
                        _results[BackgroundAlpha] = new SvgSceneFilterResult(BackgroundAlpha, skImageFilter, srcColorSpace);
                        return _results[BackgroundAlpha];
                    }

                    break;
                }
            case FillPaint:
                {
                    var skPicture = _filterSource.FillPaint(cullRect);
                    if (skPicture is { })
                    {
                        var paintGraphic = GetGraphic(skPicture, cullRect);
                        var sourceAlpha = GetInputFilter(SourceAlpha, SvgColourInterpolation.SRGB, cullRect, false);
                        var skImageFilter = paintGraphic;
                        if (paintGraphic is { } && sourceAlpha?.Filter is { } sourceAlphaFilter)
                        {
                            skImageFilter = SKImageFilter.CreateBlendMode(SKBlendMode.SrcOver, sourceAlphaFilter, paintGraphic, cullRect);
                        }
                        if (skImageFilter is { })
                        {
                            var srcColorSpace = SvgColourInterpolation.SRGB;
                            _results[FillPaint] = new SvgSceneFilterResult(FillPaint, skImageFilter, srcColorSpace);
                            return _results[FillPaint];
                        }
                    }

                    break;
                }
            case StrokePaint:
                {
                    var skPicture = _filterSource.StrokePaint(cullRect);
                    if (skPicture is { })
                    {
                        var paintGraphic = GetGraphic(skPicture, cullRect);
                        var sourceAlpha = GetInputFilter(SourceAlpha, SvgColourInterpolation.SRGB, cullRect, false);
                        var skImageFilter = paintGraphic;
                        if (paintGraphic is { } && sourceAlpha?.Filter is { } sourceAlphaFilter)
                        {
                            skImageFilter = SKImageFilter.CreateBlendMode(SKBlendMode.SrcOver, sourceAlphaFilter, paintGraphic, cullRect);
                        }
                        if (skImageFilter is { })
                        {
                            var srcColorSpace = SvgColourInterpolation.SRGB;
                            _results[StrokePaint] = new SvgSceneFilterResult(StrokePaint, skImageFilter, srcColorSpace);
                            return _results[StrokePaint];
                        }
                    }

                    break;
                }
        }

        return default;
    }

    private static bool IsExplicitUnresolvedInput(string? inputKey, SvgSceneFilterResult? inputFilterResult)
    {
        return !string.IsNullOrWhiteSpace(inputKey) && inputFilterResult is null;
    }

    private SvgSceneFilterResult? GetFilterResult(SvgFilterPrimitive svgFilterPrimitive, SKImageFilter? skImageFilter, SvgColourInterpolation colorSpace)
    {
        if (skImageFilter is { })
        {
            var key = svgFilterPrimitive.Result;
            var result = new SvgSceneFilterResult(key, skImageFilter, colorSpace);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _results[key] = result;
            }
            return result;
        }
        return default;
    }

    private List<SvgFilter>? GetLinkedFilter(SvgVisualElement svgVisualElement, HashSet<Uri> uris)
        => GetLinkedFilter(svgVisualElement, svgVisualElement.Filter, uris);

    private List<SvgFilter>? GetLinkedFilter(SvgElement owner, Uri? filterUri, HashSet<Uri> uris)
    {
        var currentFilter = ResolveFilterReference(owner, filterUri);
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
                if (SvgService.HasRecursiveReference(currentFilter, static e => SvgService.GetEffectiveReferenceUri(e, e.Href), uris))
                {
                    return svgFilters;
                }

                var currentFilterHref = SvgService.GetEffectiveReferenceUri(currentFilter, currentFilter.Href);
                currentFilter = ResolveFilterReference(currentFilter, currentFilterHref);
            }
        } while (currentFilter is { });

        return svgFilters;
    }

    internal static SvgFilter? ResolveFilterReference(SvgElement owner, Uri? uri)
    {
        return SvgService.GetReference<SvgFilter>(owner, NormalizeFilterReferenceUri(uri));
    }

    private static Uri? NormalizeFilterReferenceUri(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        var text = uri.OriginalString.Trim();
        if (text.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            text.EndsWith(")", StringComparison.Ordinal))
        {
            text = text.Substring(4, text.Length - 5).Trim();
            if (text.Length >= 2 &&
                ((text[0] == '"' && text[text.Length - 1] == '"') ||
                 (text[0] == '\'' && text[text.Length - 1] == '\'')))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }
        }

        return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var normalizedUri)
            ? normalizedUri
            : uri;
    }

    private SKImageFilter? ApplyColourInterpolation(
        SvgSceneFilterResult? input,
        SvgColourInterpolation dst,
        bool allowImplicitSourceGraphic = false)
    {
        if (input is null)
        {
            return null;
        }

        var src = input.ColorSpace;
        var useImplicitSourceGraphic = allowImplicitSourceGraphic &&
                                       string.Equals(input.Key, SourceGraphic, StringComparison.Ordinal);

        if (src == dst)
        {
            return useImplicitSourceGraphic ? null : input.Filter;
        }

        if (src == SvgColourInterpolation.SRGB && dst == SvgColourInterpolation.LinearRGB)
        {
            return SKImageFilter.CreateColorFilter(
                FilterEffectsService.SRGBToLinearGamma(),
                useImplicitSourceGraphic ? null : input.Filter);
        }

        if (src == SvgColourInterpolation.LinearRGB && dst == SvgColourInterpolation.SRGB)
        {
            return SKImageFilter.CreateColorFilter(
                FilterEffectsService.LinearToSRGBGamma(),
                useImplicitSourceGraphic ? null : input.Filter);
        }

        return null;
    }

    private SKImageFilter? ApplyColourInterpolationAndClip(
        SvgSceneFilterResult? input,
        SvgColourInterpolation dst,
        SKRect clip,
        bool allowImplicitSourceGraphic = false)
    {
        if (input is null)
        {
            return null;
        }

        var inputRegion = GetFilterResultRegion(input);
        var shouldClip = IsUsableRegion(clip) && !AreSameRegion(inputRegion, clip);
        var filter = ApplyColourInterpolation(input, dst, allowImplicitSourceGraphic && !shouldClip);
        if (filter is null)
        {
            if (!shouldClip ||
                !allowImplicitSourceGraphic ||
                !string.Equals(input.Key, SourceGraphic, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return shouldClip ? CreateIdentityCropFilter(filter, clip) : filter;
    }

    private SKImageFilter? ApplyDisplacementMapInterpolationAndClip(
        SvgSceneFilterResult? input,
        SvgColourInterpolation dst,
        SKRect clip)
    {
        if (input is { } &&
            dst == SvgColourInterpolation.LinearRGB &&
            input.ColorSpace == SvgColourInterpolation.SRGB &&
            _linearPngGammaImageFilters?.Contains(input.Filter) == true)
        {
            return ClipFilterInput(input, clip);
        }

        return ApplyColourInterpolationAndClip(input, dst, clip);
    }

    private SKImageFilter? ClipFilterInput(SvgSceneFilterResult? input, SKRect clip)
    {
        if (input is null)
        {
            return null;
        }

        var inputRegion = GetFilterResultRegion(input);
        return IsUsableRegion(clip) && !AreSameRegion(inputRegion, clip)
            ? CreateIdentityCropFilter(input.Filter, clip)
            : input.Filter;
    }

    private static SKImageFilter CreateIdentityCropFilter(SKImageFilter? input, SKRect clip)
    {
        return SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(CreateIdentityColorMatrix()), input, clip);
    }

    private static SKRect NormalizeFilterPrimitiveRegion(SKRect region)
    {
        return IsUsableRegion(region) ? region : SKRect.Empty;
    }

    private static bool AreSameRegion(SKRect left, SKRect right)
    {
        const float tolerance = 0.001f;
        return Math.Abs(left.Left - right.Left) <= tolerance &&
               Math.Abs(left.Top - right.Top) <= tolerance &&
               Math.Abs(left.Right - right.Right) <= tolerance &&
               Math.Abs(left.Bottom - right.Bottom) <= tolerance;
    }

    private static bool IsStandardInput(SvgSceneFilterResult? filterResult)
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

    private static bool IsFilterBackgroundInputEnabled(ISvgAssetLoader assetLoader)
    {
        return assetLoader is not ISvgFilterBackgroundInputOptions { EnableFilterBackgroundInputs: false };
    }

    private float CalculateHorizontal(SvgElement svgElement, SvgUnit unit)
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
        return new float[]
        {
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        };
    }

    private SKImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        SKColorFilter skColorFilter;

        switch (svgColourMatrix.Type)
        {
            case SvgColourMatrixType.HueRotate:
                {
                    var value = TryParseFiniteSingle(svgColourMatrix.Values, 0f);
                    var hue = (float)SvgService.DegreeToRadian(value);
                    var cosHue = Math.Cos(hue);
                    var sinHue = Math.Sin(hue);
                    float[] matrix = {
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
                    };
                    skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                }
                break;

            case SvgColourMatrixType.LuminanceToAlpha:
                {
                    float[] matrix = {
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0,
                        0.2125f, 0.7154f, 0.0721f, 0, 0
                    };
                    skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                }
                break;

            case SvgColourMatrixType.Saturate:
                {
                    var value = TryParseFiniteSingle(svgColourMatrix.Values, 1f);
                    float[] matrix = {
                        (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                        (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                        (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                        0, 0, 0, 1, 0
                    };
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
                                if (!TryParseFiniteSingle(parts[i], out matrix[i]))
                                {
                                    matrix = CreateIdentityColorMatrixArray();
                                    break;
                                }
                            }
                            if (matrix.Length == 20)
                            {
                                matrix[4] *= 255f;
                                matrix[9] *= 255f;
                                matrix[14] *= 255f;
                                matrix[19] *= 255f;
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

    private static float TryParseFiniteSingle(string? value, float fallback)
        => !string.IsNullOrWhiteSpace(value) &&
           TryParseFiniteSingle(value!, out var parsed)
            ? parsed
            : fallback;

    private static bool TryParseFiniteSingle(string value, out float parsed)
    {
        return float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) &&
               IsFinite(parsed);
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
        TransformsService.GetOptionalNumbers(svgConvolveMatrix.Order, 3f, 3f, out var orderX, out var orderY);

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
        var targetX = svgConvolveMatrix.TargetX;
        var targetY = svgConvolveMatrix.TargetY;
        if (!svgConvolveMatrix.ContainsAttribute("targetX"))
        {
            targetX = (int)Math.Floor(orderX / 2f);
        }
        if (!svgConvolveMatrix.ContainsAttribute("targetY"))
        {
            targetY = (int)Math.Floor(orderY / 2f);
        }
        targetX = Math.Max(0, Math.Min(targetX, (int)orderX - 1));
        targetY = Math.Max(0, Math.Min(targetY, (int)orderY - 1));
        var kernelOffset = new SKPointI(targetX, targetY);
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
        var azimuthRad = SvgService.DegreeToRadian(azimuth);
        var elevationRad = SvgService.DegreeToRadian(elevation);
        var x = (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad));
        var y = (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad));
        var z = (float)Math.Sin(elevationRad);
        return new SKPoint3(x, y, z);
    }

    private SKPoint3 GetPoint3(float x, float y, float z)
    {
        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            x = _skBounds.Left + x * _skBounds.Width;
            y = _skBounds.Top + y * _skBounds.Height;
            z *= TransformsService.CalculateOtherPercentageValue(_skBounds);
        }
        return new SKPoint3(x, y, z);
    }

    private SKImageFilter? CreateDiffuseLighting(SvgDiffuseLighting svgDiffuseLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var lightColor = GetFilterPrimitiveColor(svgDiffuseLighting, svgDiffuseLighting.LightingColor);
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
                    if (specularExponentSpotLight < 1f)
                    {
                        specularExponentSpotLight = 1f;
                    }
                    else if (specularExponentSpotLight > 128f)
                    {
                        specularExponentSpotLight = 128f;
                    }
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
            scale *= TransformsService.CalculateOtherPercentageValue(_skBounds);
        }

        if (!IsFinite(scale))
        {
            return default;
        }

        return SKImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, input, cropRect);
    }

    private static SKColor ConvertToFilterColorSpace(SKColor color, SvgColourInterpolation colorSpace)
    {
        if (colorSpace != SvgColourInterpolation.LinearRGB)
        {
            return color;
        }

        return new SKColor(
            ToByte(FilterEffectsService.SRGBToLinear(color.Red / 255f)),
            ToByte(FilterEffectsService.SRGBToLinear(color.Green / 255f)),
            ToByte(FilterEffectsService.SRGBToLinear(color.Blue / 255f)),
            color.Alpha);

        static byte ToByte(float value)
        {
            return (byte)Math.Round(Math.Min(Math.Max(value, 0f), 1f) * 255f);
        }
    }

    private static SKColor? GetFilterPrimitiveColor(SvgElement svgElement, SvgPaintServer server)
    {
        if (server is SvgDeferredPaintServer svgDeferredPaintServer)
        {
            server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgElement);
        }

        if (server is SvgColourServer svgColourServer)
        {
            return PaintingService.GetColor(svgColourServer, 1f, DrawAttributes.None);
        }

        return new SKColor(0x00, 0x00, 0x00, 0xFF);
    }

    private SKImageFilter? CreateDropShadow(SvgDropShadow svgDropShadow, SvgColourInterpolation colorInterpolationFilters, SKImageFilter? input = default, SKImageFilter? mergeInput = default, SKRect? cropRect = default)
    {
        TransformsService.GetOptionalNumbers(svgDropShadow.StdDeviation, 2f, 2f, out var sigmaX, out var sigmaY);
        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = TransformsService.CalculateOtherPercentageValue(_skBounds);
            sigmaX *= value;
            sigmaY *= value;
        }

        if (!IsFinite(sigmaX) || !IsFinite(sigmaY) || sigmaX < 0f || sigmaY < 0f)
        {
            return default;
        }

        var floodColor = GetFilterPrimitiveColor(svgDropShadow, svgDropShadow.FloodColor);
        if (floodColor is null)
        {
            return default;
        }

        var floodAlpha = PaintingService.CombineWithOpacity(floodColor.Value.Alpha, svgDropShadow.FloodOpacity);
        var filterFloodColor = ConvertToFilterColorSpace(floodColor.Value, colorInterpolationFilters);
        var dx = CalculateHorizontal(svgDropShadow, svgDropShadow.Dx);
        var dy = CalculateVertical(svgDropShadow, svgDropShadow.Dy);
        if (!IsFinite(dx) || !IsFinite(dy))
        {
            return default;
        }

        var alphaMatrix = new float[]
        {
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        };
        var alpha = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(alphaMatrix), input);
        var blurredAlpha = SKImageFilter.CreateBlur(sigmaX, sigmaY, alpha, cropRect);
        var offsetAlpha = SKImageFilter.CreateOffset(dx, dy, blurredAlpha, cropRect);

        var colorizeMatrix = new float[]
        {
            0f, 0f, 0f, filterFloodColor.Red / 255f, 0f,
            0f, 0f, 0f, filterFloodColor.Green / 255f, 0f,
            0f, 0f, 0f, filterFloodColor.Blue / 255f, 0f,
            0f, 0f, 0f, floodAlpha / 255f, 0f
        };
        var shadow = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(colorizeMatrix), offsetAlpha, cropRect);

        return mergeInput is null
            ? shadow
            : SKImageFilter.CreateMerge(new[] { shadow, mergeInput }, cropRect);
    }

    private SKImageFilter? CreateFlood(SvgFlood svgFlood, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var floodColor = GetFilterPrimitiveColor(svgFlood, svgFlood.FloodColor);
        if (floodColor is null)
        {
            return default;
        }

        var floodOpacity = svgFlood.FloodOpacity;
        var floodAlpha = PaintingService.CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
        floodColor = new SKColor(floodColor.Value.Red, floodColor.Value.Green, floodColor.Value.Blue, floodAlpha);

        if (cropRect is null)
        {
            cropRect = _skFilterRegion;
        }

        var floodRect = cropRect.Value;
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(floodRect);
        var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = floodColor.Value
        };
        var path = new SKPath();
        path.AddRect(floodRect);
        canvas.DrawPath(path, paint);
        var picture = recorder.EndRecording();
        return SKImageFilter.CreatePicture(picture, floodRect);
    }

    private SKImageFilter? CreateBlur(SvgGaussianBlur svgGaussianBlur, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        TransformsService.GetOptionalNumbers(svgGaussianBlur.StdDeviation, 0f, 0f, out var sigmaX, out var sigmaY);

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = TransformsService.CalculateOtherPercentageValue(_skBounds);
            sigmaX *= value;
            sigmaY *= value;
        }

        if (!IsFinite(sigmaX) || !IsFinite(sigmaY) || sigmaX < 0f || sigmaY < 0f)
        {
            return default;
        }

        return SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
    }

    private SKImageFilter? CreateImage(
        FilterEffects.SvgImage svgImage,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect skFilterPrimitiveRegion,
        out bool hasLinearPngGamma,
        SKRect? cropRect = default)
    {
        hasLinearPngGamma = false;
        var href = SvgService.GetEffectiveHrefString(svgImage, svgImage.Href);
        if (string.IsNullOrWhiteSpace(href))
        {
            return default;
        }

        var imageUri = SvgService.GetImageUri(href!, svgImage);
        if (TryCreateLocalFragmentImage(svgImage, href!, imageUri, assetLoader, skFilterPrimitiveRegion, out var localFragmentImageFilter))
        {
            return localFragmentImageFilter ?? GetTransparentBlackImage(skFilterPrimitiveRegion);
        }

        var uri = SvgService.GetImageDocumentUri(imageUri);
        if (ContainsFeImageDocumentReference(references, uri) ||
            IsActiveFeImageDocumentReference(uri))
        {
            return GetTransparentBlackImage(skFilterPrimitiveRegion);
        }

        using var activeDocumentReferences = PushActiveFeImageDocumentReferences(references, uri);
        var image = GetCachedFeImageResource(href!, imageUri, svgImage, assetLoader);
        var skImage = image as SKImage;
        var svgDocument = image as SvgDocument;
        if (skImage is null && svgDocument is null)
        {
            return GetTransparentBlackImage(skFilterPrimitiveRegion);
        }

        var srcRect = default(SKRect);

        if (skImage is { })
        {
            srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            if (srcRect.IsEmpty)
            {
                return GetTransparentBlackImage(skFilterPrimitiveRegion);
            }

            if (HasLinearPngGamma(skImage))
            {
                hasLinearPngGamma = true;
            }
        }

        var rootSubregion = SKRect.Create(0f, 0f, skFilterPrimitiveRegion.Width, skFilterPrimitiveRegion.Height);

        if (svgDocument is { })
        {
            var skSize = SvgService.GetDimensions(svgDocument, skFilterPrimitiveRegion);
            srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            if (srcRect.IsEmpty)
            {
                return GetTransparentBlackImage(skFilterPrimitiveRegion);
            }
        }

        var destRect = TransformsService.CalculateRect(svgImage.AspectRatio, srcRect, rootSubregion);

        if (skImage is { })
        {
            return CreateFeImagePictureFilter(
                skFilterPrimitiveRegion,
                canvas => canvas.DrawImage(
                    skImage,
                    srcRect,
                    destRect,
                    new SKPaint
                    {
                        IsAntialias = true,
                        FilterQuality = SKFilterQuality.High
                    }));
        }

        if (svgDocument is { })
        {
            if (!SvgSceneCompiler.TryCompile(svgDocument, srcRect, assetLoader, _sceneDocument.IgnoreAttributes, out var sceneDocument) ||
                sceneDocument is null)
            {
                return GetTransparentBlackImage(skFilterPrimitiveRegion);
            }

            var skPicture = SvgSceneRenderer.Render(sceneDocument);
            if (skPicture is null)
            {
                return GetTransparentBlackImage(skFilterPrimitiveRegion);
            }

            var contentMatrix = CreateRectMapping(srcRect, destRect);
            return CreateFeImagePictureFilter(
                skFilterPrimitiveRegion,
                canvas =>
                {
                    canvas.Save();
                    canvas.SetMatrix(contentMatrix);
                    canvas.DrawPicture(skPicture);
                    canvas.Restore();
                });
        }

        return default;
    }

    private object? GetCachedFeImageResource(string href, Uri imageUri, FilterEffects.SvgImage svgImage, ISvgAssetLoader assetLoader)
    {
        var cacheKey = CreateFeImageResourceCacheKey(imageUri);
        _feImageResourceCache ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (_feImageResourceCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var image = SvgService.GetImage(href, svgImage, assetLoader);
        _feImageResourceCache[cacheKey] = image;
        return image;
    }

    private static string CreateFeImageResourceCacheKey(Uri imageUri)
    {
        var documentUri = SvgService.GetImageDocumentUri(imageUri);
        if (!string.IsNullOrEmpty(imageUri.Fragment))
        {
            return documentUri.OriginalString + imageUri.Fragment;
        }

        return documentUri.OriginalString;
    }

    private static bool ContainsFeImageDocumentReference(HashSet<Uri>? references, Uri documentUri)
    {
        if (references is null)
        {
            return false;
        }

        var documentReferenceKey = CreateFeImageDocumentReferenceKey(documentUri);
        foreach (var reference in references)
        {
            if (string.Equals(CreateFeImageDocumentReferenceKey(reference), documentReferenceKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveFeImageDocumentReference(Uri documentUri)
    {
        return s_activeFeImageDocumentReferences?.Contains(CreateFeImageDocumentReferenceKey(documentUri)) == true;
    }

    private static ActiveFeImageDocumentReferenceScope PushActiveFeImageDocumentReferences(HashSet<Uri>? references, Uri documentUri)
    {
        s_activeFeImageDocumentReferences ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeReferences = s_activeFeImageDocumentReferences;
        var added = new List<string>();

        if (references is { })
        {
            foreach (var reference in references)
            {
                AddReference(reference);
            }
        }

        AddReference(documentUri);
        return new ActiveFeImageDocumentReferenceScope(added);

        void AddReference(Uri reference)
        {
            var key = CreateFeImageDocumentReferenceKey(reference);
            if (activeReferences.Add(key))
            {
                added.Add(key);
            }
        }
    }

    private static string CreateFeImageDocumentReferenceKey(Uri uri)
    {
        var documentUri = SvgService.GetImageDocumentUri(uri);
        return documentUri.IsAbsoluteUri
            ? documentUri.AbsoluteUri
            : documentUri.OriginalString;
    }

    private readonly struct ActiveFeImageDocumentReferenceScope : IDisposable
    {
        private readonly List<string>? _added;

        public ActiveFeImageDocumentReferenceScope(List<string> added)
        {
            _added = added;
        }

        public void Dispose()
        {
            if (_added is null)
            {
                return;
            }

            for (var i = 0; i < _added.Count; i++)
            {
                s_activeFeImageDocumentReferences?.Remove(_added[i]);
            }
        }
    }

    private static bool HasLinearPngGamma(SKImage image)
    {
        var data = image.Data;
        if (data is null ||
            data.Length < 33 ||
            data[0] != 0x89 ||
            data[1] != (byte)'P' ||
            data[2] != (byte)'N' ||
            data[3] != (byte)'G')
        {
            return false;
        }

        var offset = 8;
        while (offset + 12 <= data.Length)
        {
            var length = ReadBigEndianInt32(data, offset);
            if (length < 0 || offset + 12 + length > data.Length)
            {
                return false;
            }

            var typeOffset = offset + 4;
            if (data[typeOffset] == (byte)'g' &&
                data[typeOffset + 1] == (byte)'A' &&
                data[typeOffset + 2] == (byte)'M' &&
                data[typeOffset + 3] == (byte)'A')
            {
                return length == 4 &&
                       ReadBigEndianInt32(data, offset + 8) == 100000;
            }

            offset += 12 + length;
        }

        return false;
    }

    private static int ReadBigEndianInt32(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               data[offset + 3];
    }

    private bool TryCreateLocalFragmentImage(FilterEffects.SvgImage svgImage, string href, Uri imageUri, ISvgAssetLoader assetLoader, SKRect skFilterPrimitiveRegion, out SKImageFilter? imageFilter)
    {
        imageFilter = default;

        if (string.IsNullOrWhiteSpace(href) || !href.TrimStart().StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        using var activeReference = PushActiveLocalFeImageReference(svgImage, href);
        if (!activeReference.IsEntered)
        {
            return true;
        }

        var measureViewport = _skViewport.IsEmpty ? skFilterPrimitiveRegion : _skViewport;
        if (!TryCreateLocalFragmentDocument(svgImage, href, imageUri, measureViewport, out var fragmentDocument, out var measurementElementId, out var translateToPrimitiveRegion) ||
            fragmentDocument is null)
        {
            return true;
        }

        if (measureViewport.IsEmpty ||
            !SvgSceneCompiler.TryCompile(fragmentDocument, measureViewport, assetLoader, _sceneDocument.IgnoreAttributes, out var measuredSceneDocument) ||
            measuredSceneDocument is null)
        {
            return true;
        }

        var measuredBounds = FindNodeBoundsById(measuredSceneDocument.Root, measurementElementId)
            ?? measuredSceneDocument.Root.TransformedBounds;
        if (measuredBounds.IsEmpty)
        {
            return true;
        }

        if (!SvgSceneCompiler.TryCompile(fragmentDocument, measureViewport, assetLoader, _sceneDocument.IgnoreAttributes, out var sceneDocument) ||
            sceneDocument is null)
        {
            return true;
        }

        var skPicture = SvgSceneRenderer.Render(sceneDocument);
        if (skPicture is null)
        {
            return true;
        }

        imageFilter = CreateFeImagePictureFilter(
            skFilterPrimitiveRegion,
            canvas => canvas.DrawPicture(skPicture),
            translateToPrimitiveRegion);
        return true;
    }

    private static ActiveLocalFeImageReferenceScope PushActiveLocalFeImageReference(FilterEffects.SvgImage svgImage, string href)
    {
        var key = CreateLocalFeImageReferenceKey(svgImage, href);
        s_activeLocalFeImageReferences ??= new HashSet<string>(StringComparer.Ordinal);
        return s_activeLocalFeImageReferences.Add(key)
            ? new ActiveLocalFeImageReferenceScope(key)
            : default;
    }

    private static string CreateLocalFeImageReferenceKey(FilterEffects.SvgImage svgImage, string href)
    {
        var documentKey = svgImage.OwnerDocument?.BaseUri?.OriginalString ?? string.Empty;
        var owningFilterKey = GetAncestorFilter(svgImage)?.ID ?? string.Empty;
        return string.Join("|", documentKey, owningFilterKey, href.Trim());
    }

    private readonly struct ActiveLocalFeImageReferenceScope : IDisposable
    {
        private readonly string? _key;

        public ActiveLocalFeImageReferenceScope(string key)
        {
            _key = key;
            IsEntered = true;
        }

        public bool IsEntered { get; }

        public void Dispose()
        {
            if (IsEntered && _key is { } key)
            {
                s_activeLocalFeImageReferences?.Remove(key);
            }
        }
    }

    private SKImageFilter? CreateFeImagePictureFilter(SKRect primitiveRegion, Action<SKCanvas> drawContent, bool translateToPrimitiveRegion = true)
    {
        var isAxisAlignedScaleTranslate = IsAxisAlignedScaleTranslate(_targetTransform);
        var globalFilterRegion = _targetTransform.MapRect(_skFilterRegion);
        var globalPrimitiveRegion = _targetTransform.MapRect(primitiveRegion);

        if (!IsUsableRegion(globalFilterRegion) ||
            !IsUsableRegion(globalPrimitiveRegion) ||
            !_targetTransform.TryInvert(out var inverseTargetTransform))
        {
            return default;
        }

        _usesGlobalFeImageCoordinates = true;
        _usesNonAxisFeImageCoordinates |= !isAxisAlignedScaleTranslate;
        var useGlobalLayer = !isAxisAlignedScaleTranslate &&
            _primitives.Count == 1 &&
            _primitives[0].FilterPrimitive is FilterEffects.SvgImage;
        _usesGlobalLayer |= useGlobalLayer;

        var localCullRect = useGlobalLayer
            ? globalFilterRegion
            : inverseTargetTransform.MapRect(globalFilterRegion);
        if (!IsUsableRegion(localCullRect))
        {
            localCullRect = primitiveRegion;
        }

        if (!isAxisAlignedScaleTranslate && !useGlobalLayer)
        {
            var inflation = Math.Max(localCullRect.Width, localCullRect.Height) * 16f;
            localCullRect = SKRect.Create(
                localCullRect.Left - inflation,
                localCullRect.Top - inflation,
                localCullRect.Width + (inflation * 2f),
                localCullRect.Height + (inflation * 2f));
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(localCullRect);
        if (isAxisAlignedScaleTranslate)
        {
            canvas.ClipPath(CreateInverseMappedRectClip(globalPrimitiveRegion, inverseTargetTransform), SKClipOperation.Intersect, true);
            canvas.SetMatrix(CreateFeImageRootTransform(globalPrimitiveRegion, inverseTargetTransform, translateToPrimitiveRegion));
        }
        else if (useGlobalLayer)
        {
            canvas.ClipRect(globalPrimitiveRegion, SKClipOperation.Intersect, true);
            canvas.SetMatrix(CreateFeImageGlobalRootTransform(globalPrimitiveRegion, translateToPrimitiveRegion));
        }
        else
        {
            canvas.ClipRect(globalPrimitiveRegion, SKClipOperation.Intersect, true);
            canvas.SetMatrix(CreateFeImageRootTransform(globalPrimitiveRegion, inverseTargetTransform, translateToPrimitiveRegion));
        }

        drawContent(canvas);
        var picture = recorder.EndRecording();

        return SKImageFilter.CreatePicture(picture, localCullRect);
    }

    private SKMatrix CreateFeImageRootTransform(SKRect globalPrimitiveRegion, SKMatrix inverseTargetTransform, bool translateToPrimitiveRegion)
    {
        DecomposeScale(_targetTransform, out var scaleX, out var scaleY);
        var globalRootTransform = new SKMatrix
        {
            ScaleX = scaleX,
            ScaleY = scaleY,
            TransX = translateToPrimitiveRegion ? globalPrimitiveRegion.Left : 0f,
            TransY = translateToPrimitiveRegion ? globalPrimitiveRegion.Top : 0f,
            Persp2 = 1f
        };

        return inverseTargetTransform.PreConcat(globalRootTransform);
    }

    private SKMatrix CreateFeImageGlobalRootTransform(SKRect globalPrimitiveRegion, bool translateToPrimitiveRegion)
    {
        DecomposeScale(_targetTransform, out var scaleX, out var scaleY);
        return new SKMatrix
        {
            ScaleX = scaleX,
            ScaleY = scaleY,
            TransX = translateToPrimitiveRegion ? globalPrimitiveRegion.Left : 0f,
            TransY = translateToPrimitiveRegion ? globalPrimitiveRegion.Top : 0f,
            Persp2 = 1f
        };
    }

    private static ClipPath CreateInverseMappedRectClip(SKRect rect, SKMatrix inverseTransform)
    {
        var path = new SKPath();
        var topLeft = inverseTransform.MapPoint(new SKPoint(rect.Left, rect.Top));
        var topRight = inverseTransform.MapPoint(new SKPoint(rect.Right, rect.Top));
        var bottomRight = inverseTransform.MapPoint(new SKPoint(rect.Right, rect.Bottom));
        var bottomLeft = inverseTransform.MapPoint(new SKPoint(rect.Left, rect.Bottom));

        path.MoveTo(topLeft.X, topLeft.Y);
        path.LineTo(topRight.X, topRight.Y);
        path.LineTo(bottomRight.X, bottomRight.Y);
        path.LineTo(bottomLeft.X, bottomLeft.Y);
        path.Close();

        var clipPath = new ClipPath();
        clipPath.Clips!.Add(new PathClip
        {
            Path = path,
            Transform = SKMatrix.Identity
        });

        return clipPath;
    }

    private static SKMatrix CreateRectMapping(SKRect source, SKRect destination)
    {
        var scaleX = source.Width > 0f ? destination.Width / source.Width : 1f;
        var scaleY = source.Height > 0f ? destination.Height / source.Height : 1f;

        return new SKMatrix
        {
            ScaleX = scaleX,
            ScaleY = scaleY,
            TransX = destination.Left - (source.Left * scaleX),
            TransY = destination.Top - (source.Top * scaleY),
            Persp2 = 1f
        };
    }

    private static void DecomposeScale(SKMatrix matrix, out float scaleX, out float scaleY)
    {
        scaleX = (float)Math.Sqrt((matrix.ScaleX * matrix.ScaleX) + (matrix.SkewX * matrix.SkewX));
        scaleY = (float)Math.Sqrt((matrix.SkewY * matrix.SkewY) + (matrix.ScaleY * matrix.ScaleY));

        if (scaleX <= 0f)
        {
            scaleX = 1f;
        }

        if (scaleY <= 0f)
        {
            scaleY = 1f;
        }
    }

    private bool TryCreateLocalFragmentDocument(FilterEffects.SvgImage svgImage, string href, Uri imageUri, SKRect measureViewport, out SvgDocument? fragmentDocument, out string? measurementElementId, out bool translateToPrimitiveRegion)
    {
        fragmentDocument = default;
        measurementElementId = default;
        translateToPrimitiveRegion = true;

        if (svgImage.OwnerDocument?.DeepCopy() is not SvgDocument documentClone)
        {
            return false;
        }

        var referenceId = href.Trim().TrimStart('#');

        if (documentClone.GetElementById(referenceId) is not SvgElement referencedElement)
        {
            return false;
        }

        var suppressedOwningFilter = SuppressOwningFilterReferences(referencedElement, svgImage);
        var hasDefinitionAncestor = HasDefinitionAncestor(referencedElement);
        PruneDocumentToReferencedElement(documentClone, referencedElement);
        measurementElementId = referencedElement.ID;
        translateToPrimitiveRegion = hasDefinitionAncestor || !suppressedOwningFilter;

        if (suppressedOwningFilter || hasDefinitionAncestor)
        {
            var svgUse = new SvgUse
            {
                ID = "__svgskia_feimage_measure",
                ReferencedElement = new Uri("#" + referencedElement.ID, UriKind.Relative)
            };

            if (measureViewport.Width > 0f)
            {
                svgUse.Width = measureViewport.Width;
            }

            if (measureViewport.Height > 0f)
            {
                svgUse.Height = measureViewport.Height;
            }

            documentClone.Children.Add(svgUse);
            measurementElementId = svgUse.ID;
        }

        fragmentDocument = documentClone;
        return true;
    }

    private static bool SuppressOwningFilterReferences(SvgElement referencedElement, FilterEffects.SvgImage svgImage)
    {
        var suppressed = false;
        var owningFilter = GetAncestorFilter(svgImage);
        if (owningFilter is null || string.IsNullOrWhiteSpace(owningFilter.ID))
        {
            return false;
        }

        var filterId = owningFilter.ID!;
        var stack = new Stack<SvgElement>();
        stack.Push(referencedElement);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is SvgVisualElement visualElement &&
                ReferencesFilterId(visualElement, filterId))
            {
                SuppressFilterDeclaration(visualElement);
                suppressed = true;
            }

            for (var i = 0; i < current.Children.Count; i++)
            {
                stack.Push(current.Children[i]);
            }
        }

        return suppressed;
    }

    private static void SuppressFilterDeclaration(SvgVisualElement visualElement)
    {
        visualElement.Filter = null!;
        visualElement.Attributes.Remove("filter");
        visualElement.CustomAttributes.Remove("filter");

        // Presentation attributes are also staged in the cascaded style map.
        // A higher-specificity "none" keeps cloned feImage fragments from
        // resolving the owning filter again through the CSS filter path.
        visualElement.AddStyle("filter", "none", int.MaxValue / 2);
    }

    private static SvgFilter? GetAncestorFilter(SvgElement element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (current is SvgFilter svgFilter)
            {
                return svgFilter;
            }
        }

        return null;
    }

    private static bool ReferencesFilterId(SvgVisualElement visualElement, string filterId)
    {
        foreach (var filterUri in GetFilterReferenceUris(visualElement))
        {
            if (TryGetReferenceFragment(filterUri, out var fragment) &&
                string.Equals(fragment, filterId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetReferenceFragment(Uri uri, out string fragment)
    {
        fragment = string.Empty;
        var value = uri.OriginalString.Trim();
        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(")", StringComparison.Ordinal))
        {
            value = value.Substring(4, value.Length - 5).Trim().Trim('\'', '"');
        }

        var fragmentIndex = value.LastIndexOf('#');
        if (fragmentIndex < 0 || fragmentIndex + 1 >= value.Length)
        {
            return false;
        }

        fragment = value.Substring(fragmentIndex + 1);
        return !string.IsNullOrWhiteSpace(fragment);
    }

    private static void PruneDocumentToReferencedElement(SvgDocument document, SvgElement referencedElement)
    {
        var ancestorPath = new HashSet<SvgElement>();
        for (var current = referencedElement; current is not null; current = current.Parent)
        {
            ancestorPath.Add(current);
        }

        PruneElementChildren(document, ancestorPath, referencedElement);
    }

    private static void PruneElementChildren(SvgElement parent, HashSet<SvgElement> ancestorPath, SvgElement referencedElement)
    {
        if (ReferenceEquals(parent, referencedElement) || parent is SvgDefinitionList)
        {
            return;
        }

        for (var i = parent.Children.Count - 1; i >= 0; i--)
        {
            var child = parent.Children[i];
            if (parent is SvgDocument && child is SvgDefinitionList)
            {
                continue;
            }

            if (!ancestorPath.Contains(child))
            {
                parent.Children.RemoveAt(i);
                continue;
            }

            PruneElementChildren(child, ancestorPath, referencedElement);
        }
    }

    private static bool HasDefinitionAncestor(SvgElement element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (current is SvgDefinitionList)
            {
                return true;
            }
        }

        return false;
    }

    private static SKRect? FindNodeBoundsById(SvgSceneNode node, string? elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            return null;
        }

        if (string.Equals(node.ElementId, elementId, StringComparison.Ordinal))
        {
            return node.TransformedBounds;
        }

        foreach (var child in node.Children)
        {
            var bounds = FindNodeBoundsById(child, elementId);
            if (bounds is { })
            {
                return bounds;
            }
        }

        return null;
    }

    private List<SvgSceneFilterResult>? GetMergeInputFilters(SvgMerge svgMerge, SvgColourInterpolation colorInterpolationFilters)
    {
        var inputs = new List<SvgSceneFilterResult>();

        foreach (var child in svgMerge.Children)
        {
            if (child is not SvgMergeNode svgMergeNode)
            {
                continue;
            }

            var inputFilter = GetInputFilter(svgMergeNode.Input, colorInterpolationFilters, _skFilterRegion, false);
            if (inputFilter is null)
            {
                return null;
            }

            inputs.Add(inputFilter);
        }

        return inputs;
    }

    private SKImageFilter? CreateMerge(IReadOnlyList<SvgSceneFilterResult> inputs, SvgColourInterpolation colorInterpolationFilters, SKRect primitiveRegion, SKRect? cropRect = default)
    {
        var filters = new SKImageFilter[inputs.Count];

        for (var i = 0; i < inputs.Count; i++)
        {
            var inputFilter = inputs[i];
            if (inputFilter is { })
            {
                filters[i] = ApplyColourInterpolationAndClip(inputFilter, colorInterpolationFilters, primitiveRegion)!;
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
        TransformsService.GetOptionalNumbers(svgMorphology.Radius, 0f, 0f, out var radiusX, out var radiusY);

        if (_primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = TransformsService.CalculateOtherPercentageValue(_skBounds);
            radiusX *= value;
            radiusY *= value;
        }

        if (!IsFinite(radiusX) || !IsFinite(radiusY) || radiusX < 0f || radiusY < 0f)
        {
            return default;
        }

        if (radiusX <= 0f && radiusY <= 0f)
        {
            return default;
        }

        var kernelRadiusX = radiusX <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(radiusX));
        var kernelRadiusY = radiusY <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(radiusY));

        return svgMorphology.Operator switch
        {
            SvgMorphologyOperator.Dilate => SKImageFilter.CreateDilate(kernelRadiusX, kernelRadiusY, input, cropRect),
            SvgMorphologyOperator.Erode => SKImageFilter.CreateErode(kernelRadiusX, kernelRadiusY, input, cropRect),
            _ => null,
        };
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsAxisAlignedScaleTranslate(SKMatrix matrix)
        => matrix.SkewX == 0f && matrix.SkewY == 0f;

    private static bool IsUsableRegion(SKRect region)
        => IsFinite(region.Left) &&
           IsFinite(region.Top) &&
           IsFinite(region.Right) &&
           IsFinite(region.Bottom) &&
           region.Width > 0f &&
           region.Height > 0f;

    private static bool TryConvertUnit(string value, out SvgUnit unit)
    {
        try
        {
            if (new SvgUnitConverter().ConvertFromString(value) is SvgUnit parsed &&
                IsFinite(parsed.Value))
            {
                unit = parsed;
                return true;
            }
        }
        catch
        {
            // Invalid primitive subregion declarations fall back to the default subregion.
        }

        unit = default;
        return false;
    }

    private SKImageFilter? CreateOffset(SvgOffset svgOffset, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var dxUnit = svgOffset.Dx;
        var dyUnit = svgOffset.Dy;
        var dx = CalculateHorizontal(svgOffset, dxUnit);
        var dy = CalculateVertical(svgOffset, dyUnit);
        if (!IsFinite(dx) || !IsFinite(dy))
        {
            return default;
        }

        return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
    }

    private SKImageFilter? CreateSpecularLighting(SvgSpecularLighting svgSpecularLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKRect? cropRect = default)
    {
        var lightColor = GetFilterPrimitiveColor(svgSpecularLighting, svgSpecularLighting.LightingColor);
        if (lightColor is null)
        {
            return default;
        }

        var surfaceScale = svgSpecularLighting.SurfaceScale;
        var specularConstant = svgSpecularLighting.SpecularConstant;
        if (specularConstant < 0f)
        {
            specularConstant = 0f;
        }
        var specularExponent = svgSpecularLighting.SpecularExponent;
        if (specularExponent < 1f)
        {
            specularExponent = 1f;
        }
        else if (specularExponent > 128f)
        {
            specularExponent = 128f;
        }
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
                    if (specularExponentSpotLight < 1f)
                    {
                        specularExponentSpotLight = 1f;
                    }
                    else if (specularExponentSpotLight > 128f)
                    {
                        specularExponentSpotLight = 128f;
                    }
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
        TransformsService.GetOptionalNumbers(svgTurbulence.BaseFrequency, 0f, 0f, out var baseFrequencyX, out var baseFrequencyY);

        if (!IsFinite(baseFrequencyX) || !IsFinite(baseFrequencyY) || baseFrequencyX < 0f || baseFrequencyY < 0f)
        {
            return default;
        }

        var numOctaves = svgTurbulence.NumOctaves;

        if (numOctaves < 0)
        {
            return default;
        }

        var seed = svgTurbulence.Seed;
        if (!IsFinite(seed))
        {
            return default;
        }

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
