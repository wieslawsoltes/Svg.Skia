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
    public class TextDrawable : Drawable
    {
        // TODO: Implement drawable.

        private readonly SvgText _svgText;
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

        private static readonly Regex s_multipleSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        protected string PrepareText(SvgTextBase svgTextBase, string value)
        {
            value = ApplyTransformation(svgTextBase, value);
            value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
            return svgTextBase.SpaceHandling == XmlSpaceHandling.preserve ? value : s_multipleSpaces.Replace(value.Trim(), " ");
        }

        private string ApplyTransformation(SvgTextBase svgTextBase, string value)
        {
            return svgTextBase.TextTransformation switch
            {
                SvgTextTransformation.Capitalize => value.ToUpper(),
                SvgTextTransformation.Uppercase => value.ToUpper(),
                SvgTextTransformation.Lowercase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
                _ => value,
            };
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, SKRect skOwnerBounds, bool ignoreDisplay, SKCanvas _skCanvas)
        {
            if (!CanDraw(svgTextPath, ignoreDisplay))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath == null)
            {
                return;
            }

            var skPath = SKPathUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SKMatrixUtil.GetSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SKMatrixUtil.GetSKMatrix(svgTextPath.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgTextPath, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SKPaintUtil.IsAntialias(svgTextPath);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgTextPath, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SKPaintUtil.GetFilterSKPaint(svgTextPath, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SKPaintUtil.IsValidFill(svgTextPath);
            bool isValidStroke = SKPaintUtil.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SKPaintUtil.IsValidFill(svgTextPath))
                    {
                        var skPaint = SKPaintUtil.GetFillSKPaint(svgTextPath, skBounds, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint); 
                        }
                    }

                    if (SKPaintUtil.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SKPaintUtil.GetStrokeSKPaint(svgTextPath, skBounds, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintUtil.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            _skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint); 
                        }
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

            if (SvgExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText == null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            _skCanvas.Save();

            var skMatrix = SKMatrixUtil.GetSKMatrix(svgTextRef.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgTextRef, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SKPaintUtil.IsAntialias(svgTextRef);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgTextRef, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SKPaintUtil.GetFilterSKPaint(svgTextRef, _disposable);
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

            var skMatrix = SKMatrixUtil.GetSKMatrix(svgTextSpan.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgTextSpan, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SKPaintUtil.IsAntialias(svgTextSpan);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgTextSpan, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SKPaintUtil.GetFilterSKPaint(svgTextSpan, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, skOwnerBounds, _skCanvas);
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

            if (SKPaintUtil.IsValidFill(svgTextBase))
            {
                var skPaint = SKPaintUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                if (skPaint != null)
                {
                    SKPaintUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    _skCanvas.DrawText(text, x, y, skPaint); 
                }
            }

            if (SKPaintUtil.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SKPaintUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                if (skPaint != null)
                {
                    SKPaintUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    _skCanvas.DrawText(text, x, y, skPaint); 
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, SKRect skOwnerBounds, SKCanvas _skCanvas)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SKPaintUtil.IsValidFill(svgTextBase);
            bool isValidStroke = SKPaintUtil.IsValidStroke(svgTextBase, skOwnerBounds);

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

                    if (SKPaintUtil.IsValidFill(svgTextBase))
                    {
                        var skPaint = SKPaintUtil.GetFillSKPaint(svgTextBase, skBounds, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                            _skCanvas.DrawPositionedText(text, points, skPaint); 
                        }
                    }

                    if (SKPaintUtil.IsValidStroke(svgTextBase, skBounds))
                    {
                        var skPaint = SKPaintUtil.GetStrokeSKPaint(svgTextBase, skBounds, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintUtil.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                            _skCanvas.DrawPositionedText(text, points, skPaint); 
                        }
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

            var skMatrix = SKMatrixUtil.GetSKMatrix(svgText.Transforms);

            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgText, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SKPaintUtil.IsAntialias(svgText);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var skPaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgText, _disposable);
            if (skPaintOpacity != null)
            {
                _skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SKPaintUtil.GetFilterSKPaint(svgText, _disposable);
            if (skPaintFilter != null)
            {
                _skCanvas.SaveLayer(skPaintFilter);
            }

            var nodes = GetContentNodes(svgText);

            foreach (var node in nodes)
            {
                if (!(node is SvgTextBase textNode))
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
