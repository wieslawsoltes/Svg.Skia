using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class FragmentDrawable : DrawableContainer
    {
        private FragmentDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static FragmentDrawable Create(SvgFragment svgFragment, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
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

            var x = svgFragmentParent is null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skOwnerBounds);
            var y = svgFragmentParent is null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skOwnerBounds);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skOwnerBounds.IsEmpty)
            {
                skOwnerBounds = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            drawable.CreateChildren(svgFragment, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgFragment);

            drawable.GeometryBounds = skOwnerBounds;

            drawable.CreateGeometryBounds();

            drawable.Transform = SvgExtensions.ToMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgExtensions.ToMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
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
                            Math.Abs(drawable.GeometryBounds.Left) + drawable.GeometryBounds.Width,
                            Math.Abs(drawable.GeometryBounds.Top) + drawable.GeometryBounds.Height);
                    }
                    else
                    {
                        drawable.Overflow = SKRect.Create(x, y, skSize.Width, skSize.Height);
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
                SvgExtensions.GetClipPath(svgClipPath, skOwnerBounds, clipPathUris, clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !drawable.IgnoreAttributes.HasFlag(DrawAttributes.ClipPath))
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

            return drawable;
        }

        public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(DrawAttributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element) : null;
            Filter = null;

            TotalTransform = totalMatrix.PreConcat(Transform);
            TransformedBounds = TotalTransform.MapRect(GeometryBounds);

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess(viewport, TotalTransform);
            }
        }
    }
}
