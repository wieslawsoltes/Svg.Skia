using System.Collections.Generic;
using System.Linq;
using Svg;
using Svg.Transforms;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

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

    public void Align(IList<(SvgVisualElement Element, SK.SKRect Bounds)> items, AlignType type)
    {
        if (items == null || items.Count < 2)
            return;

        float target;
        switch (type)
        {
            case AlignType.Left:
                target = items.Min(i => i.Bounds.Left);
                foreach (var (el, bounds) in items)
                {
                    var dx = target - bounds.Left;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.HCenter:
                target = items.Average(i => (i.Bounds.Left + i.Bounds.Right) / 2f);
                foreach (var (el, bounds) in items)
                {
                    var cx = (bounds.Left + bounds.Right) / 2f;
                    var dx = target - cx;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.Right:
                target = items.Max(i => i.Bounds.Right);
                foreach (var (el, bounds) in items)
                {
                    var dx = target - bounds.Right;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx + dx, ty);
                }
                break;
            case AlignType.Top:
                target = items.Min(i => i.Bounds.Top);
                foreach (var (el, bounds) in items)
                {
                    var dy = target - bounds.Top;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
            case AlignType.VCenter:
                target = items.Average(i => (i.Bounds.Top + i.Bounds.Bottom) / 2f);
                foreach (var (el, bounds) in items)
                {
                    var cy = (bounds.Top + bounds.Bottom) / 2f;
                    var dy = target - cy;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
            case AlignType.Bottom:
                target = items.Max(i => i.Bounds.Bottom);
                foreach (var (el, bounds) in items)
                {
                    var dy = target - bounds.Bottom;
                    var (tx, ty) = GetTranslation(el);
                    SetTranslation(el, tx, ty + dy);
                }
                break;
        }
    }

    public void Distribute(IList<(SvgVisualElement Element, SK.SKRect Bounds)> items, DistributeType type)
    {
        if (items == null || items.Count < 3)
            return;

        var ordered = type == DistributeType.Horizontal
            ? items.OrderBy(i => i.Bounds.Left).ToList()
            : items.OrderBy(i => i.Bounds.Top).ToList();

        if (type == DistributeType.Horizontal)
        {
            var first = ordered.First().Bounds;
            var last = ordered.Last().Bounds;
            float start = (first.Left + first.Right) / 2f;
            float end = (last.Left + last.Right) / 2f;
            float step = (end - start) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var b = ordered[i].Bounds;
                float center = (b.Left + b.Right) / 2f;
                float target = start + step * i;
                var dx = target - center;
                var (tx, ty) = GetTranslation(ordered[i].Element);
                SetTranslation(ordered[i].Element, tx + dx, ty);
            }
        }
        else
        {
            var first = ordered.First().Bounds;
            var last = ordered.Last().Bounds;
            float start = (first.Top + first.Bottom) / 2f;
            float end = (last.Top + last.Bottom) / 2f;
            float step = (end - start) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var b = ordered[i].Bounds;
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
