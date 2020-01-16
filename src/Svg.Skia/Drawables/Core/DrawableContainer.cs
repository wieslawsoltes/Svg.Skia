// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public abstract class DrawableContainer : Drawable
    {
        public List<Drawable> ChildrenDrawables = new List<Drawable>();

        protected override void Draw(SKCanvas canvas)
        {
            foreach (var drawable in ChildrenDrawables)
            {
                drawable.Draw(canvas, 0f, 0f);
            }
        }

        public override Drawable? HitTest(SKPoint skPoint)
        {
            foreach (var drawable in ChildrenDrawables)
            {
                var result = drawable.HitTest(skPoint);
                if (result != null)
                {
                    return result;
                }
            }
            return base.HitTest(skPoint);
        }
    }
}
