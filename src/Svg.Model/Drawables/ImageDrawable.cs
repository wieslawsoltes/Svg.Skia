using System;

namespace Svg.Model.Drawables
{
    public sealed class ImageDrawable : DrawableBase
    {
        public Image? Image;
        public FragmentDrawable? FragmentDrawable;
        public Rect SrcRect = default;
        public Rect DestRect = default;
        public Matrix FragmentTransform;

        private ImageDrawable()
            : base()
        {
        }

        public static ImageDrawable Create(SvgImage svgImage, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new ImageDrawable
            {
                Element = svgImage,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgImage, drawable.IgnoreAttributes) && drawable.HasFeatures(svgImage, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            float width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            float x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            float y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var location = new Point(x, y);

            if (width <= 0f || height <= 0f || svgImage.Href is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            // TODO: Check for image recursive references.
            //if (HasRecursiveReference(svgImage, (e) => e.Href))
            //{
            //    _canDraw = false;
            //    return;
            //}

            var image = SvgExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument);
            var skImage = image as Image;
            var svgFragment = image as SvgFragment;
            if (skImage is null && svgFragment is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            if (skImage != null)
            {
                drawable.Disposable.Add(skImage);
            }

            drawable.SrcRect = default;

            if (skImage != null)
            {
                drawable.SrcRect = Rect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                drawable.SrcRect = Rect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = Rect.Create(location.X, location.Y, width, height);

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / drawable.SrcRect.Width;
                var fScaleY = destClip.Height / drawable.SrcRect.Height;
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
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX);
                        break;

                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY);
                        break;

                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY);
                        break;

                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - drawable.SrcRect.Width * fScaleX);
                        yOffset = (destClip.Height - drawable.SrcRect.Height * fScaleY);
                        break;
                }

                drawable.DestRect = Rect.Create(
                    destClip.Left + xOffset,
                    destClip.Top + yOffset,
                    drawable.SrcRect.Width * fScaleX,
                    drawable.SrcRect.Height * fScaleY);
            }
            else
            {
                drawable.DestRect = destClip;
            }

            drawable.Clip = destClip;

            var skClipRect = SvgExtensions.GetClipRect(svgImage, destClip);
            if (skClipRect != null)
            {
                drawable.Clip = skClipRect;
            }

            if (skImage != null)
            {
                drawable.Image = skImage;
            }

            if (svgFragment != null)
            {
                drawable.FragmentDrawable = FragmentDrawable.Create(svgFragment, skOwnerBounds, drawable, ignoreAttributes);
                drawable.Disposable.Add(drawable.FragmentDrawable);
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgImage);

            if (drawable.Image != null)
            {
                drawable.TransformedBounds = drawable.DestRect;
            }

            if (drawable.FragmentDrawable != null)
            {
                drawable.TransformedBounds = drawable.DestRect;
            }

            drawable.Transform = SvgExtensions.ToMatrix(svgImage.Transforms);
            drawable.FragmentTransform = Matrix.CreateIdentity();
            if (drawable.FragmentDrawable != null)
            {
                float dx = drawable.DestRect.Left;
                float dy = drawable.DestRect.Top;
                float sx = drawable.DestRect.Width / drawable.SrcRect.Width;
                float sy = drawable.DestRect.Height / drawable.SrcRect.Height;
                var skTranslationMatrix = Matrix.CreateTranslation(dx, dy);
                var skScaleMatrix = Matrix.CreateScale(sx, sy);
                drawable.FragmentTransform = drawable.FragmentTransform.PreConcat(skTranslationMatrix);
                drawable.FragmentTransform = drawable.FragmentTransform.PreConcat(skScaleMatrix);
                // TODO: FragmentTransform
            }

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Image != null)
            {
                using var skImagePaint = new Paint()
                {
                    IsAntialias = true,
                    FilterQuality = FilterQuality.High
                };
                canvas.DrawImage(Image, SrcRect, DestRect, skImagePaint);
            }

            if (FragmentDrawable != null)
            {
                canvas.Save();

                var skMatrixTotal = canvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(FragmentTransform);
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
    }
}
