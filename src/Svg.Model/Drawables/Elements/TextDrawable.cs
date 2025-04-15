using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class TextDrawable : DrawableBase
{
    private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);

    public SvgText? Text { get; set; }

    public SKRect OwnerBounds { get; set; }

    private TextDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static TextDrawable Create(SvgText svgText, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
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

    internal string? PrepareText(SvgTextBase svgTextBase, string? value)
    {
        value = ApplyTransformation(svgTextBase, value);
        value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
        return svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve ? value : s_multipleSpaces.Replace(value.TrimStart(), " ");
    }

    internal string? ApplyTransformation(SvgTextBase svgTextBase, string? value)
    {
        if (value is null)
        {
            return value;
        }
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

        var skMatrix = TransformsService.ToMatrix(svgTextBase.Transforms);

        if (!skMatrix.IsIdentity && enableTransform)
        {
            skCanvas.SetMatrix(skMatrix);
        }

        if (enableClip)
        {
            var clipPath = new ClipPath
            {
                Clip = new ClipPath()
            };
            MaskingService.GetSvgVisualElementClipPath(svgTextBase, GeometryBounds, new HashSet<Uri>(), clipPath);
            if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !IgnoreAttributes.HasFlag(DrawAttributes.ClipPath))
            {
                var antialias = PaintingService.IsAntialias(svgTextBase);
                skCanvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias);
            }
        }

        if (enableMask)
        {
            var mask = default(SKPaint);
            maskDstIn = default(SKPaint);
            maskDrawable = MaskingService.GetSvgElementMask(svgTextBase, skBounds, new HashSet<Uri>(), AssetLoader, References);
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
                    Color = FilterEffectsService.s_transparentBlack,
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
            skPaintOpacity = PaintingService.GetOpacityPaint(svgTextBase);
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
            var filterContext = new SvgFilterContext(svgTextBase, skBounds, skBounds, this, AssetLoader, References);
            skPaintFilter = filterContext.FilterPaint;
            skFilterClip = filterContext.FilterClip;
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

    internal void DrawTextString(SvgTextBase svgTextBase, string text, ref float x, ref float y, SKRect skViewport, DrawAttributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
    {
        // TODO: Calculate correct bounds.
        var skBounds = skViewport;
        var fillAdvance = 0f;

        if (PaintingService.IsValidFill(svgTextBase))
        {
            var skPaint = PaintingService.GetFillPaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);

            if (skPaint is { })
            {
                PaintingService.SetPaintText(svgTextBase, skBounds, skPaint);

                foreach (var typefaceSpan in AssetLoader.FindTypefaces(text, skPaint))
                {
                    skPaint.Typeface = typefaceSpan.Typeface;
#if USE_TEXT_SHAPER
                    if (skPaint.Typeface is { } typeface)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, typefaceSpan.text, x + fillAdvance, y, skPaint);
                    }
#else
                    skCanvas.DrawText(typefaceSpan.Text, x + fillAdvance, y, skPaint);
#endif
                    skPaint = skPaint.Clone(); // Don't modify stored skPaint objects
                    fillAdvance += typefaceSpan.Advance;
                }
            }
        }

        var strokeAdvance = 0f;

        if (PaintingService.IsValidStroke(svgTextBase, skBounds))
        {
            var skPaint = PaintingService.GetStrokePaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
            if (skPaint is { })
            {
                PaintingService.SetPaintText(svgTextBase, skBounds, skPaint);

                foreach (var typefaceSpan in AssetLoader.FindTypefaces(text, skPaint))
                {
                    skPaint.Typeface = typefaceSpan.Typeface;
#if USE_TEXT_SHAPER
                    if (skPaint.Typeface is { } typeface)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, typefaceSpan.text, x + strokeAdvance, y, skPaint);
                    }
#else
                    skCanvas.DrawText(typefaceSpan.Text, x + strokeAdvance, y, skPaint);
#endif
                    skPaint = skPaint.Clone(); // Don't modify stored skPaint objects
                    strokeAdvance += typefaceSpan.Advance;
                }
            }
        }

        x += Math.Max(strokeAdvance, fillAdvance);
    }

    internal void DrawTextBase(SvgTextBase svgTextBase, ref float currentX, ref float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
    {
        foreach (var node in GetContentNodes(svgTextBase))
        {
            switch (node)
            {
                case not SvgTextBase:
                {
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(svgTextBase, node.Content);
                    // TODO: Fix SvgTextBase rendering.
                    var isValidFill = PaintingService.IsValidFill(svgTextBase);
                    var isValidStroke = PaintingService.IsValidStroke(svgTextBase, skViewport);

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

                    static int Codepoints(string text)
                    {
                        return text.Length - System.Linq.Enumerable.Count(text, char.IsLowSurrogate);
                    }

                    if (xs.Count >= 1 && ys.Count >= 1 && xs.Count == ys.Count && xs.Count == Codepoints(text))
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
                        int endingCodepointStart = text.Length - (char.IsLowSurrogate(text[text.Length - 1]) ? 2 : 1);

                        float DrawTextLocal(SKPaint? skPaint)
                        {
                            if (skPaint is null)
                            {
                                return 0;
                            }

                            PaintingService.SetPaintText(svgTextBase, skBounds, skPaint);

                            int offset = 0;
                            foreach (var typefaceSpan in AssetLoader.FindTypefaces(text.Substring(0, endingCodepointStart), skPaint))
                            {
                                skPaint.Typeface = typefaceSpan.Typeface;
                                var codepoints = Codepoints(typefaceSpan.Text);
                                var textBlob = SKTextBlob.CreatePositioned(
                                    typefaceSpan.Text,
                                    points.AsMemory(offset, codepoints).ToArray());
                                skPaint = skPaint.Clone(); // Don't modify stored skPaint objects
                                skCanvas.DrawText(textBlob, 0, 0, skPaint);
                                offset += codepoints;
                            }

                            foreach (var typefaceSpan in AssetLoader.FindTypefaces(text.Substring(endingCodepointStart), skPaint))
                            {
                                skPaint.Typeface = typefaceSpan.Typeface;
                                skCanvas.DrawText(typefaceSpan.Text, points[points.Length - 1].X, points[points.Length - 1].Y, skPaint);
                                return typefaceSpan.Advance;
                            }

                            throw new ApplicationException("Code expected to be unreachable");
                        }

                        var fillAdvance = 0f;

                        if (PaintingService.IsValidFill(svgTextBase))
                        {
                            var skPaint = PaintingService.GetFillPaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                            fillAdvance = DrawTextLocal(skPaint);
                        }

                        var strokeAdvance = 0f;

                        if (PaintingService.IsValidStroke(svgTextBase, skBounds))
                        {
                            var skPaint = PaintingService.GetStrokePaint(svgTextBase, skBounds, AssetLoader, References, ignoreAttributes);
                            strokeAdvance = DrawTextLocal(skPaint);
                        }

                        currentX = points[points.Length - 1].X + Math.Max(fillAdvance, strokeAdvance);
                        currentY = points[points.Length - 1].Y;
                    }
                    else
                    {
                        var x = xs.Count >= 1 ? xs[0] : currentX;
                        var y = ys.Count >= 1 ? ys[0] : currentY;
                        var dx = dxs.Count >= 1 ? dxs[0] : 0f;
                        var dy = dys.Count >= 1 ? dys[0] : 0f;
                        currentX = x + dx;
                        currentY = y + dy;
                        DrawTextString(svgTextBase, text, ref currentX, ref currentY, skViewport, ignoreAttributes, skCanvas, until);
                    }

                    break;
                }
                case SvgTextPath svgTextPath:
                {
                    DrawTextPath(svgTextPath, ref currentX, ref currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                    break;
                }
                case SvgTextRef svgTextRef:
                {
                    DrawTextRef(svgTextRef, ref currentX, ref currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                    break;
                }
                case SvgTextSpan svgTextSpan:
                {
                    DrawTextSpan(svgTextSpan, ref currentX, ref currentY, skViewport, ignoreAttributes, true, skCanvas, until);
                    break;
                }
            }
        }
    }

    internal void DrawTextPath(SvgTextPath svgTextPath, ref float currentX, ref float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
    {
        if (!CanDraw(svgTextPath, ignoreAttributes) || !HasFeatures(svgTextPath, ignoreAttributes))
        {
            return;
        }

        if (SvgService.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
        {
            return;
        }

        var svgPath = SvgService.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
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
        // var skMatrixPath = TransformsService.ToSKMatrix(svgPath.Transforms);
        // skPath.Transform(skMatrixPath);

        // TODO: Implement StartOffset
        var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skViewport);

        var hOffset = currentX + startOffset;
        var vOffset = currentY;

        // TODO: Calculate correct bounds.
        var skBounds = skViewport;

        BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

        // TODO: Fix SvgTextPath rendering.
        var isValidFill = PaintingService.IsValidFill(svgTextPath);
        var isValidStroke = PaintingService.IsValidStroke(svgTextPath, skBounds);

        if (isValidFill || isValidStroke)
        {
            if (!string.IsNullOrEmpty(svgTextPath.Text))
            {
                var text = PrepareText(svgTextPath, svgTextPath.Text);

                if (PaintingService.IsValidFill(svgTextPath))
                {
                    var skPaint = PaintingService.GetFillPaint(svgTextPath, skBounds, AssetLoader, References, ignoreAttributes);
                    if (skPaint is { } && text is { })
                    {
                        PaintingService.SetPaintText(svgTextPath, skBounds, skPaint);
                        skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                    }
                }

                if (PaintingService.IsValidStroke(svgTextPath, skBounds))
                {
                    var skPaint = PaintingService.GetStrokePaint(svgTextPath, skBounds, AssetLoader, References, ignoreAttributes);
                    if (skPaint is { } && text is { })
                    {
                        PaintingService.SetPaintText(svgTextPath, skBounds, skPaint);
                        skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                    }
                }
            }
        }

        EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
    }

    internal void DrawTextRef(SvgTextRef svgTextRef, ref float currentX, ref float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
    {
        if (!CanDraw(svgTextRef, ignoreAttributes) || !HasFeatures(svgTextRef, ignoreAttributes))
        {
            return;
        }

        if (SvgService.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
        if (svgReferencedText is null)
        {
            return;
        }

        // TODO: Calculate correct bounds.
        var skBounds = skViewport;

        BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

        DrawTextBase(svgReferencedText, ref currentX, ref currentY, skViewport, ignoreAttributes, skCanvas, until);

        EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, skFilterClip, until);
    }

    internal void DrawTextSpan(SvgTextSpan svgTextSpan, ref float currentX, ref float currentY, SKRect skViewport, DrawAttributes ignoreAttributes, bool enableTransform, SKCanvas skCanvas, DrawableBase? until)
    {
        if (!CanDraw(svgTextSpan, ignoreAttributes) || !HasFeatures(svgTextSpan, ignoreAttributes))
        {
            return;
        }

        // TODO: Calculate correct bounds.
        var skBounds = skViewport;

        BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, enableTransform, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter, out var skFilterClip);

        DrawTextBase(svgTextSpan, ref currentX, ref currentY, skViewport, ignoreAttributes, skCanvas, until);

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

        DrawTextBase(svgText, ref currentX, ref currentY, skViewport, ignoreAttributes, skCanvas, until);

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
