using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Svg.Document_Structure;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class UseDrawable : DrawableBase
    {
        private static readonly FieldInfo? s_referencedElementParent = typeof(SvgElement).GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);

        public DrawableBase? ReferencedDrawable { get; set; }

        private UseDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static UseDrawable Create(SvgUse svgUse, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new UseDrawable(assetLoader, references)
            {
                Element = svgUse,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgUse, drawable.IgnoreAttributes) && drawable.HasFeatures(svgUse, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            if (SvgExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            var svgReferencedElement = SvgExtensions.GetReference<SvgElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            var x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skViewport);
            var y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skViewport);
            var width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skViewport);
            var height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skViewport);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skViewport);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, skViewport);
            }

            var originalReferencedElementParent = svgReferencedElement.Parent;

            try
            {
                if (s_referencedElementParent is { })
                {
                    s_referencedElementParent.SetValue(svgReferencedElement, svgUse);
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
                drawable.ReferencedDrawable = SymbolDrawable.Create(svgSymbol, x, y, width, height, skViewport, drawable, assetLoader, references, ignoreAttributes);
            }
            else
            {
                var referencedDrawable = DrawableFactory.Create(svgReferencedElement, skViewport, drawable, assetLoader, references, ignoreAttributes);
                if (referencedDrawable is { })
                {
                    drawable.ReferencedDrawable = referencedDrawable;
                }
                else
                {
                    drawable.IsDrawable = false;
                    return drawable;
                }
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgUse);

            // TODO: use drawable.ReferencedDrawable.GeometryBounds
            drawable.GeometryBounds = drawable.ReferencedDrawable.GeometryBounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = SKMatrix.CreateTranslation(x, y);
                drawable.Transform = drawable.Transform.PreConcat(skMatrixTranslateXY);
            }

            drawable.Fill = null;
            drawable.Stroke = null;

            try
            {
                if (s_referencedElementParent is { })
                {
                    s_referencedElementParent.SetValue(svgReferencedElement, originalReferencedElementParent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            drawable.Initialize();

            return drawable;
        }

        private void Initialize()
        {
            // TODO: Initialize
        }
        
        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            ReferencedDrawable?.Draw(canvas, ignoreAttributes, until, true);
        }

        public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
        {
            base.PostProcess(viewport, totalMatrix);

            // TODO: Fix PostProcess() using correct ReferencedElement Parent.
            ReferencedDrawable?.PostProcess(viewport, TotalTransform);
        }
    }
}
