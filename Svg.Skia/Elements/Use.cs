// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Reflection;
using SkiaSharp;
using Svg;

namespace Svg.Skia
{
    internal struct Use
    {
        public SKMatrix matrix;

        public Use(SvgUse svgUse, SvgVisualElement svgVisualElement)
        {
            matrix = SvgHelper.GetSKMatrix(svgUse.Transforms);

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
    }
}
