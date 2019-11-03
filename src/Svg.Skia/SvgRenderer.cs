// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System.Reflection;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    internal static class SvgRenderer
    {
        internal static void DrawSvgFragment(SKCanvas skCanvas, SKSize skSize, SvgFragment svgFragment, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var fragment = new Fragment(svgFragment);

            SvgHelper.SetOpacity(skCanvas, svgFragment, disposable);
            SvgHelper.SetTransform(skCanvas, fragment.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgFragment.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgSymbol(SKCanvas skCanvas, SKSize skSize, SvgSymbol svgSymbol, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var symbol = new Symbol(svgSymbol);

            SvgHelper.SetOpacity(skCanvas, svgSymbol, disposable);
            SvgHelper.SetTransform(skCanvas, symbol.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgSymbol.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgImage(SKCanvas skCanvas, SKSize skSize, SvgImage svgImage, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var image = new Image(svgImage);

            SvgHelper.SetOpacity(skCanvas, svgImage, disposable);
            SvgHelper.SetTransform(skCanvas, image.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgSwitch(SKCanvas skCanvas, SKSize skSize, SvgSwitch svgSwitch, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var @switch = new Switch(svgSwitch);

            SvgHelper.SetOpacity(skCanvas, svgSwitch, disposable);
            SvgHelper.SetTransform(skCanvas, @switch.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgUse(SKCanvas skCanvas, SKSize skSize, SvgUse svgUse, CompositeDisposable disposable)
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

                SvgHelper.SetOpacity(skCanvas, svgUse, disposable);
                SvgHelper.SetFilter(skCanvas, svgUse, disposable);
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
                    DrawSvgSymbol(skCanvas, skSize, svgSymbol, disposable);
                }
                else
                {
                    DrawSvgElement(skCanvas, skSize, svgVisualElement, disposable);
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

        internal static void DrawSvgForeignObject(SKCanvas skCanvas, SKSize skSize, SvgForeignObject svgForeignObject, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var foreignObject = new ForeignObject(svgForeignObject);

            SvgHelper.SetOpacity(skCanvas, svgForeignObject, disposable);
            SvgHelper.SetTransform(skCanvas, foreignObject.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgCircle(SKCanvas skCanvas, SKSize skSize, SvgCircle svgCircle, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var circle = new Circle(svgCircle);

            SvgHelper.SetOpacity(skCanvas, svgCircle, disposable);
            SvgHelper.SetTransform(skCanvas, circle.matrix);

            if (svgCircle.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgCircle, skSize, circle.bounds, disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            if (svgCircle.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgCircle, skSize, circle.bounds, disposable))
                {
                    skCanvas.DrawCircle(circle.cx, circle.cy, circle.radius, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgEllipse(SKCanvas skCanvas, SKSize skSize, SvgEllipse svgEllipse, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var ellipse = new Ellipse(svgEllipse);

            SvgHelper.SetOpacity(skCanvas, svgEllipse, disposable);
            SvgHelper.SetTransform(skCanvas, ellipse.matrix);

            if (svgEllipse.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgEllipse, skSize, ellipse.bounds, disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            if (svgEllipse.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgEllipse, skSize, ellipse.bounds, disposable))
                {
                    skCanvas.DrawOval(ellipse.cx, ellipse.cy, ellipse.rx, ellipse.ry, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgRectangle(SKCanvas skCanvas, SKSize skSize, SvgRectangle svgRectangle, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var rectangle = new Rectangle(svgRectangle);

            SvgHelper.SetOpacity(skCanvas, svgRectangle, disposable);
            SvgHelper.SetTransform(skCanvas, rectangle.matrix);

            if (svgRectangle.Fill != null)
            {
                using (var skPaint = SvgHelper.GetFillSKPaint(svgRectangle, skSize, rectangle.bounds, disposable))
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
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgRectangle, skSize, rectangle.bounds, disposable))
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

        internal static void DrawSvgMarker(SKCanvas skCanvas, SKSize skSize, SvgMarker svgMarker, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var marker = new Marker(svgMarker);

            SvgHelper.SetOpacity(skCanvas, svgMarker, disposable);
            SvgHelper.SetTransform(skCanvas, marker.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgGlyph(SKCanvas skCanvas, SKSize skSize, SvgGlyph svgGlyph, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var glyph = new Glyph(svgGlyph);

            SvgHelper.SetOpacity(skCanvas, svgGlyph, disposable);
            SvgHelper.SetTransform(skCanvas, glyph.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgGroup(SKCanvas skCanvas, SKSize skSize, SvgGroup svgGroup, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var group = new Group(svgGroup);

            SvgHelper.SetOpacity(skCanvas, svgGroup, disposable);
            SvgHelper.SetFilter(skCanvas, svgGroup, disposable);
            SvgHelper.SetTransform(skCanvas, group.matrix);

            DrawSvgElementCollection(skCanvas, skSize, svgGroup.Children, disposable);

            skCanvas.Restore();
        }

        internal static void DrawSvgLine(SKCanvas skCanvas, SKSize skSize, SvgLine svgLine, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var line = new Line(svgLine);

            SvgHelper.SetOpacity(skCanvas, svgLine, disposable);
            SvgHelper.SetTransform(skCanvas, line.matrix);

            if (svgLine.Stroke != null)
            {
                using (var skPaint = SvgHelper.GetStrokeSKPaint(svgLine, skSize, line.bounds, disposable))
                {
                    skCanvas.DrawLine(line.x0, line.y0, line.x1, line.y1, skPaint);
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPath(SKCanvas skCanvas, SKSize skSize, SvgPath svgPath, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var path = new Path(svgPath);

            SvgHelper.SetOpacity(skCanvas, svgPath, disposable);
            SvgHelper.SetTransform(skCanvas, path.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPath.PathData, svgPath.FillRule))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPath.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPath, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPath.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPath, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPolyline(SKCanvas skCanvas, SKSize skSize, SvgPolyline svgPolyline, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var polyline = new Polyline(svgPolyline);

            SvgHelper.SetOpacity(skCanvas, svgPolyline, disposable);
            SvgHelper.SetTransform(skCanvas, polyline.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolyline.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPolyline, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolyline.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPolyline, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgPolygon(SKCanvas skCanvas, SKSize skSize, SvgPolygon svgPolygon, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var polygon = new Polygon(svgPolygon);

            SvgHelper.SetOpacity(skCanvas, svgPolygon, disposable);
            SvgHelper.SetTransform(skCanvas, polygon.matrix);

            using (var skPath = SvgHelper.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true))
            {
                if (skPath != null && !skPath.IsEmpty)
                {
                    var skBounds = skPath.Bounds;

                    if (svgPolygon.Fill != null)
                    {
                        using (var skPaint = SvgHelper.GetFillSKPaint(svgPolygon, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }

                    if (svgPolygon.Stroke != null)
                    {
                        using (var skPaint = SvgHelper.GetStrokeSKPaint(svgPolygon, skSize, skBounds, disposable))
                        {
                            skCanvas.DrawPath(skPath, skPaint);
                        }
                    }
                }
            }

            skCanvas.Restore();
        }

        internal static void DrawSvgText(SKCanvas skCanvas, SKSize skSize, SvgText svgText, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var text = new Text(svgText);

            SvgHelper.SetOpacity(skCanvas, svgText, disposable);
            SvgHelper.SetTransform(skCanvas, text.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextPath(SKCanvas skCanvas, SKSize skSize, SvgTextPath svgTextPath, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textPath = new TextPath(svgTextPath);

            SvgHelper.SetOpacity(skCanvas, svgTextPath, disposable);
            SvgHelper.SetTransform(skCanvas, textPath.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextRef(SKCanvas skCanvas, SKSize skSize, SvgTextRef svgTextRef, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textRef = new TextRef(svgTextRef);

            SvgHelper.SetOpacity(skCanvas, svgTextRef, disposable);
            SvgHelper.SetTransform(skCanvas, textRef.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgTextSpan(SKCanvas skCanvas, SKSize skSize, SvgTextSpan svgTextSpan, CompositeDisposable disposable)
        {
            skCanvas.Save();

            var textSpan = new TextSpan(svgTextSpan);

            SvgHelper.SetOpacity(skCanvas, svgTextSpan, disposable);
            SvgHelper.SetTransform(skCanvas, textSpan.matrix);

            // TODO:

            skCanvas.Restore();
        }

        internal static void DrawSvgElement(SKCanvas skCanvas, SKSize skSize, SvgElement svgElement, CompositeDisposable disposable)
        {
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    {
                        DrawSvgFragment(skCanvas, skSize, svgFragment, disposable);
                    }
                    break;
                case SvgImage svgImage:
                    {
                        DrawSvgImage(skCanvas, skSize, svgImage, disposable);
                    }
                    break;
                case SvgSwitch svgSwitch:
                    {
                        DrawSvgSwitch(skCanvas, skSize, svgSwitch, disposable);
                    }
                    break;
                case SvgUse svgUse:
                    {
                        DrawSvgUse(skCanvas, skSize, svgUse, disposable);
                    }
                    break;
                case SvgForeignObject svgForeignObject:
                    {
                        DrawSvgForeignObject(skCanvas, skSize, svgForeignObject, disposable);
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        DrawSvgCircle(skCanvas, skSize, svgCircle, disposable);
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        DrawSvgEllipse(skCanvas, skSize, svgEllipse, disposable);
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        DrawSvgRectangle(skCanvas, skSize, svgRectangle, disposable);
                    }
                    break;
                case SvgMarker svgMarker:
                    {
                        DrawSvgMarker(skCanvas, skSize, svgMarker, disposable);
                    }
                    break;
                case SvgGlyph svgGlyph:
                    {
                        DrawSvgGlyph(skCanvas, skSize, svgGlyph, disposable);
                    }
                    break;
                case SvgGroup svgGroup:
                    {
                        DrawSvgGroup(skCanvas, skSize, svgGroup, disposable);
                    }
                    break;
                case SvgLine svgLine:
                    {
                        DrawSvgLine(skCanvas, skSize, svgLine, disposable);
                    }
                    break;
                case SvgPath svgPath:
                    {
                        DrawSvgPath(skCanvas, skSize, svgPath, disposable);
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        DrawSvgPolyline(skCanvas, skSize, svgPolyline, disposable);
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        DrawSvgPolygon(skCanvas, skSize, svgPolygon, disposable);
                    }
                    break;
                case SvgText svgText:
                    {
                        DrawSvgText(skCanvas, skSize, svgText, disposable);
                    }
                    break;
                case SvgTextPath svgTextPath:
                    {
                        DrawSvgTextPath(skCanvas, skSize, svgTextPath, disposable);
                    }
                    break;
                case SvgTextRef svgTextRef:
                    {
                        DrawSvgTextRef(skCanvas, skSize, svgTextRef, disposable);
                    }
                    break;
                case SvgTextSpan svgTextSpan:
                    {
                        DrawSvgTextSpan(skCanvas, skSize, svgTextSpan, disposable);
                    }
                    break;
                default:
                    break;
            }
        }

        internal static void DrawSvgElementCollection(SKCanvas canvas, SKSize skSize, SvgElementCollection svgElementCollection, CompositeDisposable disposable)
        {
            foreach (var svgElement in svgElementCollection)
            {
                DrawSvgElement(canvas, skSize, svgElement, disposable);
            }
        }
    }
}
