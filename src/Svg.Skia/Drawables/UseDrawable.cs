// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    internal class UseDrawable : BaseDrawable
    {
        internal BaseDrawable? _referencedDrawable;

        public UseDrawable(SvgUse svgUse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgUse, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                _canDraw = false;
                return;
            }

            var svgReferencedElement = SkiaUtil.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement == null)
            {
                _canDraw = false;
                return;
            }

            float x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            float width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            }

            var originalParent = svgUse.Parent;
            var useParent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (useParent != null)
            {
                useParent.SetValue(svgReferencedElement, svgUse);
            }

            svgReferencedElement.InvalidateChildPaths();

            if (svgReferencedElement is SvgSymbol svgSymbol)
            {
                _referencedDrawable = new SymbolDrawable(svgSymbol, x, y, width, height, skOwnerBounds, ignoreDisplay);
                _disposable.Add(_referencedDrawable);
            }
            else
            {
                var drawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    _referencedDrawable = drawable;
                    _disposable.Add(_referencedDrawable);
                }
                else
                {
                    _canDraw = false;
                    return;
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgUse);

            _skBounds = _referencedDrawable._skBounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
                SKMatrix.PreConcat(ref _skMatrix, ref skMatrixTranslateXY);
            }

            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgUse, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgUse, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgUse, _disposable);

            if (SkiaUtil.IsValidFill(svgUse))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgUse, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgUse, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgUse, _skBounds, _disposable);
            }

            if (useParent != null)
            {
                useParent.SetValue(svgReferencedElement, originalParent);
            }
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!_canDraw)
            {
                return;
            }

            canvas.Save();

            if (_skClipRect != null)
            {
                canvas.ClipRect(_skClipRect.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref _skMatrix);
            canvas.SetMatrix(skMatrixTotal);

            if (_skPathClip != null && !_skPathClip.IsEmpty)
            {
                canvas.ClipPath(_skPathClip, SKClipOperation.Intersect, _antialias);
            }

            if (_skPaintOpacity != null)
            {
                canvas.SaveLayer(_skPaintOpacity);
            }

            if (_skPaintFilter != null)
            {
                canvas.SaveLayer(_skPaintFilter);
            }

            _referencedDrawable?.Draw(canvas, 0f, 0f);

            if (_skPaintFilter != null)
            {
                canvas.Restore();
            }

            if (_skPaintOpacity != null)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }
    }
}
