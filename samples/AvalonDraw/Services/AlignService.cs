using System.Collections.Generic;
using System.Linq;
using Svg;
using Svg.Model.Drawables;
using Svg.Transforms;
using SK = SkiaSharp;

namespace AvalonDraw.Services;

public class AlignService
{
    public enum AlignType
    {
        Left,
        HCenter,
        Right,
        Top,
        VCenter,
        Bottom
    }

    public enum DistributeType
    {
        Horizontal,
        Vertical
    }

    public void Align(IList<(SvgVisualElement Element, DrawableBase Drawable)> items, AlignType type)
    {
        if (items == null || items.Count < 2)
            return;

        float target;
        switch (type)
        {
            case AlignType.Left:
                target = items.Min(i => i.Drawable.TransformedBounds.Left);
                foreach (var (el, dr) in items)
                {
                    var dx = target - dr.TransformedBounds.Left;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.HCenter:
                target = items.Average(i => (i.Drawable.TransformedBounds.Left + i.Drawable.TransformedBounds.Right) / 2f);
                foreach (var (el, dr) in items)
                {
                    var cx = (dr.TransformedBounds.Left + dr.TransformedBounds.Right) / 2f;
                    var dx = target - cx;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.Right:
                target = items.Max(i => i.Drawable.TransformedBounds.Right);
                foreach (var (el, dr) in items)
                {
                    var dx = target - dr.TransformedBounds.Right;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.Top:
                target = items.Min(i => i.Drawable.TransformedBounds.Top);
                foreach (var (el, dr) in items)
                {
                    var dy = target - dr.TransformedBounds.Top;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
            case AlignType.VCenter:
                target = items.Average(i => (i.Drawable.TransformedBounds.Top + i.Drawable.TransformedBounds.Bottom) / 2f);
                foreach (var (el, dr) in items)
                {
                    var cy = (dr.TransformedBounds.Top + dr.TransformedBounds.Bottom) / 2f;
                    var dy = target - cy;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
            case AlignType.Bottom:
                target = items.Max(i => i.Drawable.TransformedBounds.Bottom);
                foreach (var (el, dr) in items)
                {
                    var dy = target - dr.TransformedBounds.Bottom;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
        }
    }

    public void Distribute(IList<(SvgVisualElement Element, DrawableBase Drawable)> items, DistributeType type)
    {
        if (items == null || items.Count < 3)
            return;

        var ordered = type == DistributeType.Horizontal
            ? items.OrderBy(i => i.Drawable.TransformedBounds.Left).ToList()
            : items.OrderBy(i => i.Drawable.TransformedBounds.Top).ToList();

        if (type == DistributeType.Horizontal)
        {
            var first = ordered.First().Drawable.TransformedBounds;
            var last = ordered.Last().Drawable.TransformedBounds;
            float start = (first.Left + first.Right) / 2f;
            float end = (last.Left + last.Right) / 2f;
            float step = (end - start) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var b = ordered[i].Drawable.TransformedBounds;
                float center = (b.Left + b.Right) / 2f;
                float target = start + step * i;
                var dx = target - center;
                var (tx, ty) = GetTranslation(ordered[i].Element);
                SetTranslation(ordered[i].Element, tx + dx, ty);
            }
        }
        else
        {
            var first = ordered.First().Drawable.TransformedBounds;
            var last = ordered.Last().Drawable.TransformedBounds;
            float start = (first.Top + first.Bottom) / 2f;
            float end = (last.Top + last.Bottom) / 2f;
            float step = (end - start) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var b = ordered[i].Drawable.TransformedBounds;
                float center = (b.Top + b.Bottom) / 2f;
                float target = start + step * i;
                var dy = target - center;
                var (tx, ty) = GetTranslation(ordered[i].Element);
                SetTranslation(ordered[i].Element, tx, ty + dy);
            }
        }
    }

    private static (float X, float Y) GetTranslation(SvgVisualElement element)
    {
        if (element.Transforms is { } t)
        {
            var tr = t.OfType<SvgTranslate>().FirstOrDefault();
            if (tr is { })
                return (tr.X, tr.Y);
        }
        return (0f, 0f);
    }

    private static void SetTranslation(SvgVisualElement element, float x, float y)
    {
        if (element.Transforms == null)
            element.Transforms = new SvgTransformCollection();
        var tr = element.Transforms.OfType<SvgTranslate>().FirstOrDefault();
        if (tr != null)
        {
            tr.X = x;
            tr.Y = y;
        }
        else
        {
            element.Transforms.Add(new SvgTranslate(x, y));
        }
    }
}
