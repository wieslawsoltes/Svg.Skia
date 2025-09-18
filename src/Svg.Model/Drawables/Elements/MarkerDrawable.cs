// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.DataTypes;
using Svg.Model.Drawables.Factories;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class MarkerDrawable : DrawableBase
{
    public DrawableBase? MarkerElementDrawable { get; set; }
    public SKRect? MarkerClipRect { get; set; }

    private MarkerDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static MarkerDrawable Create(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new MarkerDrawable(assetLoader, references)
        {
            Element = svgMarker,
            Parent = parent,
            IgnoreAttributes = DrawAttributes.Display | ignoreAttributes,
            IsDrawable = true
        };

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        var markerElement = drawable.GetMarkerElement(svgMarker);
        if (markerElement is null)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        var skMarkerMatrix = SKMatrix.CreateIdentity();

        var skMatrixMarkerPoint = SKMatrix.CreateTranslation(pMarkerPoint.X, pMarkerPoint.Y);
        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixMarkerPoint);

        var skMatrixAngle = SKMatrix.CreateRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixAngle);

        var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skViewport);

        var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, skViewport);
        var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, skViewport);
        var markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skViewport);
        var markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, skViewport);
        var viewBoxToMarkerUnitsScaleX = 1f;
        var viewBoxToMarkerUnitsScaleY = 1f;

        switch (svgMarker.MarkerUnits)
        {
            case SvgMarkerUnits.StrokeWidth:
                {
                    var skMatrixStrokeWidth = SKMatrix.CreateScale(strokeWidth, strokeWidth);
                    skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixStrokeWidth);

                    var viewBoxWidth = svgMarker.ViewBox.Width;
                    var viewBoxHeight = svgMarker.ViewBox.Height;

                    var scaleFactorWidth = viewBoxWidth <= 0 ? 1 : markerWidth / viewBoxWidth;
                    var scaleFactorHeight = viewBoxHeight <= 0 ? 1 : markerHeight / viewBoxHeight;

                    viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                    viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                    var skMatrixTranslateRefXY = SKMatrix.CreateTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                    skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixTranslateRefXY);

                    var skMatrixScaleXY = SKMatrix.CreateScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                    skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixScaleXY);
                }
                break;

            case SvgMarkerUnits.UserSpaceOnUse:
                {
                    var skMatrixTranslateRefXY = SKMatrix.CreateTranslation(-refX, -refY);
                    skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixTranslateRefXY);
                }
                break;
        }

        switch (svgMarker.Overflow)
        {
            case SvgOverflow.Auto:
            case SvgOverflow.Visible:
            case SvgOverflow.Inherit:
                break;

            default:
                drawable.MarkerClipRect = SKRect.Create(
                    svgMarker.ViewBox.MinX,
                    svgMarker.ViewBox.MinY,
                    markerWidth / viewBoxToMarkerUnitsScaleX,
                    markerHeight / viewBoxToMarkerUnitsScaleY);
                break;
        }

        var markerElementDrawable = DrawableFactory.Create(markerElement, skViewport, drawable, assetLoader, references, DrawAttributes.Display);
        if (markerElementDrawable is { })
        {
            drawable.MarkerElementDrawable = markerElementDrawable;
        }
        else
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.Initialize(skMarkerMatrix);

        return drawable;
    }

    private void Initialize(SKMatrix skMarkerMatrix)
    {
        if (Element is not SvgMarker svgMarker || MarkerElementDrawable is null)
        {
            return;
        }

        IsAntialias = PaintingService.IsAntialias(svgMarker);

        GeometryBounds = MarkerElementDrawable.GeometryBounds;

        Transform = TransformsService.ToMatrix(svgMarker.Transforms);
        Transform = Transform.PreConcat(skMarkerMatrix);

        Fill = null;
        Stroke = null;
    }

    internal SvgVisualElement? GetMarkerElement(SvgMarker svgMarker)
    {
        SvgVisualElement? markerElement = null;

        foreach (var child in svgMarker.Children)
        {
            if (child is SvgVisualElement svgVisualElement)
            {
                markerElement = svgVisualElement;
                break;
            }
        }

        return markerElement;
    }

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        if (MarkerClipRect is { })
        {
            canvas.ClipRect(MarkerClipRect.Value, SKClipOperation.Intersect);
        }

        MarkerElementDrawable?.Draw(canvas, ignoreAttributes, until, true);
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        MarkerElementDrawable?.PostProcess(viewport, TotalTransform);
    }
}
