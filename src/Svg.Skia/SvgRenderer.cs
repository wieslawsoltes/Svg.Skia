// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SvgRenderer : ISvgRenderer
    {
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public void DrawFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment)
        {
            skCanvas.Save();

            var fragment = new Fragment(svgFragment);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgFragment, _disposable);
            SvgHelper.SetTransform(skCanvas, fragment.matrix);

            DrawElementCollection(skCanvas, skSize, svgFragment.Children);

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        public void DrawSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol)
        {
            skCanvas.Save();

            var symbol = new Symbol(svgSymbol);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgSymbol, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgSymbol, _disposable);
            SvgHelper.SetTransform(skCanvas, symbol.matrix);

            DrawElementCollection(skCanvas, skSize, svgSymbol.Children);

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

        public void DrawImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage)
        {
            skCanvas.Save();

            var image = new Image(svgImage);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgImage, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgImage, _disposable);
            SvgHelper.SetTransform(skCanvas, image.matrix);

            // TODO:

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

        public void DrawSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch)
        {
            skCanvas.Save();

            var @switch = new Switch(svgSwitch);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgSwitch, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgSwitch, _disposable);
            SvgHelper.SetTransform(skCanvas, @switch.matrix);

            // TODO:

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

        public void DrawUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse)
        {
            var svgVisualElement = SvgHelper.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement != null && !SvgHelper.HasRecursiveReference(svgUse))
            {
                skCanvas.Save();

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

                var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgUse, _disposable);
                var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgUse, _disposable);
                SvgHelper.SetTransform(skCanvas, use.matrix);

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

        public void DrawForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject)
        {
            skCanvas.Save();

            var foreignObject = new ForeignObject(svgForeignObject);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgForeignObject, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgForeignObject, _disposable);
            SvgHelper.SetTransform(skCanvas, foreignObject.matrix);

            // TODO:

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

        public void DrawCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle)
        {
            skCanvas.Save();

            var circle = new Circle(svgCircle);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgCircle, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgCircle, _disposable);
            SvgHelper.SetTransform(skCanvas, circle.matrix);

            if (svgCircle.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgCircle, skSize, circle.bounds, _disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            if (svgCircle.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgCircle, skSize, circle.bounds, _disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

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

        public void DrawEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse)
        {
            skCanvas.Save();

            var ellipse = new Ellipse(svgEllipse);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgEllipse, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgEllipse, _disposable);
            SvgHelper.SetTransform(skCanvas, ellipse.matrix);

            if (svgEllipse.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgEllipse, skSize, ellipse.bounds, _disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            if (svgEllipse.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgEllipse, skSize, ellipse.bounds, _disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

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

        public void DrawRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle)
        {
            var rectangle = new Rectangle(svgRectangle);

            skCanvas.Save();

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgRectangle, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgRectangle, _disposable);
            SvgHelper.SetTransform(skCanvas, rectangle.matrix);

            if (svgRectangle.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgRectangle, skSize, rectangle.bounds, _disposable))
                {
                    if (rectangle.isRound)
                    {
                        skCanvas.DrawRoundRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, rectangle.rx, rectangle.ry, skPaint);
                    }
                    else
                    {
                        skCanvas.DrawRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height, skPaint);
                    }
                }
            }

            if (svgRectangle.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgRectangle, skSize, rectangle.bounds, _disposable))
                {
                    if (rectangle.isRound)
                    {
                        skCanvas.DrawRoundRect(rectangle.bounds, rectangle.rx, rectangle.ry, skPaint);
                    }
                    else
                    {
                        skCanvas.DrawRect(rectangle.bounds, skPaint);
                    }
                }
            }

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

        public void DrawMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker)
        {
            skCanvas.Save();

            var marker = new Marker(svgMarker);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgMarker, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgMarker, _disposable);
            SvgHelper.SetTransform(skCanvas, marker.matrix);

            // TODO:

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

        public void DrawGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph)
        {
            skCanvas.Save();

            var glyph = new Glyph(svgGlyph);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgGlyph, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgGlyph, _disposable);
            SvgHelper.SetTransform(skCanvas, glyph.matrix);

            // TODO:

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

        public void DrawGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup)
        {
            var group = new Group(svgGroup);

            skCanvas.Save();

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgGroup, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgGroup, _disposable);
            SvgHelper.SetTransform(skCanvas, group.matrix);

            DrawElementCollection(skCanvas, skSize, svgGroup.Children);

            if (skPaintFilter != null)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

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

        public void DrawLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine)
        {
            var line = new Line(svgLine);

            skCanvas.Save();

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgLine, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgLine, _disposable);
            SvgHelper.SetTransform(skCanvas, line.matrix);

            if (svgLine.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgLine, skSize, line.bounds, _disposable))
                {
                    skCanvas.DrawLine(line.x0, line.y0, line.x1, line.y1, skPaint);
                }
            }

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

        public void DrawPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath)
        {
            skCanvas.Save();

            var path = new Path(svgPath);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgPath, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgPath, _disposable);
            SvgHelper.SetTransform(skCanvas, path.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPath.PathData, svgPath.FillRule))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPath.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPath, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPath.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPath, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

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

        public void DrawPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline)
        {
            skCanvas.Save();

            var polyline = new Polyline(svgPolyline);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgPolyline, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgPolyline, _disposable);
            SvgHelper.SetTransform(skCanvas, polyline.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolyline.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPolyline, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolyline.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPolyline, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

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

        public void DrawPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon)
        {
            skCanvas.Save();

            var polygon = new Polygon(svgPolygon);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgPolygon, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgPolygon, _disposable);
            SvgHelper.SetTransform(skCanvas, polygon.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolygon.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPolygon, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolygon.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPolygon, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

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

        public void DrawText(SKCanvas skCanvas, SKSize skSize, SvgText svgText)
        {
            skCanvas.Save();

            var text = new Text(svgText);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgText, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgText, _disposable);
            SvgHelper.SetTransform(skCanvas, text.matrix);

            // TODO:

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

        public void DrawTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath)
        {
            skCanvas.Save();

            var textPath = new TextPath(svgTextPath);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgTextPath, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgTextPath, _disposable);
            SvgHelper.SetTransform(skCanvas, textPath.matrix);

            // TODO:

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

        public void DrawTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef)
        {
            skCanvas.Save();

            var textRef = new TextRef(svgTextRef);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgTextRef, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgTextRef, _disposable);
            SvgHelper.SetTransform(skCanvas, textRef.matrix);

            // TODO:

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

        public void DrawTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan)
        {
            skCanvas.Save();

            var textSpan = new TextSpan(svgTextSpan);

            var skPaintOpacity = SvgHelper.SetOpacity(skCanvas, svgTextSpan, _disposable);
            var skPaintFilter = SvgHelper.SetFilter(skCanvas, svgTextSpan, _disposable);
            SvgHelper.SetTransform(skCanvas, textSpan.matrix);

            // TODO:

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

        public void DrawElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    {
                        DrawFragment(skCanvas, skSize, svgFragment);
                    }
                    break;
                case SvgImage svgImage:
                    {
                        DrawImage(skCanvas, skSize, svgImage);
                    }
                    break;
                case SvgSwitch svgSwitch:
                    {
                        DrawSwitch(skCanvas, skSize, svgSwitch);
                    }
                    break;
                case SvgUse svgUse:
                    {
                        DrawUse(skCanvas, skSize, svgUse);
                    }
                    break;
                case SvgForeignObject svgForeignObject:
                    {
                        DrawForeignObject(skCanvas, skSize, svgForeignObject);
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        DrawCircle(skCanvas, skSize, svgCircle);
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        DrawEllipse(skCanvas, skSize, svgEllipse);
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        DrawRectangle(skCanvas, skSize, svgRectangle);
                    }
                    break;
                case SvgMarker svgMarker:
                    {
                        DrawMarker(skCanvas, skSize, svgMarker);
                    }
                    break;
                case SvgGlyph svgGlyph:
                    {
                        DrawGlyph(skCanvas, skSize, svgGlyph);
                    }
                    break;
                case SvgGroup svgGroup:
                    {
                        DrawGroup(skCanvas, skSize, svgGroup);
                    }
                    break;
                case SvgLine svgLine:
                    {
                        DrawLine(skCanvas, skSize, svgLine);
                    }
                    break;
                case SvgPath svgPath:
                    {
                        DrawPath(skCanvas, skSize, svgPath);
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        DrawPolyline(skCanvas, skSize, svgPolyline);
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        DrawPolygon(skCanvas, skSize, svgPolygon);
                    }
                    break;
                case SvgText svgText:
                    {
                        DrawText(skCanvas, skSize, svgText);
                    }
                    break;
                case SvgTextPath svgTextPath:
                    {
                        DrawTextPath(skCanvas, skSize, svgTextPath);
                    }
                    break;
                case SvgTextRef svgTextRef:
                    {
                        DrawTextRef(skCanvas, skSize, svgTextRef);
                    }
                    break;
                case SvgTextSpan svgTextSpan:
                    {
                        DrawTextSpan(skCanvas, skSize, svgTextSpan);
                    }
                    break;
                default:
                    break;
            }
        }

        public void DrawElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection)
        {
            foreach (var svgElement in svgElementCollection)
            {
                DrawElement(canvas, skSize, svgElement);
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
