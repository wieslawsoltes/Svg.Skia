using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Svg.Document_Structure;

namespace Svg.Model.Drawables
{
    public sealed class UseDrawable : DrawableBase
    {
        internal static FieldInfo? s_referencedElementParent = typeof(SvgElement).GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);

        public DrawableBase? ReferencedDrawable;

        private UseDrawable()
            : base()
        {
        }

        public static UseDrawable Create(SvgUse svgUse, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new UseDrawable
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

            float x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);
            float width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, skOwnerBounds);
            float height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, skOwnerBounds);

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
                if (s_referencedElementParent != null)
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
                drawable.ReferencedDrawable = SymbolDrawable.Create(svgSymbol, x, y, width, height, skOwnerBounds, drawable, ignoreAttributes);
                drawable.Disposable.Add(drawable.ReferencedDrawable);
            }
            else
            {
                var referencedDrawable = DrawableFactory.Create(svgReferencedElement, skOwnerBounds, drawable, ignoreAttributes);
                if (referencedDrawable != null)
                {
                    drawable.ReferencedDrawable = referencedDrawable;
                    drawable.Disposable.Add(drawable.ReferencedDrawable);
                }
                else
                {
                    drawable.IsDrawable = false;
                    return drawable;
                }
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgUse);

            drawable.TransformedBounds = drawable.ReferencedDrawable.TransformedBounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgUse.Transforms);
            if (!(svgReferencedElement is SvgSymbol))
            {
                var skMatrixTranslateXY = Matrix.CreateTranslation(x, y);
                drawable.Transform = drawable.Transform.PreConcat(skMatrixTranslateXY);
            }

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            try
            {
                if (s_referencedElementParent != null)
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
            if (until != null && this == until)
            {
                return;
            }

            ReferencedDrawable?.Draw(canvas, ignoreAttributes, until);
        }

        public override void PostProcess()
        {
            base.PostProcess();
            // TODO: Fix PostProcess() using correct ReferencedElement Parent.
            ReferencedDrawable?.PostProcess();
        }
    }
}
