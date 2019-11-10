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
    public class SKSvgRenderer : ISvgRenderer
    {
        private readonly SKSize _skSize;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public SKSvgRenderer(SKSize skSize)
        {
            _skSize = skSize;
            _disposable = new CompositeDisposable();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }

        private void Draw(object canvas, SvgElement svgElement)
        {
            // HACK: Normally 'SvgElement' object itself would call appropriate 'Draw' on current render.
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    DrawFragment(canvas, svgFragment);
                    break;
                case SvgImage svgImage:
                    DrawImage(canvas, svgImage);
                    break;
                case SvgSwitch svgSwitch:
                    DrawSwitch(canvas, svgSwitch);
                    break;
                case SvgUse svgUse:
                    DrawUse(canvas, svgUse);
                    break;
                case SvgForeignObject svgForeignObject:
                    DrawForeignObject(canvas, svgForeignObject);
                    break;
                case SvgCircle svgCircle:
                    DrawCircle(canvas, svgCircle);
                    break;
                case SvgEllipse svgEllipse:
                    DrawEllipse(canvas, svgEllipse);
                    break;
                case SvgRectangle svgRectangle:
                    DrawRectangle(canvas, svgRectangle);
                    break;
                case SvgMarker svgMarker:
                    DrawMarker(canvas, svgMarker);
                    break;
                case SvgGlyph svgGlyph:
                    DrawGlyph(canvas, svgGlyph);
                    break;
                case SvgGroup svgGroup:
                    DrawGroup(canvas, svgGroup);
                    break;
                case SvgLine svgLine:
                    DrawLine(canvas, svgLine);
                    break;
                case SvgPath svgPath:
                    DrawPath(canvas, svgPath);
                    break;
                case SvgPolyline svgPolyline:
                    DrawPolyline(canvas, svgPolyline);
                    break;
                case SvgPolygon svgPolygon:
                    DrawPolygon(canvas, svgPolygon);
                    break;
                case SvgText svgText:
                    DrawText(canvas, svgText);
                    break;
                case SvgTextPath svgTextPath:
                    DrawTextPath(canvas, svgTextPath);
                    break;
                case SvgTextRef svgTextRef:
                    DrawTextRef(canvas, svgTextRef);
                    break;
                case SvgTextSpan svgTextSpan:
                    DrawTextSpan(canvas, svgTextSpan);
                    break;
                default:
                    break;
            }
        }

        public void DrawFragment(object canvas, SvgFragment svgFragment)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float x = svgFragment.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            float y = svgFragment.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            float width = svgFragment.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            float height = svgFragment.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            SKRect bounds = SKRect.Create(x, y, width, height);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgFragment.Transforms);
            var viewBoxMatrix = SkiaUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, width, height);
            SKMatrix.Concat(ref matrix, ref matrix, ref viewBoxMatrix);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgFragment, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            foreach (var svgElement in svgFragment.Children)
            {
                Draw(canvas, svgElement);
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        public void DrawImage(object canvas, SvgImage svgImage)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgImage.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgImage, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgImage, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawSwitch(object canvas, SvgSwitch svgSwitch)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgSwitch.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgSwitch, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgSwitch, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawSymbol(object canvas, SvgSymbol svgSymbol)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float x = 0f;
            float y = 0f;
            float width = svgSymbol.ViewBox.Width;
            float height = svgSymbol.ViewBox.Height;

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFrom(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgSymbol);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFrom(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(null, UnitRenderingType.Vertical, svgSymbol);
                }
            }

            SKRect bounds = SKRect.Create(x, y, width, height);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgSymbol.Transforms);
            var viewBoxMatrix = SkiaUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            SKMatrix.Concat(ref matrix, ref matrix, ref viewBoxMatrix);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgSymbol, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgSymbol, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            foreach (var svgElement in svgSymbol.Children)
            {
                Draw(canvas, svgElement);
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

        public void DrawUse(object canvas, SvgUse svgUse)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            var svgVisualElement = SkiaUtil.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement == null || SkiaUtil.HasRecursiveReference(svgUse))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgUse.Transforms);

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
                        if (viewBox != SvgViewBox.Empty && Math.Abs(ew - viewBox.Width) > float.Epsilon && Math.Abs(eh - viewBox.Height) > float.Epsilon)
                        {
                            var sw = ew / viewBox.Width;
                            var sh = eh / viewBox.Height;

                            var skMatrixTranslateSWSH = SKMatrix.MakeTranslation(sw, sh);
                            SKMatrix.Concat(ref matrix, ref matrix, ref skMatrixTranslateSWSH);
                        }
                    }
                }
            }

            var originalParent = svgUse.Parent;
            var useParent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, svgUse);
            }

            svgVisualElement.InvalidateChildPaths();

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgUse, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgUse, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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
                DrawSymbol(canvas, svgSymbol);
            }
            else
            {
                Draw(skCanvas, svgVisualElement);
            }

            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, originalParent);
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

        public void DrawForeignObject(object canvas, SvgForeignObject svgForeignObject)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgForeignObject.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgForeignObject, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgForeignObject, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawCircle(object canvas, SvgCircle svgCircle)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            float cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            float radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);
            SKRect bounds = SKRect.Create(cx - radius, cy - radius, radius + radius, radius + radius);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgCircle, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgCircle, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (svgCircle.Fill != null)
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, _skSize, bounds, _disposable);
                skCanvas.DrawCircle(cx, cy, radius, skPaintFill);
            }

            if (svgCircle.Stroke != null)
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, _skSize, bounds, _disposable);
                skCanvas.DrawCircle(cx, cy, radius, skPaintStroke);
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

        public void DrawEllipse(object canvas, SvgEllipse svgEllipse)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float cx = svgEllipse.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgEllipse);
            float cy = svgEllipse.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgEllipse);
            float rx = svgEllipse.RadiusX.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            float ry = svgEllipse.RadiusY.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            SKRect bounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgEllipse, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgEllipse, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (svgEllipse.Fill != null)
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, _skSize, bounds, _disposable);
                skCanvas.DrawOval(cx, cy, rx, ry, skPaintFill);
            }

            if (svgEllipse.Stroke != null)
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, _skSize, bounds, _disposable);
                skCanvas.DrawOval(cx, cy, rx, ry, skPaintStroke);
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

        public void DrawRectangle(object canvas, SvgRectangle svgRectangle)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float x = svgRectangle.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float y = svgRectangle.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float width = svgRectangle.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float height = svgRectangle.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float rx = svgRectangle.CornerRadiusX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float ry = svgRectangle.CornerRadiusY.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            bool isRound = rx > 0f && ry > 0f;
            SKRect bounds = SKRect.Create(x, y, width, height);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgRectangle, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgRectangle, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (svgRectangle.Fill != null)
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, _skSize, bounds, _disposable);
                if (isRound)
                {
                    skCanvas.DrawRoundRect(x, y, width, height, rx, ry, skPaintFill);
                }
                else
                {
                    skCanvas.DrawRect(x, y, width, height, skPaintFill);
                }
            }

            if (svgRectangle.Stroke != null)
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, _skSize, bounds, _disposable);
                if (isRound)
                {
                    skCanvas.DrawRoundRect(bounds, rx, ry, skPaintStroke);
                }
                else
                {
                    skCanvas.DrawRect(bounds, skPaintStroke);
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

        public void DrawMarker(object canvas, SvgMarker svgMarker)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgMarker.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgMarker, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgMarker, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawGlyph(object canvas, SvgGlyph svgGlyph)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgGlyph.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgGlyph, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgGlyph, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawGroup(object canvas, SvgGroup svgGroup)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgGroup, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgGroup, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            foreach (var svgElement in svgGroup.Children)
            {
                Draw(canvas, svgElement);
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

        public void DrawLine(object canvas, SvgLine svgLine)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            float x0 = svgLine.StartX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            float y0 = svgLine.StartY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);
            float x1 = svgLine.EndX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgLine);
            float y1 = svgLine.EndY.ToDeviceValue(null, UnitRenderingType.Vertical, svgLine);
            float x = Math.Min(x0, x1);
            float y = Math.Min(y0, y1);
            float width = Math.Abs(x0 - x1);
            float height = Math.Abs(y0 - y1);
            SKRect bounds = SKRect.Create(x, y, width, height);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgLine, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgLine, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (svgLine.Stroke != null)
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgLine, _skSize, bounds, _disposable);
                skCanvas.DrawLine(x0, y0, x1, y1, skPaint);
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

        public void DrawPath(object canvas, SvgPath svgPath)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPath, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPath, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPath.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPath, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPath.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPath, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
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

        public void DrawPolyline(object canvas, SvgPolyline svgPolyline)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolyline, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolyline, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPolyline.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPolyline.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
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

        public void DrawPolygon(object canvas, SvgPolygon svgPolygon)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolygon, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolygon, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (svgPolygon.Fill != null)
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (svgPolygon.Stroke != null)
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
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

        public void DrawText(object canvas, SvgText svgText)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgText.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgText, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgText, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawTextPath(object canvas, SvgTextPath svgTextPath)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgTextPath.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgTextPath, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgTextPath, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawTextRef(object canvas, SvgTextRef svgTextRef)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgTextRef.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgTextRef, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgTextRef, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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

        public void DrawTextSpan(object canvas, SvgTextSpan svgTextSpan)
        {
            if (!(canvas is SKCanvas skCanvas))
            {
                return;
            }

            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgTextSpan.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgTextSpan, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgTextSpan, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

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
    }
}
