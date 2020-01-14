// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class ImageDrawable : Drawable
    {
        internal SKImage? _skImage;
        internal FragmentDrawable? _fragmentDrawable;
        internal SKRect _srcRect = default;
        internal SKRect _destRect = default;

        public ImageDrawable(SvgImage svgImage, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgImage, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            float width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            float x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var location = new SKPoint(x, y);

            if (width <= 0f || height <= 0f || svgImage.Href == null)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Check for image recursive references.
            //if (SkiaUtil.HasRecursiveReference(svgImage, (e) => e.Href))
            //{
            //    _canDraw = false;
            //    return;
            //}

            var image = SvgImageUtil.GetImage(svgImage, svgImage.Href);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                IsDrawable = false;
                return;
            }

            if (skImage != null)
            {
                _disposable.Add(skImage);
            }

            _srcRect = default;

            if (skImage != null)
            {
                _srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                _srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);
            _destRect = destClip;

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / _srcRect.Width;
                var fScaleY = destClip.Height / _srcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - _srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - _srcRect.Height * fScaleY);
                        break;
                }

                _destRect = SKRect.Create(
                    destClip.Left + xOffset, destClip.Top + yOffset,
                    _srcRect.Width * fScaleX, _srcRect.Height * fScaleY);
            }

            ClipRect = destClip;

            var skClipRect = SvgClipPathUtil.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                ClipRect = skClipRect;
            }

            if (skImage != null)
            {
                _skImage = skImage;
            }

            if (svgFragment != null)
            {
                _fragmentDrawable = new FragmentDrawable(svgFragment, skOwnerBounds, ignoreDisplay);
                _disposable.Add(_fragmentDrawable);
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgImage);

            if (_skImage != null)
            {
                TransformedBounds = _destRect;
            }

            if (_fragmentDrawable != null)
            {
                //_skBounds = _fragmentDrawable._skBounds;
                TransformedBounds = _destRect;
            }

            Transform = SKMatrixUtil.GetSKMatrix(svgImage.Transforms);
            if (_fragmentDrawable != null)
            {
                float dx = _destRect.Left;
                float dy = _destRect.Top;
                float sx = _destRect.Width / _srcRect.Width;
                float sy = _destRect.Height / _srcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                SKMatrix.PreConcat(ref skTranslationMatrix, ref skScaleMatrix);
                SKMatrix.PreConcat(ref Transform, ref skTranslationMatrix);
            }

            PathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgImage, TransformedBounds, new HashSet<Uri>(), _disposable);
            PictureMask = SKPaintUtil.GetSvgVisualElementMask(svgImage, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgImage, _disposable);
            PaintFilter = SKPaintUtil.GetFilterSKPaint(svgImage, TransformedBounds, _disposable);

            PaintFill = null;
            PaintStroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

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

            if (PaintOpacity != null)
            {
                canvas.SaveLayer(PaintOpacity);
            }

            if (PaintFilter != null)
            {
                canvas.SaveLayer(PaintFilter);
            }

            if (_skImage != null)
            {
                canvas.DrawImage(_skImage, _srcRect, _destRect);
            }

            if (_fragmentDrawable != null)
            {
                _fragmentDrawable.Draw(canvas, 0f, 0f);
            }

            if (PaintFilter != null)
            {
                canvas.Restore();
            }

            if (PaintOpacity != null)
            {
                canvas.Restore();
            }

            canvas.Restore();
        }
    }
}
