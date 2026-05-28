// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.DataTypes;

namespace Svg.Model.Services;

internal sealed class SvgPatternPaintState
{
    public SvgPatternPaintState(
        SvgPatternServer contentSource,
        SKRect patternRect,
        SKRect pictureViewport,
        SKRect pictureCullRect,
        SKRect shaderTile,
        SKMatrix shaderMatrix,
        SKMatrix pictureTransform,
        SKRect tileClip,
        bool clipTile)
    {
        ContentSource = contentSource;
        PatternRect = patternRect;
        PictureViewport = pictureViewport;
        PictureCullRect = pictureCullRect;
        ShaderTile = shaderTile;
        ShaderMatrix = shaderMatrix;
        PictureTransform = pictureTransform;
        TileClip = tileClip;
        ClipTile = clipTile;
    }

    public SvgPatternServer ContentSource { get; }

    public SvgElementCollection Children => ContentSource.Children;

    public SKRect PatternRect { get; }

    public SKRect PictureViewport { get; }

    public SKRect PictureCullRect { get; }

    public SKRect ShaderTile { get; }

    public SKMatrix ShaderMatrix { get; }

    public SKMatrix PictureTransform { get; }

    public SKRect TileClip { get; }

    public bool ClipTile { get; }
}

internal static class SvgPatternPaintStateResolver
{
    public static bool TryCreate(
        SvgPatternServer svgPatternServer,
        SvgVisualElement svgVisualElement,
        SKRect skBounds,
        out SvgPatternPaintState? state)
    {
        state = null;

        var svgReferencedPatternServers = GetLinkedPatternServers(svgPatternServer, svgVisualElement);

        SvgPatternServer? firstChildren = null;
        SvgPatternServer? firstX = null;
        SvgPatternServer? firstY = null;
        SvgPatternServer? firstWidth = null;
        SvgPatternServer? firstHeight = null;
        SvgPatternServer? firstPatternUnit = null;
        SvgPatternServer? firstPatternContentUnit = null;
        SvgPatternServer? firstPatternTransform = null;
        SvgPatternServer? firstViewBox = null;
        SvgPatternServer? firstAspectRatio = null;
        SvgOverflow? firstOverflow = null;

        foreach (var pattern in svgReferencedPatternServers)
        {
            if (firstChildren is null && pattern.Children.Count > 0)
            {
                firstChildren = pattern;
            }

            if (firstX is null && pattern.X != SvgUnit.None)
            {
                firstX = pattern;
            }

            if (firstY is null && pattern.Y != SvgUnit.None)
            {
                firstY = pattern;
            }

            if (firstWidth is null && pattern.Width != SvgUnit.None)
            {
                firstWidth = pattern;
            }

            if (firstHeight is null && pattern.Height != SvgUnit.None)
            {
                firstHeight = pattern;
            }

            if (firstPatternUnit is null && SvgService.TryGetAttribute(pattern, "patternUnits", out _))
            {
                firstPatternUnit = pattern;
            }

            if (firstPatternContentUnit is null && SvgService.TryGetAttribute(pattern, "patternContentUnits", out _))
            {
                firstPatternContentUnit = pattern;
            }

            if (firstPatternTransform is null && SvgService.TryGetAttribute(pattern, "patternTransform", out _))
            {
                firstPatternTransform = pattern;
            }

            if (firstViewBox is null && pattern.ViewBox != SvgViewBox.Empty)
            {
                firstViewBox = pattern;
            }

            if (firstAspectRatio is null && SvgService.TryGetAttribute(pattern, "preserveAspectRatio", out _))
            {
                firstAspectRatio = pattern;
            }

            if (firstOverflow is null && TryGetSpecifiedOverflow(pattern, out var patternOverflow))
            {
                firstOverflow = patternOverflow;
            }
        }

        if (firstChildren is null || firstWidth is null || firstHeight is null)
        {
            return false;
        }

        var xUnit = firstX?.X ?? new SvgUnit(0f);
        var yUnit = firstY?.Y ?? new SvgUnit(0f);
        var widthUnit = firstWidth.Width;
        var heightUnit = firstHeight.Height;
        var patternUnits = firstPatternUnit?.PatternUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var patternContentUnits = firstPatternContentUnit?.PatternContentUnits ?? SvgCoordinateUnits.UserSpaceOnUse;
        var patternTransform = firstPatternTransform?.PatternTransform;
        var viewBox = firstViewBox?.ViewBox ?? SvgViewBox.Empty;
        var aspectRatioValue = firstAspectRatio?.AspectRatio ?? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false);
        var overflow = firstOverflow ?? SvgOverflow.Hidden;
        var clipTile = overflow is not SvgOverflow.Auto and not SvgOverflow.Visible and not SvgOverflow.Inherit;

