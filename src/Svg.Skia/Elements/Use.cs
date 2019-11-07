// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Reflection;
using SkiaSharp;
using Svg;
using Svg.Document_Structure;

namespace Svg.Skia
{
    internal struct Use : IElement
    {
        public SvgUse svgUse;
        public SvgVisualElement svgVisualElement;
        public SKMatrix matrix;

        public Use(SvgUse use, SvgVisualElement visualElement)
        {
            svgUse = use;
            svgVisualElement = visualElement;
            matrix = SKSvgHelper.GetSKMatrix(svgUse.Transforms);

            float x = svgUse.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            float y = svgUse.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);

            var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
            SKMatrix.Concat(ref matrix, ref matrix, ref skMatrixTranslateXY);

            var ew = svgUse.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            var eh = svgUse.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);
            if (ew > 0 && eh > 0)
            {
                var _attributes = svgVisualElement.GetType().GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_attributes != null)
                {
                    var attributes = _attributes.GetValue(svgVisualElement) as SvgAttributeCollection;
                    if (attributes != null)
                    {
                        var viewBox = attributes.GetAttribute<SvgViewBox>("viewBox");
                        //var viewBox = svgVisualElement.Attributes.GetAttribute<SvgViewBox>("viewBox");
                        if (viewBox != SvgViewBox.Empty && Math.Abs(ew - viewBox.Width) > float.Epsilon && Math.Abs(eh - viewBox.Height) > float.Epsilon)
                        {
                            var sw = ew / viewBox.Width;
                            var sh = eh / viewBox.Height;

                            var skMatrixTranslateSWSH = SKMatrix.MakeTranslation(sw, sh);
                            SKMatrix.Concat(ref matrix, ref matrix, ref skMatrixTranslateSWSH);
                        }
                    }
                }
                //else
                //{
                //    throw new Exception("Can not get 'use' referenced element transform.");
                //}
            }
        }

        public void Draw(SKCanvas skCanvas, SKSize skSize, CompositeDisposable disposable)
        {
            var svgVisualElement = SKSvgHelper.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement != null && !SKSvgHelper.HasRecursiveReference(svgUse))
            {
                var parent = svgUse.Parent;
                //svgVisualElement.Parent = svgUse;
                var _parent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_parent != null)
                {
                    _parent.SetValue(svgVisualElement, svgUse);
                }
                //else
                //{
                //    throw new Exception("Can not set 'use' referenced element parent.");
                //}

                svgVisualElement.InvalidateChildPaths();

                var use = new Use(svgUse, svgVisualElement);

                skCanvas.Save();

                var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgUse, disposable);
                var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgUse, disposable);
                SKSvgHelper.SetTransform(skCanvas, use.matrix);

                // TODO:
                //if (svgUse.ClipPath != null)
                //{
                //    var svgClipPath = svgVisualElement.OwnerDocument.GetElementById<SvgClipPath>(svgUse.ClipPath.ToString());
                //    if (svgClipPath != null && svgClipPath.Children != null)
                //    {
                //        foreach (var child in svgClipPath.Children)
                //        {
                //            var skPath = new SKPath();
                //        }
                //        // TODO:
                //        Console.WriteLine($"clip-path: {svgClipPath}");
                //    }
                //}

                if (svgVisualElement is SvgSymbol svgSymbol)
                {
                    DrawSymbol(skCanvas, skSize, svgSymbol);
                }
                else
                {
                    DrawElement(skCanvas, skSize, svgVisualElement);
                }

                //svgVisualElement.Parent = parent;
                if (_parent != null)
                {
                    _parent.SetValue(svgVisualElement, parent);
                }
                //else
                //{
                //    throw new Exception("Can not set 'use' referenced element parent.");
                //}

                if (skPaintFilter != null)
                {
                    skCanvas.Restore();
                }

                if (skPaintOpacity != null)
                {
                    skCanvas.Restore();
                }

                skCanvas.Restore();
            }
        }
    }
}
