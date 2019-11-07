// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SKSvgRenderer : ISKSvgRenderer
    {
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public void DrawFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment)
        {
            var fragment = new Fragment(svgFragment);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgFragment, _disposable);
            SKSvgHelper.SetTransform(skCanvas, fragment.matrix);

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

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgSymbol, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgSymbol, _disposable);
            SKSvgHelper.SetTransform(skCanvas, symbol.matrix);

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
            var image = new Image(svgImage);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgImage, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgImage, _disposable);
            SKSvgHelper.SetTransform(skCanvas, image.matrix);

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
            var @switch = new Switch(svgSwitch);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgSwitch, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgSwitch, _disposable);
            SKSvgHelper.SetTransform(skCanvas, @switch.matrix);

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

                var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgUse, _disposable);
                var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgUse, _disposable);
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

        public void DrawForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject)
        {
            var foreignObject = new ForeignObject(svgForeignObject);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgForeignObject, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgForeignObject, _disposable);
            SKSvgHelper.SetTransform(skCanvas, foreignObject.matrix);

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
            var circle = new Circle(svgCircle);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgCircle, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgCircle, _disposable);
            SKSvgHelper.SetTransform(skCanvas, circle.matrix);

            if (svgCircle.Fill != null)
            {
                using (var skPaint = SKSvgHelper.GetFillSKPaint(svgCircle, skSize, circle.bounds, _disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            if (svgCircle.Stroke != null)
            {
                using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgCircle, skSize, circle.bounds, _disposable))
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
            var ellipse = new Ellipse(svgEllipse);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgEllipse, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgEllipse, _disposable);
            SKSvgHelper.SetTransform(skCanvas, ellipse.matrix);

            if (svgEllipse.Fill != null)
            {
                using (var skPaint = SKSvgHelper.GetFillSKPaint(svgEllipse, skSize, ellipse.bounds, _disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            if (svgEllipse.Stroke != null)
            {
                using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgEllipse, skSize, ellipse.bounds, _disposable))
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

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgRectangle, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgRectangle, _disposable);
            SKSvgHelper.SetTransform(skCanvas, rectangle.matrix);

            if (svgRectangle.Fill != null)
            {
                using (var skPaint = SKSvgHelper.GetFillSKPaint(svgRectangle, skSize, rectangle.bounds, _disposable))
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
                using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgRectangle, skSize, rectangle.bounds, _disposable))
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
            var marker = new Marker(svgMarker);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgMarker, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgMarker, _disposable);
            SKSvgHelper.SetTransform(skCanvas, marker.matrix);

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
            var glyph = new Glyph(svgGlyph);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgGlyph, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgGlyph, _disposable);
            SKSvgHelper.SetTransform(skCanvas, glyph.matrix);

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

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgGroup, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgGroup, _disposable);
            SKSvgHelper.SetTransform(skCanvas, group.matrix);

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

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgLine, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgLine, _disposable);
            SKSvgHelper.SetTransform(skCanvas, line.matrix);

            if (svgLine.Stroke != null)
            {
                using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgLine, skSize, line.bounds, _disposable))
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
            var path = new Path(svgPath);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgPath, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgPath, _disposable);
            SKSvgHelper.SetTransform(skCanvas, path.matrix);

            using (var skPath = SKSvgHelper.ToSKPath(svgPath.PathData, svgPath.FillRule))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPath.Fill != null)
                    {
                        using (var skPaint = SKSvgHelper.GetFillSKPaint(svgPath, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPath.Stroke != null)
                    {
                        using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgPath, skSize, skBounds, _disposable))
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
            var polyline = new Polyline(svgPolyline);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgPolyline, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgPolyline, _disposable);
            SKSvgHelper.SetTransform(skCanvas, polyline.matrix);

            using (var skPath = SKSvgHelper.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolyline.Fill != null)
                    {
                        using (var skPaint = SKSvgHelper.GetFillSKPaint(svgPolyline, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolyline.Stroke != null)
                    {
                        using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgPolyline, skSize, skBounds, _disposable))
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
            var polygon = new Polygon(svgPolygon);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgPolygon, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgPolygon, _disposable);
            SKSvgHelper.SetTransform(skCanvas, polygon.matrix);

            using (var skPath = SKSvgHelper.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolygon.Fill != null)
                    {
                        using (var skPaint = SKSvgHelper.GetFillSKPaint(svgPolygon, skSize, skBounds, _disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolygon.Stroke != null)
                    {
                        using (var skPaint = SKSvgHelper.GetStrokeSKPaint(svgPolygon, skSize, skBounds, _disposable))
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
            var text = new Text(svgText);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgText, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgText, _disposable);
            SKSvgHelper.SetTransform(skCanvas, text.matrix);

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
            var textPath = new TextPath(svgTextPath);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgTextPath, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgTextPath, _disposable);
            SKSvgHelper.SetTransform(skCanvas, textPath.matrix);

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
            var textRef = new TextRef(svgTextRef);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgTextRef, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgTextRef, _disposable);
            SKSvgHelper.SetTransform(skCanvas, textRef.matrix);

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
            var textSpan = new TextSpan(svgTextSpan);

            skCanvas.Save();

            var skPaintOpacity = SKSvgHelper.SetOpacity(skCanvas, svgTextSpan, _disposable);
            var skPaintFilter = SKSvgHelper.SetFilter(skCanvas, svgTextSpan, _disposable);
            SKSvgHelper.SetTransform(skCanvas, textSpan.matrix);

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
