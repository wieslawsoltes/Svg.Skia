// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class DrawableContainer : Drawable
    {
        public List<Drawable> ChildrenDrawables = new List<Drawable>();

        protected override void OnDraw(SKCanvas canvas)
        {
            if (!IsDrawable)
            {
                return;
            }

            canvas.Save();

            if (ClipRect != null)
            {
                canvas.ClipRect(ClipRect.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (PathClip != null && !PathClip.IsEmpty)
            {
                canvas.ClipPath(PathClip, SKClipOperation.Intersect, IsAntialias);
            }

            if (PictureMask != null)
            {
                canvas.SaveLayer(PaintTransparentBlack);
            }

            if (PaintOpacity != null)
            {
                canvas.SaveLayer(PaintOpacity);
            }

            if (PaintFilter != null)
            {
                canvas.SaveLayer(PaintFilter);
            }

            foreach (var drawable in ChildrenDrawables)
            {
                drawable.Draw(canvas, 0f, 0f);
            }

            if (PaintFilter != null)
            {
                canvas.Restore();
            }

            if (PaintOpacity != null)
            {
                canvas.Restore();
            }

            if (PictureMask != null)
            {
                canvas.SaveLayer(PaintDstIn);
                canvas.DrawPicture(PictureMask);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            foreach (var drawable in ChildrenDrawables)
            {
                var result = drawable.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }
            return base.HitTest(skPoint);
        }
    }
}
