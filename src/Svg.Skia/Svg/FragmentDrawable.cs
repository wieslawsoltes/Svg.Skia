using System;
using System.Collections.Generic;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class FragmentDrawable : DrawableContainer
    {
        private FragmentDrawable()
            : base()
        {
        }

        public static FragmentDrawable Create(SvgFragment svgFragment, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new FragmentDrawable
            {
                Element = svgFragment,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.HasFeatures(svgFragment, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            var svgFragmentParent = svgFragment.Parent;

            float x = svgFragmentParent is null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            float y = svgFragmentParent is null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            drawable.CreateChildren(svgFragment, skOwnerBounds, drawable, ignoreAttributes);

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgFragment);

            drawable.TransformedBounds = skOwnerBounds;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgExtensions.ToSKMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgExtensions.ToSKMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            drawable.Transform = drawable.Transform.PreConcat(skMatrixViewBox);

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    if (skSize.IsEmpty)
                    {
                        drawable.Overflow = SKRect.Create(
                            x,
                            y,
                            Math.Abs(drawable.TransformedBounds.Left) + drawable.TransformedBounds.Width,
                            Math.Abs(drawable.TransformedBounds.Top) + drawable.TransformedBounds.Height);
                    }
                    else
                    {
                        drawable.Overflow = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            var clipPathUris = new HashSet<Uri>();
            var svgClipPath = svgFragment.GetUriElementReference<SvgClipPath>("clip-path", clipPathUris);
            if (svgClipPath != null && svgClipPath.Children != null)
            {
#if USE_PICTURE
                var clipPath = new Svg.Picture.ClipPath()
                {
                    Clip = new Svg.Picture.ClipPath()
                };
                SvgExtensions.GetClipPath(svgClipPath, drawable.TransformedBounds, clipPathUris, drawable.Disposable, clipPath);
                if (clipPath.Clips != null && clipPath.Clips.Count > 0 && !drawable.IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    drawable.ClipPath = clipPath;
                }
                else
                {
                    drawable.ClipPath = null;
                }
#else
                drawable.ClipPath = drawable.IgnoreAttributes.HasFlag(Attributes.ClipPath) ?
                    null :
                    SvgExtensions.GetClipPath(svgClipPath, drawable.TransformedBounds, clipPathUris, drawable.Disposable);
#endif
            }
            else
            {
                drawable.ClipPath = null;
            }

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgExtensions.GetOpacitySKPaint(element, Disposable) : null;
            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }
}
