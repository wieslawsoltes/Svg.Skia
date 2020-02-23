// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class ImageDrawable : Drawable
    {
        public SKImage? Image;
        public FragmentDrawable? FragmentDrawable;
        public SKRect SrcRect = default;
        public SKRect DestRect = default;
        public SKMatrix FragmentTransform;

        public ImageDrawable(SvgImage svgImage, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgImage, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgImage, IgnoreAttributes) && HasFeatures(svgImage, IgnoreAttributes);

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

            var image = SvgImageExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument);
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

            SrcRect = default;

            if (skImage != null)
            {
                SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / SrcRect.Width;
                var fScaleY = destClip.Height / SrcRect.Height;
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
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - SrcRect.Height * fScaleY);
                        break;
                }

                DestRect = SKRect.Create(
                    destClip.Left + xOffset,
                    destClip.Top + yOffset,
                    SrcRect.Width * fScaleX,
                    SrcRect.Height * fScaleY);
            }
            else
            {
                DestRect = destClip;
            }

            Clip = destClip;

            var skClipRect = SvgClippingExtensions.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                Clip = skClipRect;
            }

            if (skImage != null)
            {
                Image = skImage;
            }

            if (svgFragment != null)
            {
                FragmentDrawable = new FragmentDrawable(svgFragment, skOwnerBounds, root, this, ignoreAttributes);
                _disposable.Add(FragmentDrawable);
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgImage);

            if (Image != null)
            {
                TransformedBounds = DestRect;
            }

            if (FragmentDrawable != null)
            {
                //_skBounds = _fragmentDrawable._skBounds;
                TransformedBounds = DestRect;
            }

            Transform = SvgTransformsExtensions.ToSKMatrix(svgImage.Transforms);
            FragmentTransform = SKMatrix.MakeIdentity();
            if (FragmentDrawable != null)
            {
                float dx = DestRect.Left;
                float dy = DestRect.Top;
                float sx = DestRect.Width / SrcRect.Width;
                float sy = DestRect.Height / SrcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                SKMatrix.PreConcat(ref FragmentTransform, ref skTranslationMatrix);
                SKMatrix.PreConcat(ref FragmentTransform, ref skScaleMatrix);
            }

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Image != null)
            {
                using var skImagePaint = new SKPaint()
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };
                canvas.DrawImage(Image, SrcRect, DestRect, skImagePaint);
            }

            if (FragmentDrawable != null)
            {
                canvas.Save();

                var skMatrixTotal = canvas.TotalMatrix;
                SKMatrix.PreConcat(ref skMatrixTotal, ref FragmentTransform);
                canvas.SetMatrix(skMatrixTotal);

                FragmentDrawable.Draw(canvas, ignoreAttributes, until);

                canvas.Restore();
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();
            FragmentDrawable?.PostProcess();
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            if (Image != null)
            {
                if (DestRect.Contains(skPoint))
                {
                    return this;
                }
            }

            if (FragmentDrawable != null)
            {
                var result = FragmentDrawable?.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }

            return base.HitTest(skPoint);
        }
    }
}
