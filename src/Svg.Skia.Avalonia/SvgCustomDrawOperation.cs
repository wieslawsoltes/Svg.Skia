// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace Svg.Skia.Avalonia
{
    internal class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly ISvg _svg;

        public SvgCustomDrawOperation(Rect bounds, ISvg svg)
        {
            _svg = svg;
            Bounds = bounds;
        }

        public void Dispose()
        {
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation other) => false;

        public void Render(IDrawingContextImpl context)
        {
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas != null)
            {
                canvas.Save();
                canvas.DrawPicture(_svg.Picture);
                canvas.Restore();
            }
        }
    }
}
