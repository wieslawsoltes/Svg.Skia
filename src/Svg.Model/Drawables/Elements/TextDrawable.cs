using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class TextDrawable : DrawableBase
    {
        private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);

        public SvgText? Text { get; set; }

        public SKRect OwnerBounds { get; set; }

        private TextDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static TextDrawable Create(SvgText svgText, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new TextDrawable(assetLoader, references)
            {
                Element = svgText,
                Parent = parent,
                Text = svgText,
                OwnerBounds = skViewport,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.Initialize();

            return drawable;
        }

        private void Initialize()
        {
            // TODO: Initialize
        }

        internal void GetPositionsX(SvgTextBase svgTextBase, SKRect skBounds, List<float> xs)
        {
            var _x = svgTextBase.X;

            for (var i = 0; i < _x.Count; i++)
            {
                xs.Add(_x[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsY(SvgTextBase svgTextBase, SKRect skBounds, List<float> ys)
        {
            var _y = svgTextBase.Y;

            for (var i = 0; i < _y.Count; i++)
            {
                ys.Add(_y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDX(SvgTextBase svgTextBase, SKRect skBounds, List<float> dxs)
        {
            var _dx = svgTextBase.Dx;

            for (var i = 0; i < _dx.Count; i++)
            {
                dxs.Add(_dx[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDY(SvgTextBase svgTextBase, SKRect skBounds, List<float> dys)
        {
            var _dy = svgTextBase.Dy;

            for (var i = 0; i < _dy.Count; i++)
            {
                dys.Add(_dy[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal IEnumerable<ISvgNode> GetContentNodes(SvgTextBase svgTextBase)
        {
            if (svgTextBase.Nodes is null || svgTextBase.Nodes.Count < 1)
            {
                foreach (var child in svgTextBase.Children)
                {
                    if (child is ISvgNode svgNode && !(svgNode is ISvgDescriptiveElement))
                    {
                        yield return svgNode;
                    }
                }
            }
            else
            {
                foreach (var node in svgTextBase.Nodes)
                {
                    yield return node;
                }
            }
        }

        internal string PrepareText(SvgTextBase svgTextBase, string value)
        {
            value = ApplyTransformation(svgTextBase, value);
            value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
            return svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve ? value : s_multipleSpaces.Replace(value.Trim(), " ");
        }

        internal string ApplyTransformation(SvgTextBase svgTextBase, string value)
        {
            return svgTextBase.TextTransformation switch
            {
                SvgTextTransformation.Capitalize => value.ToUpper(),
                SvgTextTransformation.Uppercase => value.ToUpper(),
                SvgTextTransformation.Lowercase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
                _ => value,
            };
        }

        internal void BeginDraw(SvgTextBase svgTextBase, SKCanvas skCanvas, SKRect skBounds, DrawAttributes ignoreAttributes, bool enableTransform, out MaskDrawable? maskDrawable, out SKPaint? maskDstIn, out SKPaint? skPaintOpacity, out SKPaint? skPaintFilter, out SKRect? skFilterClip)
        {
            var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter);

            skCanvas.Save();

            var skMatrix = SvgExtensions.ToMatrix(svgTextBase.Transforms);

            if (!skMatrix.IsIdentity && enableTransform)
            {
                var skMatrixTotal = skCanvas.TotalMatrix;
                skMatrixTotal = skMatrixTotal.PreConcat(skMatrix);
                skCanvas.SetMatrix(skMatrixTotal);
            }

            if (enableClip)
            {
                var clipPath = new ClipPath
                {
                    Clip = new ClipPath()
                };
                SvgExtensions.GetSvgVisualElementClipPath(svgTextBase, GeometryBounds, new HashSet<Uri>(), clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !IgnoreAttributes.HasFlag(DrawAttributes.ClipPath))
                {
                    var antialias = SvgExtensions.IsAntialias(svgTextBase);
#if USE_SKIASHARP
                    // TODO: skCanvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias);
                    throw new NotImplementedException();
#else
                    skCanvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias);
#endif
                }
            }

            if (enableMask)
            {
                var mask = default(SKPaint);
                maskDstIn = default(SKPaint);
                maskDrawable = SvgExtensions.GetSvgElementMask(svgTextBase, skBounds, new HashSet<Uri>(), AssetLoader, References);
                if (maskDrawable is { })
                {
                    mask = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill
                    };

                    var lumaColor = SKColorFilter.CreateLumaColor();

                    maskDstIn = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill,
                        BlendMode = SKBlendMode.DstIn,
                        Color = SvgExtensions.s_transparentBlack,
                        ColorFilter = lumaColor
                    };
                    skCanvas.SaveLayer(mask);
                }
            }
            else
            {
                maskDstIn = null;
                maskDrawable = null;
            }

            if (enableOpacity)
            {
                skPaintOpacity = SvgExtensions.GetOpacityPaint(svgTextBase);
                if (skPaintOpacity is { } && !IgnoreAttributes.HasFlag(DrawAttributes.Opacity))
                {
                    skCanvas.SaveLayer(skPaintOpacity);
                }
            }
            else
            {
                skPaintOpacity = null;
            }

            if (enableFilter)
            {
                // TODO: skViewport
                skPaintFilter = SvgExtensions.GetFilterPaint(svgTextBase, skBounds, skBounds, this, AssetLoader, References, out var isValid, out var filterClip);
                skFilterClip = filterClip;
                if (skPaintFilter is { } && !IgnoreAttributes.HasFlag(DrawAttributes.Filter))
                {
                    if (skFilterClip is not null)
                    {
                        skCanvas.ClipRect(skFilterClip.Value, SKClipOperation.Intersect);
                    }

                    skCanvas.SaveLayer(skPaintFilter);
                }
            }
            else
            {
                skPaintFilter = null;
                skFilterClip = null;
            }
        }

        internal void EndDraw(SKCanvas skCanvas, DrawAttributes ignoreAttributes, MaskDrawable? maskDrawable, SKPaint? maskDstIn, SKPaint? skPaintOpacity, SKPaint? skPaintFilter, SKRect? skFilterClip, DrawableBase? until)
        {
            var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter);

            if (skPaintFilter is { } && enableFilter)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity is { } && enableOpacity)
            {
                skCanvas.Restore();
            }

            if (maskDrawable is { } && enableMask && maskDstIn is { })
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, ignoreAttributes, until, true);
                skCanvas.Restore();
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skViewport, DrawAttributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skViewport;

            if (SvgExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgExtensions.GetFillPaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                if (skPaint is { })
                {
                    SvgExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface is { })
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }

            if (SvgExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgExtensions.GetStrokePaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                if (skPaint is { })
                {
                    SvgExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface is { })
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, float currentX, float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            // TODO: Fix SvgTextBase rendering.
            var isValidFill = SvgExtensions.IsValidFill(svgTextBase);
            var isValidStroke = SvgExtensions.IsValidStroke(svgTextBase, skViewport);

            if (!isValidFill && !isValidStroke || text is null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();

            GetPositionsX(svgTextBase, skViewport, xs);
            GetPositionsY(svgTextBase, skViewport, ys);
            GetPositionsDX(svgTextBase, skViewport, dxs);
            GetPositionsDY(svgTextBase, skViewport, dys);

            if (xs.Count >= 1 && ys.Count >= 1 && xs.Count == ys.Count && xs.Count == text.Length)
            {
                // TODO: Fix text position rendering.
                var points = new SKPoint[xs.Count];

                for (var i = 0; i < xs.Count; i++)
                {
                    var x = xs[i];
                    var y = ys[i];
                    float dx = 0;
                    float dy = 0;
                    if (dxs.Count >= 1 && xs.Count >= dxs.Count)
                    {
                        dx = dxs[i];
                    }
                    if (dys.Count >= 1 && ys.Count >= dys.Count)
                    {
                        dy = dys[i];
                    }
                    points[i] = new SKPoint(x + dx, y + dy);
                }

                // TODO: Calculate correct bounds.
                var skBounds = skViewport;

                if (SvgExtensions.IsValidFill(svgTextBase))
                {
                    var skPaint = SvgExtensions.GetFillPaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                    if (skPaint is { })
                    {
                        SvgExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_SKIASHARP
                        var textBlob = SKTextBlob.CreatePositioned(text, skPaint.ToFont(), points);
#else
                        var textBlob = SKTextBlob.CreatePositioned(text, points);
#endif
                        skCanvas.DrawText(textBlob, 0, 0, skPaint);
                    }
                }

                if (SvgExtensions.IsValidStroke(svgTextBase, skBounds))
                {
                    var skPaint = SvgExtensions.GetStrokePaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                    if (skPaint is { })
                    {
                        SvgExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_SKIASHARP
                        var textBlob = SKTextBlob.CreatePositioned(text, skPaint.ToFont(), points);
#else
                        var textBlob = SKTextBlob.CreatePositioned(text, points);
#endif
                        skCanvas.DrawText(textBlob, 0, 0, skPaint);
                    }
                }
            }
            else
            {
                var x = xs.Count >= 1 ? xs[0] : currentX;
                var y = ys.Count >= 1 ? ys[0] : currentY;
                var dx = dxs.Count >= 1 ? dxs[0] : 0f;
                var dy = dys.Count >= 1 ? dys[0] : 0f;

                DrawTextString(svgTextBase, text, x + dx, y + dy, skViewport, ignoreAttributes, skCanvas, until);
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, float currentX, float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes) || !HasFeatures(svgTextPath, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath is null)
            {
                return;
            }

            var skPath = svgPath.PathData?.ToPath(svgPath.FillRule);
            if (skPath is null || skPath.IsEmpty)
            {
                return;
            }

            // TODO: svgPath.Transforms
            // var skMatrixPath = SvgExtensions.ToSKMatrix(svgPath.Transforms);
            // skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skViewport);

            var hOffset = currentX + startOffset;
            var vOffset = currentY;

            // TODO: Calculate correct bounds.
            var skBounds = skViewport;

            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

            // TODO: Fix SvgTextPath rendering.
            var isValidFill = SvgExtensions.IsValidFill(svgTextPath);
            var isValidStroke = SvgExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgExtensions.GetFillPaint(svgTextPath, skBounds, AssetLoader, References, ignoreAttributes);
                        if (skPaint is { })
                        {
                            SvgExtensions.SetPaintText(svgTextPath, skBounds, skPaint);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }

                    if (SvgExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgExtensions.GetStrokePaint(svgTextPath, skBounds, AssetLoader, References, ignoreAttributes);
                        if (skPaint is { })
                        {
                            SvgExtensions.SetPaintText(svgTextPath, skBounds, skPaint);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, float currentX, float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes) || !HasFeatures(svgTextRef, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText is null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skViewport;

            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, currentX, currentY, skViewport, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, float currentX, float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes) || !HasFeatures(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skViewport;

            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, currentX, currentY, skViewport, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
        }

        internal void DrawText(SvgText svgText, SKRect skViewport, DrawAttributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until, bool enableTransform)
        {
            if (!CanDraw(svgText, ignoreAttributes) || !HasFeatures(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skViewport;

            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();
            GetPositionsX(svgText, skViewport, xs);
            GetPositionsY(svgText, skViewport, ys);
            GetPositionsDX(svgText, skViewport, dxs);
            GetPositionsDY(svgText, skViewport, dys);

            var x = xs.Count >= 1 ? xs[0] : 0f;
            var y = ys.Count >= 1 ? ys[0] : 0f;
            var dx = dxs.Count >= 1 ? dxs[0] : 0f;
            var dy = dys.Count >= 1 ? dys[0] : 0f;

            var currentX = x + dx;
            var currentY = y + dy;

            foreach (var node in GetContentNodes(svgText))
            {
                if (!(node is SvgTextBase textNode))
                {
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var text = PrepareText(svgText, node.Content);
                        DrawTextBase(svgText, text, 0f, 0f, skViewport, ignoreAttributes, skCanvas, until);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, currentX, currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                            break;

                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, currentX, currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                            break;

                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, currentX, currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                            break;
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            // TODO: Currently using custom OnDraw override.
        }

        public override void Draw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until, bool enableTransform)
        {
            if (until is { } && this == until)
            {
                return;
            }

            if (Text is { })
            {
                DrawText(Text, OwnerBounds, ignoreAttributes, canvas, until, enableTransform);
            }
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO: OnDraw
            Draw(canvas, IgnoreAttributes, null, true);
        }

        public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
        {
            // TODO: PostProcess
        }
    }
}
