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
        private readonly SKCanvas skCanvas;
        private readonly SKSize _skSize;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public SKSvgRenderer(SKCanvas skCanvas, SKSize skSize)
        {
            this.skCanvas = skCanvas;
            _skSize = skSize;
            _disposable = new CompositeDisposable();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }

        private void Draw(SvgElement svgElement)
        {
            // HACK: Normally 'SvgElement' object itself would call appropriate 'Draw' on current render.
            switch (svgElement)
            {
                case SvgFragment svgFragment:
                    DrawFragment(svgFragment);
                    break;
                case SvgImage svgImage:
                    DrawImage(svgImage);
                    break;
                case SvgSwitch svgSwitch:
                    DrawSwitch(svgSwitch);
                    break;
                case SvgUse svgUse:
                    DrawUse(svgUse);
                    break;
                case SvgForeignObject svgForeignObject:
                    DrawForeignObject(svgForeignObject);
                    break;
                case SvgCircle svgCircle:
                    DrawCircle(svgCircle);
                    break;
                case SvgEllipse svgEllipse:
                    DrawEllipse(svgEllipse);
                    break;
                case SvgRectangle svgRectangle:
                    DrawRectangle(svgRectangle);
                    break;
                case SvgMarker svgMarker:
                    DrawMarker(svgMarker);
                    break;
                case SvgGlyph svgGlyph:
                    DrawGlyph(svgGlyph);
                    break;
                case SvgGroup svgGroup:
                    DrawGroup(svgGroup);
                    break;
                case SvgLine svgLine:
                    DrawLine(svgLine);
                    break;
                case SvgPath svgPath:
                    DrawPath(svgPath);
                    break;
                case SvgPolyline svgPolyline:
                    DrawPolyline(svgPolyline);
                    break;
                case SvgPolygon svgPolygon:
                    DrawPolygon(svgPolygon);
                    break;
                case SvgText svgText:
                    DrawText(svgText);
                    break;
                case SvgTextPath svgTextPath:
                    DrawTextPath(svgTextPath);
                    break;
                case SvgTextRef svgTextRef:
                    DrawTextRef(svgTextRef);
                    break;
                case SvgTextSpan svgTextSpan:
                    DrawTextSpan(svgTextSpan);
                    break;
                default:
                    break;
            }
        }

        public void DrawFragment(SvgFragment svgFragment)
        {
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
                Draw(svgElement);
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        public void DrawImage(SvgImage svgImage)
        {
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

        public void DrawSwitch(SvgSwitch svgSwitch)
        {
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

        public void DrawSymbol(SvgSymbol svgSymbol)
        {
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
                Draw(svgElement);
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

        public void DrawUse(SvgUse svgUse)
        {
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
                DrawSymbol(svgSymbol);
            }
            else
            {
                Draw(svgVisualElement);
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

        public void DrawForeignObject(SvgForeignObject svgForeignObject)
        {
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

        public void DrawCircle(SvgCircle svgCircle)
        {
            float cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            float cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            float radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);
            SKRect bounds = SKRect.Create(cx - radius, cy - radius, radius + radius, radius + radius);
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgCircle, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgCircle, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, _skSize, bounds, _disposable);
                skCanvas.DrawCircle(cx, cy, radius, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgCircle))
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

        public void DrawEllipse(SvgEllipse svgEllipse)
        {
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

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, _skSize, bounds, _disposable);
                skCanvas.DrawOval(cx, cy, rx, ry, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse))
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

        public void DrawRectangle(SvgRectangle svgRectangle)
        {
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

            if (SkiaUtil.IsValidFill(svgRectangle))
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

            if (SkiaUtil.IsValidStroke(svgRectangle))
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

        public void DrawMarker(SvgMarker svgMarker)
        {
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

        public void DrawGlyph(SvgGlyph svgGlyph)
        {
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

        public void DrawGroup(SvgGroup svgGroup)
        {
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgGroup, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgGroup, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            foreach (var svgElement in svgGroup.Children)
            {
                Draw(svgElement);
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

        public void DrawLine(SvgLine svgLine)
        {
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

            if (SkiaUtil.IsValidStroke(svgLine))
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

        public void DrawPath(SvgPath svgPath)
        {
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPath, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPath, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPath))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPath, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPath))
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

        public void DrawPolyline(SvgPolyline svgPolyline)
        {
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolyline, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolyline, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPolyline))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPolyline))
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

        public void DrawPolygon(SvgPolygon svgPolygon)
        {
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgPolygon, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgPolygon, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPolygon))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                    skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPolygon))
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

        public void DrawText(SvgText svgText)
        {
            SKMatrix matrix = SkiaUtil.GetSKMatrix(svgText.Transforms);

            skCanvas.Save();

            var skPaintOpacity = SkiaUtil.SetOpacity(skCanvas, svgText, _disposable);
            var skPaintFilter = SkiaUtil.SetFilter(skCanvas, svgText, _disposable);
            SkiaUtil.SetTransform(skCanvas, matrix);

            // TODO:
            bool isValidFill = SkiaUtil.IsValidFill(svgText);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgText);

            if (isValidFill || isValidStroke)
            {
                var text = svgText.Text;

                if (svgText.X.Count == 1 && svgText.Y.Count == 1 && !string.IsNullOrEmpty(text))
                {
                    // TODO:
                    float x0 = svgText.X[0].ToDeviceValue(null, UnitRenderingType.HorizontalOffset, svgText);
                    float y0 = svgText.Y[0].ToDeviceValue(null, UnitRenderingType.VerticalOffset, svgText);

                    // TODO:
                    var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

                    if (SkiaUtil.IsValidFill(svgText))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgText, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgText, _skSize, skBounds, skPaint, _disposable);
                        skCanvas.DrawText(text, x0, y0, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgText))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgText, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgText, _skSize, skBounds, skPaint, _disposable);
                        skCanvas.DrawText(text, x0, y0, skPaint);
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


        public void DrawTextPath(SvgTextPath svgTextPath)
        {
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

        public void DrawTextRef(SvgTextRef svgTextRef)
        {
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

        public void DrawTextSpan(SvgTextSpan svgTextSpan)
        {
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
