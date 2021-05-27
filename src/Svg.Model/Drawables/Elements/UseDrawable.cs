using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Svg.Document_Structure;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class UseDrawable : DrawableBase
    {
        private static readonly FieldInfo? s_referencedElementParent = typeof(SvgElement).GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);

        public DrawableBase? ReferencedDrawable { get; set; }

        private UseDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static UseDrawable Create(SvgUse svgUse, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new UseDrawable(assetLoader)
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

            if (SvgModelExtensions.HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            var svgReferencedElement = SvgModelExtensions.GetReference<SvgElement>(svgUse, svgUse.ReferencedElement);
            if (svgReferencedElement is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            var x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            var y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            var width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            var height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);

            if (width <= 0f)
            {
                width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            }

            if (height <= 0f)
            {
                height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
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
                drawable.ReferencedDrawable = SymbolDrawable.Create(svgSymbol, x, y, width, height, skOwnerBounds, drawable, assetLoader, ignoreAttributes);
            }
            else
            {
                var referencedDrawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, drawable, assetLoader, ignoreAttributes);
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

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgUse);

            drawable.GeometryBounds = drawable.ReferencedDrawable.TransformedBounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = Matrix.CreateTranslation(x, y);
                drawable.Transform = drawable.Transform.PreConcat(skMatrixTranslateXY);
            }

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

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

            return drawable;
        }

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            ReferencedDrawable?.Draw(canvas, ignoreAttributes, until, true);
        }

        public override void PostProcess(Rect? viewport)
        {
            base.PostProcess(viewport);
            // TODO: Fix PostProcess() using correct ReferencedElement Parent.
            ReferencedDrawable?.PostProcess(viewport);
        }
    }
}
