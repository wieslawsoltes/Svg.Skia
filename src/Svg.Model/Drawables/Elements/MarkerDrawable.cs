using System;
using Svg.DataTypes;
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class MarkerDrawable : DrawableBase
    {
        public DrawableBase? MarkerElementDrawable { get; set; }
        public Rect? MarkerClipRect { get; set; }

        private MarkerDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static MarkerDrawable Create(SvgMarker svgMarker, SvgVisualElement pOwner, Point pMarkerPoint, float fAngle, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new MarkerDrawable(assetLoader)
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

            var skMarkerMatrix = Matrix.CreateIdentity();

            var skMatrixMarkerPoint = Matrix.CreateTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixMarkerPoint);

            var skMatrixAngle = Matrix.CreateRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixAngle);

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);

            var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, skOwnerBounds);
            var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, skOwnerBounds);
            var markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            var markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, skOwnerBounds);
            var viewBoxToMarkerUnitsScaleX = 1f;
            var viewBoxToMarkerUnitsScaleY = 1f;

            switch (svgMarker.MarkerUnits)
            {
                case SvgMarkerUnits.StrokeWidth:
                    {
                        var skMatrixStrokeWidth = Matrix.CreateScale(strokeWidth, strokeWidth);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = viewBoxWidth <= 0 ? 1 : markerWidth / viewBoxWidth;
                        var scaleFactorHeight = viewBoxHeight <= 0 ? 1 : markerHeight / viewBoxHeight;

                        viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                        viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                        var skMatrixTranslateRefXY = Matrix.CreateTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixTranslateRefXY);

                        var skMatrixScaleXY = Matrix.CreateScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                        skMarkerMatrix = skMarkerMatrix.PreConcat(skMatrixScaleXY);
                    }
                    break;

                case SvgMarkerUnits.UserSpaceOnUse:
                    {
                        var skMatrixTranslateRefXY = Matrix.CreateTranslation(-refX, -refY);
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
                    drawable.MarkerClipRect = Rect.Create(
                        svgMarker.ViewBox.MinX,
                        svgMarker.ViewBox.MinY,
                        markerWidth / viewBoxToMarkerUnitsScaleX,
                        markerHeight / viewBoxToMarkerUnitsScaleY);
                    break;
            }

            var markerElementDrawable = DrawableFactory.Create(markerElement, skOwnerBounds, drawable, assetLoader, Attributes.Display);
            if (markerElementDrawable is { })
            {
                drawable.MarkerElementDrawable = markerElementDrawable;
            }
            else
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgMarker);

            drawable.TransformedBounds = drawable.MarkerElementDrawable.TransformedBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgMarker.Transforms);
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

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            if (MarkerClipRect is { })
            {
                canvas.ClipRect(MarkerClipRect.Value, ClipOperation.Intersect);
            }

            MarkerElementDrawable?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess(Rect? viewport)
        {
            base.PostProcess(viewport);
            MarkerElementDrawable?.PostProcess(viewport);
        }
    }
}
