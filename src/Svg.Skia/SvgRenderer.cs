// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SvgRenderer : IDisposable
    {
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public void DrawSvgFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment)
        {
            skCanvas.Save();

            var fragment = new Fragment(svgFragment);

            SvgHelper.SetOpacity(skCanvas, svgFragment, _disposable);
            SvgHelper.SetTransform(skCanvas, fragment.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgFragment.Children);

            skCanvas.Restore();
        }

        public void DrawSvgSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol)
        {
            skCanvas.Save();

            var symbol = new Symbol(svgSymbol);

            SvgHelper.SetOpacity(skCanvas, svgSymbol, _disposable);
            SvgHelper.SetTransform(skCanvas, symbol.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgSymbol.Children);

            skCanvas.Restore();
        }

        public void DrawSvgImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage)
        {
            skCanvas.Save();

            var image = new Image(svgImage);

            SvgHelper.SetOpacity(skCanvas, svgImage, _disposable);
            SvgHelper.SetTransform(skCanvas, image.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch)
        {
            skCanvas.Save();

            var @switch = new Switch(svgSwitch);

            SvgHelper.SetOpacity(skCanvas, svgSwitch, _disposable);
            SvgHelper.SetTransform(skCanvas, @switch.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse)
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

                SvgHelper.SetOpacity(skCanvas, svgUse, _disposable);
                SvgHelper.SetFilter(skCanvas, svgUse, _disposable);
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
                    DrawSvgSymbol(skCanvas, skSize, svgSymbol);
                }
                else
                {
                    DrawSvgElement(skCanvas, skSize, svgVisualElement);
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

                skCanvas.Restore();
            }
        }

        public void DrawSvgForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject)
        {
            skCanvas.Save();

            var foreignObject = new ForeignObject(svgForeignObject);

            SvgHelper.SetOpacity(skCanvas, svgForeignObject, _disposable);
            SvgHelper.SetTransform(skCanvas, foreignObject.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle)
        {
            skCanvas.Save();

            var circle = new Circle(svgCircle);

            SvgHelper.SetOpacity(skCanvas, svgCircle, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse)
        {
            skCanvas.Save();

            var ellipse = new Ellipse(svgEllipse);

            SvgHelper.SetOpacity(skCanvas, svgEllipse, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle)
        {
            skCanvas.Save();

            var rectangle = new Rectangle(svgRectangle);

            SvgHelper.SetOpacity(skCanvas, svgRectangle, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker)
        {
            skCanvas.Save();

            var marker = new Marker(svgMarker);

            SvgHelper.SetOpacity(skCanvas, svgMarker, _disposable);
            SvgHelper.SetTransform(skCanvas, marker.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph)
        {
            skCanvas.Save();

            var glyph = new Glyph(svgGlyph);

            SvgHelper.SetOpacity(skCanvas, svgGlyph, _disposable);
            SvgHelper.SetTransform(skCanvas, glyph.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup)
        {
            skCanvas.Save();

            var group = new Group(svgGroup);

            SvgHelper.SetOpacity(skCanvas, svgGroup, _disposable);
            SvgHelper.SetFilter(skCanvas, svgGroup, _disposable);
            SvgHelper.SetTransform(skCanvas, group.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgGroup.Children);

            skCanvas.Restore();
        }

        public void DrawSvgLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine)
        {
            skCanvas.Save();

            var line = new Line(svgLine);

            SvgHelper.SetOpacity(skCanvas, svgLine, _disposable);
            SvgHelper.SetTransform(skCanvas, line.matrix);

            if (svgLine.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgLine, skSize, line.bounds, _disposable))
                {
                    skCanvas.DrawLine(line.x0, line.y0, line.x1, line.y1, skPaint);
                }
            }

            skCanvas.Restore();
        }

        public void DrawSvgPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath)
        {
            skCanvas.Save();

            var path = new Path(svgPath);

            SvgHelper.SetOpacity(skCanvas, svgPath, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline)
        {
            skCanvas.Save();

            var polyline = new Polyline(svgPolyline);

            SvgHelper.SetOpacity(skCanvas, svgPolyline, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon)
        {
            skCanvas.Save();

            var polygon = new Polygon(svgPolygon);

            SvgHelper.SetOpacity(skCanvas, svgPolygon, _disposable);
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

            skCanvas.Restore();
        }

        public void DrawSvgText(SKCanvas skCanvas, SKSize skSize, SvgText svgText)
        {
            skCanvas.Save();

            var text = new Text(svgText);

            SvgHelper.SetOpacity(skCanvas, svgText, _disposable);
            SvgHelper.SetTransform(skCanvas, text.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath)
        {
            skCanvas.Save();

            var textPath = new TextPath(svgTextPath);

            SvgHelper.SetOpacity(skCanvas, svgTextPath, _disposable);
            SvgHelper.SetTransform(skCanvas, textPath.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef)
        {
            skCanvas.Save();

            var textRef = new TextRef(svgTextRef);

            SvgHelper.SetOpacity(skCanvas, svgTextRef, _disposable);
            SvgHelper.SetTransform(skCanvas, textRef.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan)
        {
            skCanvas.Save();

            var textSpan = new TextSpan(svgTextSpan);

            SvgHelper.SetOpacity(skCanvas, svgTextSpan, _disposable);
            SvgHelper.SetTransform(skCanvas, textSpan.matrix);

            // TODO:

            skCanvas.Restore();
        }

        public void DrawSvgElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    {
                        DrawSvgFragment(skCanvas, skSize, svgFragment);
                    }
                    break;
                case SvgImage svgImage:
                    {
                        DrawSvgImage(skCanvas, skSize, svgImage);
                    }
                    break;
                case SvgSwitch svgSwitch:
                    {
                        DrawSvgSwitch(skCanvas, skSize, svgSwitch);
                    }
                    break;
                case SvgUse svgUse:
                    {
                        DrawSvgUse(skCanvas, skSize, svgUse);
                    }
                    break;
                case SvgForeignObject svgForeignObject:
                    {
                        DrawSvgForeignObject(skCanvas, skSize, svgForeignObject);
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        DrawSvgCircle(skCanvas, skSize, svgCircle);
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        DrawSvgEllipse(skCanvas, skSize, svgEllipse);
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        DrawSvgRectangle(skCanvas, skSize, svgRectangle);
                    }
                    break;
                case SvgMarker svgMarker:
                    {
                        DrawSvgMarker(skCanvas, skSize, svgMarker);
                    }
                    break;
                case SvgGlyph svgGlyph:
                    {
                        DrawSvgGlyph(skCanvas, skSize, svgGlyph);
                    }
                    break;
                case SvgGroup svgGroup:
                    {
                        DrawSvgGroup(skCanvas, skSize, svgGroup);
                    }
                    break;
                case SvgLine svgLine:
                    {
                        DrawSvgLine(skCanvas, skSize, svgLine);
                    }
                    break;
                case SvgPath svgPath:
                    {
                        DrawSvgPath(skCanvas, skSize, svgPath);
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        DrawSvgPolyline(skCanvas, skSize, svgPolyline);
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        DrawSvgPolygon(skCanvas, skSize, svgPolygon);
                    }
                    break;
                case SvgText svgText:
                    {
                        DrawSvgText(skCanvas, skSize, svgText);
                    }
                    break;
                case SvgTextPath svgTextPath:
                    {
                        DrawSvgTextPath(skCanvas, skSize, svgTextPath);
                    }
                    break;
                case SvgTextRef svgTextRef:
                    {
                        DrawSvgTextRef(skCanvas, skSize, svgTextRef);
                    }
                    break;
                case SvgTextSpan svgTextSpan:
                    {
                        DrawSvgTextSpan(skCanvas, skSize, svgTextSpan);
                    }
                    break;
                default:
                    break;
            }
        }

        public void DrawSvgElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection)
        {
            foreach (var svgElement in svgElementCollection)
            {
                DrawSvgElement(canvas, skSize, svgElement);
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
