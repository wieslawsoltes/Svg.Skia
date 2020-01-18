// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class UseDrawable : Drawable
    {
        public Drawable? ReferencedDrawable;

        public UseDrawable(SvgUse svgUse, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgUse, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                IsDrawable = false;
                return;
            }

            var svgReferencedElement = SvgExtensions.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement == null)
            {
                IsDrawable = false;
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

            var originalReferencedElementParent = svgReferencedElement.Parent;
            var referencedElementParent = default(FieldInfo);

            try
            {
                referencedElementParent = svgReferencedElement.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (referencedElementParent != null)
                {
                    referencedElementParent.SetValue(svgReferencedElement, svgUse);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            svgReferencedElement.InvalidateChildPaths();

            if (svgReferencedElement is SvgSymbol svgSymbol)
            {
                ReferencedDrawable = new SymbolDrawable(svgSymbol, x, y, width, height, skOwnerBounds, ignoreAttributes);
                _disposable.Add(ReferencedDrawable);
            }
            else
            {
                var drawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, ignoreAttributes);
                if (drawable != null)
                {
                    ReferencedDrawable = drawable;
                    _disposable.Add(ReferencedDrawable);
                }
                else
                {
                    IsDrawable = false;
                    return;
                }
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgUse);

            TransformedBounds = ReferencedDrawable.TransformedBounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
                SKMatrix.PreConcat(ref Transform, ref skMatrixTranslateXY);
            }

            ClipPath = SvgClipPathUtil.GetSvgVisualElementClipPath(svgUse, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgUse, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints(); 
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintUtil.GetOpacitySKPaint(svgUse, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SKPaintUtil.GetFilterSKPaint(svgUse, TransformedBounds, _disposable);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            try
            {
                if (referencedElementParent != null)
                {
                    referencedElementParent.SetValue(svgReferencedElement, originalReferencedElementParent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        protected override void Draw(SKCanvas canvas)
        {
            ReferencedDrawable?.Draw(canvas, 0f, 0f);
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            var result = ReferencedDrawable?.HitTest(skPoint);
            if (result != null)
            {
                return result;
            }
            return base.HitTest(skPoint);
        }
    }
}
