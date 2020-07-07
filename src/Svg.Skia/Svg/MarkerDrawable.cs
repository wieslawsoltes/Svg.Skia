﻿using System;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
using Svg.DataTypes;
#if USE_PICTURE
using SKCanvas = Svg.Picture.Canvas;
using SKClipOperation = Svg.Picture.ClipOperation;
using SKMatrix = Svg.Picture.Matrix;
using SKPoint = Svg.Picture.Point;
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class MarkerDrawable : DrawableBase
    {
        public DrawableBase? MarkerElementDrawable;
        public SKRect? MarkerClipRect;

        private MarkerDrawable()
            : base()
        {
        }

        public static MarkerDrawable Create(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new MarkerDrawable
            {
                Element = svgMarker,
                Parent = parent,
                IgnoreAttributes = Attributes.Display | ignoreAttributes,
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

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);

            var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, skOwnerBounds);
            var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, skOwnerBounds);
            float markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            float markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            float viewBoxToMarkerUnitsScaleX = 1f;
            float viewBoxToMarkerUnitsScaleY = 1f;

            switch (svgMarker.MarkerUnits)
            {
                case SvgMarkerUnits.StrokeWidth:
                    {
                        var skMatrixStrokeWidth = SKMatrix.CreateScale(strokeWidth, strokeWidth);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = (viewBoxWidth <= 0) ? 1 : (markerWidth / viewBoxWidth);
                        var scaleFactorHeight = (viewBoxHeight <= 0) ? 1 : (markerHeight / viewBoxHeight);

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

            var markerElementDrawable = DrawableFactory.Create(markerElement, skOwnerBounds, drawable, Attributes.Display);
            if (markerElementDrawable != null)
            {
                drawable.MarkerElementDrawable = markerElementDrawable;
                drawable.Disposable.Add(drawable.MarkerElementDrawable);
            }
            else
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgMarker);

            drawable.TransformedBounds = drawable.MarkerElementDrawable.TransformedBounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgMarker.Transforms);
            drawable.Transform = drawable.Transform.PreConcat(skMarkerMatrix);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
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

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (MarkerClipRect != null)
            {
                canvas.ClipRect(MarkerClipRect.Value, SKClipOperation.Intersect);
            }

            MarkerElementDrawable?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            MarkerElementDrawable?.PostProcess();
        }
    }
}
