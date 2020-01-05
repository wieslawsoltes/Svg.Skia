// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Svg.Skia
{
    internal class TextDrawable : Drawable
    {
        // TODO: Implement drawable.

        private SvgText _svgText;
        private SKRect _skOwnerBounds;

        public TextDrawable(SvgText svgText, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _svgText = svgText;
            _skOwnerBounds = skOwnerBounds;
            IgnoreDisplay = ignoreDisplay;
        }

        internal virtual IEnumerable<ISvgNode> GetContentNodes(SvgTextBase svgTextBase)
        {
            return svgTextBase.Nodes == null || svgTextBase.Nodes.Count < 1 ?
                svgTextBase.Children.OfType<ISvgNode>().Where(o => !(o is ISvgDescriptiveElement)) :
                svgTextBase.Nodes;
        }

        private static readonly Regex MultipleSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        protected string PrepareText(SvgTextBase svgTextBase, string value)
        {
            value = ApplyTransformation(svgTextBase, value);
            value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
            return svgTextBase.SpaceHandling == XmlSpaceHandling.preserve ? value : MultipleSpaces.Replace(value.Trim(), " ");
        }

        private string ApplyTransformation(SvgTextBase svgTextBase, string value)
        {
            switch (svgTextBase.TextTransformation)
            {
                case SvgTextTransformation.Capitalize:
                    return value.ToUpper();

                case SvgTextTransformation.Uppercase:
                    return value.ToUpper();

                case SvgTextTransformation.Lowercase:
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
            }

            return value;
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, SKRect skOwnerBounds, bool ignoreDisplay, SKCanvas _skCanvas)
        {
            if (!CanDraw(svgTextPath, ignoreDisplay))
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SkiaUtil.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath == null)
            {
                return;
            }

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SkiaUtil.GetSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextPath.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextPath, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextPath);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextPath, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextPath, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SkiaUtil.IsValidFill(svgTextPath);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SkiaUtil.IsValidFill(svgTextPath))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextPath, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                        _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextPath, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                        _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                    }
                }
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, SKRect skOwnerBounds, bool ignoreDisplay, SKCanvas _skCanvas)
        {
            if (!CanDraw(svgTextRef, ignoreDisplay))
            {
                return;
            }

            if (SkiaUtil.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SkiaUtil.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText == null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextRef.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextRef, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextRef);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextRef, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextRef, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, skOwnerBounds, _skCanvas);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, SKRect skOwnerBounds, bool ignoreDisplay, SKCanvas _skCanvas)
        {
            if (!CanDraw(svgTextSpan, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextSpan.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgTextSpan, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgTextSpan);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgTextSpan, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgTextSpan, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, svgTextSpan.Text, skOwnerBounds, _skCanvas);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, SKCanvas _skCanvas)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SkiaUtil.IsValidFill(svgTextBase))
            {
                var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }

            if (SkiaUtil.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                _skCanvas.DrawText(text, x, y, skPaint);
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, SKRect skOwnerBounds, SKCanvas _skCanvas)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SkiaUtil.IsValidFill(svgTextBase);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgTextBase, skOwnerBounds);

            if ((isValidFill || isValidStroke) && text != null && !string.IsNullOrEmpty(text))
            {
                var xCount = svgTextBase.X.Count;
                var yCount = svgTextBase.Y.Count;
                var dxCount = svgTextBase.Dx.Count;
                var dyCount = svgTextBase.Dy.Count;

                if (xCount >= 1 && yCount >= 1 && xCount == yCount && xCount == text.Length)
                {
                    // TODO: Fix text position rendering.
                    var points = new SKPoint[xCount];

                    for (int i = 0; i < xCount; i++)
                    {
                        float x = svgTextBase.X[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skOwnerBounds);
                        float y = svgTextBase.Y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skOwnerBounds);
                        points[i] = new SKPoint(x, y);
                    }

                    // TODO: Calculate correct bounds.
                    var skBounds = skOwnerBounds;

                    if (SkiaUtil.IsValidFill(svgTextBase))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        _skCanvas.DrawPositionedText(text, points, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgTextBase, skBounds))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        _skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }
                else
                {
                    float x = 0f;
                    float y = 0f;
                    float dx = 0f;
                    float dy = 0f;

                    if (xCount >= 1)
                    {
                        x = svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skOwnerBounds);
                    }

                    if (yCount >= 1)
                    {
                        y = svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skOwnerBounds);
                    }

                    if (dxCount >= 1)
                    {
                        dx = svgTextBase.Dx[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skOwnerBounds);
                    }

                    if (dyCount >= 1)
                    {
                        dy = svgTextBase.Dy[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skOwnerBounds);
                    }

                    DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, _skCanvas);
                }
            }
        }

        public void DrawText(SvgText svgText, SKRect skOwnerBounds, bool ignoreDisplay, SKCanvas _skCanvas)
        {
            if (!CanDraw(svgText, ignoreDisplay))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgText.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgText, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgText);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgText, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SkiaUtil.GetFilterSKPaint(svgText, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            var nodes = GetContentNodes(svgText);

            foreach (var node in nodes)
            {
                var textNode = node as SvgTextBase;

                if (textNode == null)
                {
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var text = PrepareText(svgText, node.Content);
                        DrawTextBase(svgText, text, skOwnerBounds, _skCanvas);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, skOwnerBounds, ignoreDisplay, _skCanvas);
                            break;
                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, skOwnerBounds, ignoreDisplay, _skCanvas);
                            break;
                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, skOwnerBounds, ignoreDisplay, _skCanvas);
                            break;
                        default:
                            break;
                    }
                }
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO:
            DrawText(_svgText, _skOwnerBounds, IgnoreDisplay, canvas);
        }
    }
}