        var patternRect = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, patternUnits, skBounds, skBounds, svgPatternServer);
        if (patternRect is null || patternRect.Value.Width <= 0f || patternRect.Value.Height <= 0f)
        {
            return false;
        }

        var shaderMatrix = SKMatrix.CreateIdentity();
        shaderMatrix = shaderMatrix.PreConcat(TransformsService.ToMatrix(patternTransform));
        if (!shaderMatrix.TryInvert(out _))
        {
            return false;
        }

        shaderMatrix = shaderMatrix.PreConcat(SKMatrix.CreateTranslation(patternRect.Value.Left, patternRect.Value.Top));

        var pictureTransform = SKMatrix.CreateIdentity();
        if (!viewBox.Equals(SvgViewBox.Empty))
        {
            pictureTransform = pictureTransform.PreConcat(TransformsService.ToMatrix(
                viewBox,
                aspectRatioValue,
                0f,
                0f,
                patternRect.Value.Width,
                patternRect.Value.Height));
        }
        else if (patternContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            pictureTransform = pictureTransform.PreConcat(SKMatrix.CreateScale(skBounds.Width, skBounds.Height));
        }

        var pictureViewport = SKRect.Create(0f, 0f, patternRect.Value.Width, patternRect.Value.Height);
        var pictureCullRect = clipTile ? pictureViewport : SKRect.Empty;
        state = new SvgPatternPaintState(
            firstChildren,
            patternRect.Value,
            pictureViewport,
            pictureCullRect,
            pictureViewport,
            shaderMatrix,
            pictureTransform,
            pictureViewport,
            clipTile);
        return true;
    }

    private static List<SvgPatternServer> GetLinkedPatternServers(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
    {
        var svgPatternServers = new List<SvgPatternServer>();
        var visited = new HashSet<SvgPatternServer>();
        var currentPatternServer = svgPatternServer;
        do
        {
            if (!visited.Add(currentPatternServer))
            {
                break;
            }

            svgPatternServers.Add(currentPatternServer);
            currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
        } while (currentPatternServer is { });

        return svgPatternServers;
    }

    private static bool TryGetSpecifiedOverflow(SvgPatternServer pattern, out SvgOverflow overflow)
    {
        if (pattern.TryGetOwnCascadedStyleDeclarationValue("overflow", out var styleOverflow) &&
            TryParseOverflow(styleOverflow, out overflow))
        {
            return true;
        }

        if (SvgService.TryGetAttribute(pattern, "overflow", out var attributeOverflow) &&
            TryParseOverflow(attributeOverflow, out overflow))
        {
            return true;
        }

        overflow = SvgOverflow.Hidden;
        return false;
    }

    private static bool TryParseOverflow(string value, out SvgOverflow overflow)
    {
        try
        {
            if (new SvgOverflowConverter().ConvertFromString(value) is SvgOverflow parsedOverflow)
            {
                overflow = parsedOverflow;
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException)
        {
        }

        overflow = SvgOverflow.Hidden;
        return false;
    }
}
