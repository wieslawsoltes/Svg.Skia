// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.DataTypes;

namespace Svg.Skia
{
    public class MarkerDrawable : Drawable
    {
        public Drawable? MarkerElementDrawable;
        public SKRect? MarkerClipRect;

        public MarkerDrawable(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = IgnoreAttributes.Display | ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            var markerElement = GetMarkerElement(svgMarker);
            if (markerElement == null)
            {
                IsDrawable = false;
                return;
            }

            var skMarkerMatrix = SKMatrix.MakeIdentity();

            var skMatrixMarkerPoint = SKMatrix.MakeTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixMarkerPoint);

            var skMatrixAngle = SKMatrix.MakeRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixAngle);

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
                        var skMatrixStrokeWidth = SKMatrix.MakeScale(strokeWidth, strokeWidth);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = (viewBoxWidth <= 0) ? 1 : (markerWidth / viewBoxWidth);
                        var scaleFactorHeight = (viewBoxHeight <= 0) ? 1 : (markerHeight / viewBoxHeight);

                        viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                        viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);

                        var skMatrixScaleXY = SKMatrix.MakeScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixScaleXY);
                    }
                    break;
                case SvgMarkerUnits.UserSpaceOnUse:
                    {
                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX, -refY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);
                    }
                    break;
            }

            MarkerClipRect = SKRect.Create(
                svgMarker.ViewBox.MinX,
                svgMarker.ViewBox.MinY,
                markerWidth / viewBoxToMarkerUnitsScaleX,
                markerHeight / viewBoxToMarkerUnitsScaleY);

            var drawable = DrawableFactory.Create(markerElement, skOwnerBounds, IgnoreAttributes.Display);
            if (drawable != null)
            {
                MarkerElementDrawable = drawable;
                _disposable.Add(MarkerElementDrawable);
            }
            else
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgMarker);

            TransformedBounds = MarkerElementDrawable.TransformedBounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgMarker.Transforms);
            SKMatrix.PreConcat(ref Transform, ref skMarkerMatrix);

            Fill = null;
            Stroke = null;

            ClipPath = IgnoreAttributes.HasFlag(IgnoreAttributes.Clip) ? null : SvgClippingExtensions.GetSvgVisualElementClipPath(svgMarker, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = IgnoreAttributes.HasFlag(IgnoreAttributes.Mask) ? null : SvgClippingExtensions.GetSvgVisualElementMask(svgMarker, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = IgnoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgMarker, _disposable);
            Filter = IgnoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgMarker, TransformedBounds, this, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
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

        protected override void Record(SKCanvas canvas, IgnoreAttributes ignoreAttributes)
        {
            if (MarkerClipRect != null)
            {
                canvas.ClipRect(MarkerClipRect.Value, SKClipOperation.Intersect);
            }

            MarkerElementDrawable?.RecordPicture(canvas, ignoreAttributes);
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = MarkerElementDrawable?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }
}
