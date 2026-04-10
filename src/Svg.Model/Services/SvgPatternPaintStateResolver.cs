// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
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
        SKMatrix shaderMatrix,
        SKMatrix pictureTransform)
    {
        ContentSource = contentSource;
        PatternRect = patternRect;
        PictureViewport = pictureViewport;
        ShaderMatrix = shaderMatrix;
        PictureTransform = pictureTransform;
    }

    public SvgPatternServer ContentSource { get; }

    public SvgElementCollection Children => ContentSource.Children;

    public SKRect PatternRect { get; }

    public SKRect PictureViewport { get; }

    public SKMatrix ShaderMatrix { get; }

    public SKMatrix PictureTransform { get; }
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
        SvgPatternServer? firstViewBox = null;
        SvgPatternServer? firstAspectRatio = null;

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

            if (firstViewBox is null && pattern.ViewBox != SvgViewBox.Empty)
            {
                firstViewBox = pattern;
            }

            if (firstAspectRatio is null)
            {
                var aspectRatio = pattern.AspectRatio;
                if (aspectRatio.Align != SvgPreserveAspectRatio.xMidYMid || aspectRatio.Slice || aspectRatio.Defer)
                {
                    firstAspectRatio = pattern;
                }
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
        var viewBox = firstViewBox?.ViewBox ?? SvgViewBox.Empty;
        var aspectRatioValue = firstAspectRatio?.AspectRatio ?? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false);

        var patternRect = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, patternUnits, skBounds, skBounds, svgPatternServer);
        if (patternRect is null || patternRect.Value.Width <= 0f || patternRect.Value.Height <= 0f)
        {
            return false;
        }

        var shaderMatrix = SKMatrix.CreateIdentity();
        shaderMatrix = shaderMatrix.PreConcat(TransformsService.ToMatrix(svgPatternServer.PatternTransform));
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
        state = new SvgPatternPaintState(firstChildren, patternRect.Value, pictureViewport, shaderMatrix, pictureTransform);
        return true;
    }

    private static List<SvgPatternServer> GetLinkedPatternServers(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
    {
        var svgPatternServers = new List<SvgPatternServer>();
        var currentPatternServer = svgPatternServer;
        do
        {
            svgPatternServers.Add(currentPatternServer);
            currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
        } while (currentPatternServer is { } && currentPatternServer != svgPatternServer);

        return svgPatternServers;
    }
}
