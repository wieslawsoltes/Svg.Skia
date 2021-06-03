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
        private FragmentDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static FragmentDrawable Create(SvgFragment svgFragment, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new FragmentDrawable(assetLoader, references)
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

            var x = svgFragmentParent is null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, skViewport);
            var y = svgFragmentParent is null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, skViewport);

            var skSize = SvgExtensions.GetDimensions(svgFragment);

            if (skViewport.IsEmpty)
            {
                skViewport = SKRect.Create(x, y, skSize.Width, skSize.Height);
            }

            drawable.CreateChildren(svgFragment, skViewport, drawable, assetLoader, references, ignoreAttributes);

            drawable.Initialize(skViewport, x, y, skSize);

            return drawable;
        }

        private void Initialize(SKRect skViewport, float x, float y, SKSize skSize)
        {
            if (Element is not SvgFragment svgFragment)
            {
                return;;
            }

            IsAntialias = SvgExtensions.IsAntialias(svgFragment);

            GeometryBounds = skViewport;

            CreateGeometryBounds();

            Transform = SvgExtensions.ToMatrix(svgFragment.Transforms);
            var skMatrixViewBox = SvgExtensions.ToMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            Transform = Transform.PreConcat(skMatrixViewBox);

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;

                default:
                    if (skSize.IsEmpty)
                    {
                        Overflow = SKRect.Create(
                            x,
                            y,
                            Math.Abs(GeometryBounds.Left) + GeometryBounds.Width,
                            Math.Abs(GeometryBounds.Top) + GeometryBounds.Height);
                    }
                    else
                    {
                        Overflow = SKRect.Create(x, y, skSize.Width, skSize.Height);
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
                SvgExtensions.GetClipPath(svgClipPath, skViewport, clipPathUris, clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !IgnoreAttributes.HasFlag(DrawAttributes.ClipPath))
                {
                    ClipPath = clipPath;
                }
                else
                {
                    ClipPath = null;
                }
            }
            else
            {
                ClipPath = null;
            }

            Fill = null;
            Stroke = null;
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
