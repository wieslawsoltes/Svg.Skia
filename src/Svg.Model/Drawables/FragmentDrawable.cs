using System;
using System.Collections.Generic;
using Svg.Model.Path;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    public sealed class FragmentDrawable : DrawableContainer
    {
        private FragmentDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static FragmentDrawable Create(SvgFragment svgFragment, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new FragmentDrawable(assetLoader)
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

            var skSize = SvgModelExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = Rect.Create(x, y, skSize.Width, skSize.Height);
            }

            drawable.CreateChildren(svgFragment, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgFragment);

            drawable.TransformedBounds = skOwnerBounds;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgModelExtensions.ToMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgModelExtensions.ToMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
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
                        drawable.Overflow = Rect.Create(
                            x,
                            y,
                            Math.Abs(drawable.TransformedBounds.Left) + drawable.TransformedBounds.Width,
                            Math.Abs(drawable.TransformedBounds.Top) + drawable.TransformedBounds.Height);
                    }
                    else
                    {
                        drawable.Overflow = Rect.Create(x, y, skSize.Width, skSize.Height);
                    }
                    break;
            }

            var clipPathUris = new HashSet<Uri>();
            var svgClipPath = svgFragment.GetUriElementReference<SvgClipPath>("clip-path", clipPathUris);
            if (svgClipPath?.Children is { })
            {
                var clipPath = new ClipPath
                {
                    Clip = new ClipPath()
                };
                SvgModelExtensions.GetClipPath(svgClipPath, drawable.TransformedBounds, clipPathUris, clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !drawable.IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    drawable.ClipPath = clipPath;
                }
                else
                {
                    drawable.ClipPath = null;
                }
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
            Opacity = enableOpacity ? SvgModelExtensions.GetOpacityPaint(element) : null;
            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }
}
