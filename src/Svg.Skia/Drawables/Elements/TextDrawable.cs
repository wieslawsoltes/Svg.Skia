// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public TextDrawable(SvgText svgText, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            _svgText = svgText;
            _skOwnerBounds = skOwnerBounds;
            IgnoreAttributes = ignoreAttributes;
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

        internal void DrawTextPath(SvgTextPath svgTextPath, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes))
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

            var skPath = svgPath.PathData?.ToSKPath(svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SKMatrixExtensions.ToSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            skCanvas.Save();

            var skMatrix = SKMatrixExtensions.ToSKMatrix(svgTextPath.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgTextPath, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null)
            {
                bool antialias = SKPaintExtensions.IsAntialias(svgTextPath);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            // TODO: Add mask support.

            var skPaintOpacity = SKPaintExtensions.GetOpacitySKPaint(svgTextPath, _disposable);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextPath, skBounds, _disposable);
            if (skPaintFilter != null)
            {
                skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SKPaintExtensions.IsValidFill(svgTextPath);
            bool isValidStroke = SKPaintExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SKPaintExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SKPaintExtensions.GetFillSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint); 
                        }
                    }

                    if (SKPaintExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SKPaintExtensions.GetStrokeSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint); 
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

        internal void DrawTextRef(SvgTextRef svgTextRef, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes))
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

            skCanvas.Save();

            var skMatrix = SKMatrixExtensions.ToSKMatrix(svgTextRef.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgTextRef, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null)
            {
                bool antialias = SKPaintExtensions.IsAntialias(svgTextRef);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            // TODO: Add mask support.

            var skPaintOpacity = SKPaintExtensions.GetOpacitySKPaint(svgTextRef, _disposable);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextRef, skBounds, _disposable);
            if (skPaintFilter != null)
            {
                skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, skOwnerBounds, ignoreAttributes, skCanvas);
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

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            skCanvas.Save();

            var skMatrix = SKMatrixExtensions.ToSKMatrix(svgTextSpan.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgTextSpan, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null)
            {
                bool antialias = SKPaintExtensions.IsAntialias(svgTextSpan);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            // TODO: Add mask support.

            var skPaintOpacity = SKPaintExtensions.GetOpacitySKPaint(svgTextSpan, _disposable);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextSpan, skBounds, _disposable);
            if (skPaintFilter != null)
            {
                skCanvas.SaveLayer(skPaintFilter);
            }

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, skOwnerBounds, ignoreAttributes, skCanvas);
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

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SKPaintExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SKPaintExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SKPaintExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint); 
                }
            }

            if (SKPaintExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SKPaintExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SKPaintExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint); 
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SKPaintExtensions.IsValidFill(svgTextBase);
            bool isValidStroke = SKPaintExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

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

                    if (SKPaintExtensions.IsValidFill(svgTextBase))
                    {
                        var skPaint = SKPaintExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                            skCanvas.DrawPositionedText(text, points, skPaint); 
                        }
                    }

                    if (SKPaintExtensions.IsValidStroke(svgTextBase, skBounds))
                    {
                        var skPaint = SKPaintExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SKPaintExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                            skCanvas.DrawPositionedText(text, points, skPaint); 
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

                    DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, ignoreAttributes, skCanvas);
                }
            }
        }

        public void DrawText(SvgText svgText, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            skCanvas.Save();

            var skMatrix = SKMatrixExtensions.ToSKMatrix(svgText.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgText, skBounds, new HashSet<Uri>(), _disposable);
            if (skPathClip != null)
            {
                bool antialias = SKPaintExtensions.IsAntialias(svgText);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            // TODO: Add mask support.

            var skPaintOpacity = SKPaintExtensions.GetOpacitySKPaint(svgText, _disposable);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            var skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgText, skBounds, _disposable);
            if (skPaintFilter != null)
            {
                skCanvas.SaveLayer(skPaintFilter);
            }

            var nodes = GetContentNodes(svgText);

            foreach (var node in nodes)
            {
                if (!(node is SvgTextBase textNode))
                {
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var text = PrepareText(svgText, node.Content);
                        DrawTextBase(svgText, text, skOwnerBounds, ignoreAttributes, skCanvas);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        default:
                            break;
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

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO:
            DrawText(_svgText, _skOwnerBounds, IgnoreAttributes, canvas);
        }

        protected override void Draw(SKCanvas canvas)
        {
            // TODO:
        }
    }
}
