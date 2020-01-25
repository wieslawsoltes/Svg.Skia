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

            var skMatrixPath = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, TransformedBounds, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextPath);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgPaintingExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                        }
                    }

                    if (SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, startOffset, 0f, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
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

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, TransformedBounds, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, skOwnerBounds, ignoreAttributes, skCanvas);
            }

            EndDraw(skCanvas, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, TransformedBounds, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, skOwnerBounds, ignoreAttributes, skCanvas);
            }

            EndDraw(skCanvas, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SvgPaintingExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint);
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint);
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextBase);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

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

                    if (SvgPaintingExtensions.IsValidFill(svgTextBase))
                    {
                        var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                            skCanvas.DrawPositionedText(text, points, skPaint);
                        }
                    }

                    if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
                    {
                        var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
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

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, TransformedBounds, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            foreach (var node in GetContentNodes(svgText))
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

            EndDraw(skCanvas, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        private static void BeginDraw(SvgTextBase svgTextBase, SKCanvas skCanvas, SKRect skBounds, IgnoreAttributes ignoreAttributes, SKRect transformedBounds, CompositeDisposable disposable, out MaskDrawable? maskDrawable, out SKPaint? maskDstIn, out SKPaint? skPaintOpacity, out SKPaint? skPaintFilter)
        {
            skCanvas.Save();

            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgTextBase.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            var skPathClip = SvgClippingExtensions.GetSvgVisualElementClipPath(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
            if (skPathClip != null && !ignoreAttributes.HasFlag(IgnoreAttributes.Clip))
            {
                bool antialias = SvgPaintingExtensions.IsAntialias(svgTextBase);
                skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }

            var mask = default(SKPaint);
            maskDstIn = default(SKPaint);
            maskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgTextBase, transformedBounds, new HashSet<Uri>(), disposable);
            if (maskDrawable != null)
            {
                mask = new SKPaint()
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.StrokeAndFill
                };
                disposable.Add(mask);

                maskDstIn = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.StrokeAndFill,
                    BlendMode = SKBlendMode.DstIn,
                    Color = new SKColor(0, 0, 0, 255),
                    ColorFilter = SKColorFilter.CreateLumaColor()
                };
                disposable.Add(maskDstIn);
                skCanvas.SaveLayer(mask);
            }

            skPaintOpacity = SvgPaintingExtensions.GetOpacitySKPaint(svgTextBase, disposable);
            if (skPaintOpacity != null && !ignoreAttributes.HasFlag(IgnoreAttributes.Opacity))
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextBase, skBounds, disposable);
            if (skPaintFilter != null && !ignoreAttributes.HasFlag(IgnoreAttributes.Filter))
            {
                skCanvas.SaveLayer(skPaintFilter);
            }
        }

        private static void EndDraw(SKCanvas skCanvas, MaskDrawable? maskDrawable, SKPaint? maskDstIn, SKPaint? skPaintOpacity, SKPaint? skPaintFilter)
        {
            if (skPaintFilter != null)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            if (maskDrawable != null)
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, 0f, 0f);
                skCanvas.Restore();
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
