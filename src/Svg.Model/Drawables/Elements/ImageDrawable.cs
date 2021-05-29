using System;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class ImageDrawable : DrawableBase
    {
        public SKImage? Image { get; set; }
        public FragmentDrawable? FragmentDrawable { get; set; }
        public SKRect SrcRect { get; set; }
        public SKRect DestRect { get; set; }
        public SKMatrix FragmentTransform { get; set; }

        private ImageDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static ImageDrawable Create(SvgImage svgImage, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new ImageDrawable(assetLoader)
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

            var width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            var height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skOwnerBounds);
            var y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skOwnerBounds);
            var location = new SKPoint(x, y);

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

            var image = SvgExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument, assetLoader);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage is null && svgFragment is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.SrcRect = default;

            if (skImage is { })
            {
                drawable.SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment is { })
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                drawable.SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);
            drawable.DestRect = SvgExtensions.CalculateRect(svgImage.AspectRatio, drawable.SrcRect, destClip);
            drawable.Clip = destClip;

            var skClipRect = SvgExtensions.GetClipRect(svgImage.Clip, destClip);
            if (skClipRect is { })
            {
                drawable.Clip = skClipRect;
            }

            if (skImage is { })
            {
                drawable.Image = skImage;
            }

            if (svgFragment is { })
            {
                drawable.FragmentDrawable = FragmentDrawable.Create(svgFragment, skOwnerBounds, drawable, assetLoader, ignoreAttributes);
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgImage);

            drawable.GeometryBounds = default(SKRect);

            if (drawable.Image is { })
            {
                drawable.GeometryBounds = drawable.DestRect;
                drawable.TransformedBounds = drawable.GeometryBounds;
            }

            if (drawable.FragmentDrawable is { })
            {
                drawable.GeometryBounds = drawable.DestRect;
                drawable.TransformedBounds = drawable.GeometryBounds;
            }

            drawable.Transform = SvgExtensions.ToMatrix(svgImage.Transforms);
            drawable.FragmentTransform = SKMatrix.CreateIdentity();

            if (drawable.FragmentDrawable is { })
            {
                var dx = drawable.DestRect.Left;
                var dy = drawable.DestRect.Top;
                var sx = drawable.DestRect.Width / drawable.SrcRect.Width;
                var sy = drawable.DestRect.Height / drawable.SrcRect.Height;
                var skTranslationMatrix = SKMatrix.CreateTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.CreateScale(sx, sy);
                drawable.FragmentTransform = drawable.FragmentTransform.PreConcat(skTranslationMatrix);
                drawable.FragmentTransform = drawable.FragmentTransform.PreConcat(skScaleMatrix);
                // TODO: FragmentTransform
            }

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.Fill = null;
            drawable.Stroke = null;

            return drawable;
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            if (Image is { })
            {
                var skImagePaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };
                canvas.DrawImage(Image, SrcRect, DestRect, skImagePaint);
            }

            if (FragmentDrawable is { })
            {
                canvas.Save();

                var skMatrixTotal = canvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(FragmentTransform);
                canvas.SetMatrix(skMatrixTotal);

                FragmentDrawable.Draw(canvas, ignoreAttributes, until, true);

                canvas.Restore();
            }
        }

        public override void PostProcess(SKRect? viewport)
        {
            base.PostProcess(viewport);
            FragmentDrawable?.PostProcess(viewport);
        }
    }
}
