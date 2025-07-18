using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Svg;
using Svg.Model.Drawables;
using Svg.Skia;
using Svg.Transforms;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace AvalonDraw.Services;

public class SelectionService
{
    public record struct BoundsInfo(
        SK.SKPoint TL,
        SK.SKPoint TR,
        SK.SKPoint BR,
        SK.SKPoint BL,
        SK.SKPoint TopMid,
        SK.SKPoint RightMid,
        SK.SKPoint BottomMid,
        SK.SKPoint LeftMid,
        SK.SKPoint Center,
        SK.SKPoint RotHandle);

    public bool SnapToGrid { get; set; }
    public double GridSize { get; set; } = 10.0;

    public static SK.SKPoint Mid(SK.SKPoint a, SK.SKPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    public BoundsInfo GetBoundsInfo(DrawableBase drawable, SKSvg skSvg, Func<float> getScale)
    {
        var rect = drawable.GeometryBounds;
        var m = drawable.TotalTransform;
        var tl = skSvg.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Left, rect.Top)));
        var tr = skSvg.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Right, rect.Top)));
        var br = skSvg.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Right, rect.Bottom)));
        var bl = skSvg.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Left, rect.Bottom)));
        var topMid = Mid(tl, tr);
        var rightMid = Mid(tr, br);
        var bottomMid = Mid(br, bl);
        var leftMid = Mid(bl, tl);
        var center = Mid(tl, br);
        var edge = new SK.SKPoint(tr.X - tl.X, tr.Y - tl.Y);
        var len = (float)Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        var normal = len > 0 ? new SK.SKPoint(edge.Y / len, -edge.X / len) : new SK.SKPoint(0, -1);
        var scale = getScale();
        var rotHandle = new SK.SKPoint(topMid.X + normal.X * 20f / scale, topMid.Y + normal.Y * 20f / scale);
        return new BoundsInfo(tl, tr, br, bl, topMid, rightMid, bottomMid, leftMid, center, rotHandle);
    }

    public static SK.SKRect GetBoundsRect(BoundsInfo b)
    {
        var left = Math.Min(Math.Min(b.TL.X, b.TR.X), Math.Min(b.BL.X, b.BR.X));
        var top = Math.Min(Math.Min(b.TL.Y, b.TR.Y), Math.Min(b.BL.Y, b.BR.Y));
        var right = Math.Max(Math.Max(b.TL.X, b.TR.X), Math.Max(b.BL.X, b.BR.X));
        var bottom = Math.Max(Math.Max(b.TL.Y, b.TR.Y), Math.Max(b.BL.Y, b.BR.Y));
        return new SK.SKRect(left, top, right, bottom);
    }

    public int HitHandle(BoundsInfo b, SK.SKPoint pt, float scale, out SK.SKPoint center)
    {
        center = b.Center;
        var hs = HandleSize / 2f / scale;
        var handlePts = new[] { b.TL, b.TopMid, b.TR, b.RightMid, b.BR, b.BottomMid, b.BL, b.LeftMid };
        for (int i = 0; i < handlePts.Length; i++)
        {
            var r = new SK.SKRect(handlePts[i].X - hs, handlePts[i].Y - hs, handlePts[i].X + hs, handlePts[i].Y + hs);
            if (r.Contains(pt))
                return i;
        }
        if (SK.SKPoint.Distance(b.RotHandle, pt) <= HandleSize / scale)
            return 8;
        return -1;
    }

    public int HitPolyPoint(IList<Shim.SKPoint> points, Shim.SKMatrix matrix, SK.SKPoint pt, float scale)
    {
        var hs = HandleSize / 2f / scale;
        for (int i = 0; i < points.Count; i++)
        {
            var p = matrix.MapPoint(points[i]);
            var r = new SK.SKRect(p.X - hs, p.Y - hs, p.X + hs, p.Y + hs);
            if (r.Contains(pt))
                return i;
        }
        return -1;
    }

    public float GetRotation(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var rot = t.OfType<SvgRotate>().FirstOrDefault();
            return rot?.Angle ?? 0f;
        }
        return 0f;
    }

    public void SetRotation(SvgVisualElement element, float angle, SK.SKPoint center)
    {
        element.Transforms ??= new SvgTransformCollection();
        var rot = element.Transforms.OfType<SvgRotate>().FirstOrDefault();
        if (rot != null)
        {
            rot.Angle = angle;
            rot.CenterX = center.X;
            rot.CenterY = center.Y;
        }
        else
        {
            element.Transforms.Add(new SvgRotate(angle, center.X, center.Y));
        }
    }

    public (float X, float Y) GetTranslation(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var tr = t.OfType<SvgTranslate>().FirstOrDefault();
            if (tr is { })
                return (tr.X, tr.Y);
        }
        return (0f, 0f);
    }

    public void SetTranslation(SvgVisualElement element, float x, float y)
    {
        if (SnapToGrid && GridSize > 0)
        {
            x = Snap(x);
            y = Snap(y);
        }
        element.Transforms ??= new SvgTransformCollection();
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

    public (float X, float Y) GetScale(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var sc = t.OfType<SvgScale>().FirstOrDefault();
            if (sc is { })
                return (sc.X, sc.Y);
        }
        return (1f, 1f);
    }

    public void SetScale(SvgVisualElement element, float x, float y)
    {
        element.Transforms ??= new SvgTransformCollection();
        var sc = element.Transforms.OfType<SvgScale>().FirstOrDefault();
        if (sc != null)
        {
            sc.X = x;
            sc.Y = y;
        }
        else
        {
            element.Transforms.Add(new SvgScale(x, y));
        }
    }

    public (float X, float Y) GetSkew(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var sk = t.OfType<SvgSkew>().FirstOrDefault();
            if (sk is { })
                return (sk.AngleX, sk.AngleY);
        }
        return (0f, 0f);
    }

    public void SetSkew(SvgVisualElement element, float x, float y)
    {
        element.Transforms ??= new SvgTransformCollection();
        var sk = element.Transforms.OfType<SvgSkew>().FirstOrDefault();
        if (sk != null)
        {
            sk.AngleX = x;
            sk.AngleY = y;
        }
        else
        {
            element.Transforms.Add(new SvgSkew(x, y));
        }
    }

    public float Snap(float value)
    {
        if (!SnapToGrid || GridSize <= 0)
            return value;
        return (float)(Math.Round(value / GridSize) * GridSize);
    }

    public void ResizeElement(SvgVisualElement element, int handle, float dx, float dy, SK.SKRect startRect, float startTransX, float startTransY, float startScaleX, float startScaleY)
    {
        switch (element)
        {
            case SvgRectangle:
            case SvgImage:
            case SvgUse:
                ResizeBox((dynamic)element, handle, dx, dy);
                break;
            case SvgCircle circle:
                ResizeCircle(circle, handle, dx, dy);
                break;
            case SvgPath path:
                ResizePath(path, handle, dx, dy);
                break;
        }

        void ResizeBox(dynamic el, int h, float ddx, float ddy)
        {
            float x = el.X.Value;
            float y = el.Y.Value;
            float w = el.Width.Value;
            float hgt = el.Height.Value;
            switch (h)
            {
                case 0:
                    x = startRect.Left + ddx;
                    y = startRect.Top + ddy;
                    w = startRect.Right - x;
                    hgt = startRect.Bottom - y;
                    break;
                case 1:
                    y = startRect.Top + ddy;
                    hgt = startRect.Bottom - y;
                    break;
                case 2:
                    y = startRect.Top + ddy;
                    w = startRect.Width + ddx;
                    hgt = startRect.Bottom - y;
                    break;
                case 3:
                    w = startRect.Width + ddx;
                    break;
                case 4:
                    w = startRect.Width + ddx;
                    hgt = startRect.Height + ddy;
                    break;
                case 5:
                    hgt = startRect.Height + ddy;
                    break;
                case 6:
                    x = startRect.Left + ddx;
                    w = startRect.Right - x;
                    hgt = startRect.Height + ddy;
                    break;
                case 7:
                    x = startRect.Left + ddx;
                    w = startRect.Right - x;
                    break;
            }
            if (SnapToGrid)
            {
                x = Snap(x);
                y = Snap(y);
                w = Snap(w);
                hgt = Snap(hgt);
            }
            el.X = new SvgUnit(el.X.Type, x);
            el.Y = new SvgUnit(el.Y.Type, y);
            el.Width = new SvgUnit(el.Width.Type, w);
            el.Height = new SvgUnit(el.Height.Type, hgt);
        }

        void ResizeCircle(SvgCircle c, int h, float ddx, float ddy)
        {
            float x = startRect.Left;
            float y = startRect.Top;
            float w = startRect.Width;
            float hgt = startRect.Height;
            switch (h)
            {
                case 0:
                    x += ddx;
                    y += ddy;
                    w = startRect.Right - x;
                    hgt = startRect.Bottom - y;
                    break;
                case 1:
                    y += ddy;
                    hgt = startRect.Bottom - y;
                    break;
                case 2:
                    y += ddy;
                    w += ddx;
                    hgt = startRect.Bottom - y;
                    break;
                case 3:
                    w += ddx;
                    break;
                case 4:
                    w += ddx;
                    hgt += ddy;
                    break;
                case 5:
                    hgt += ddy;
                    break;
                case 6:
                    x += ddx;
                    w = startRect.Right - x;
                    hgt += ddy;
                    break;
                case 7:
                    x += ddx;
                    w = startRect.Right - x;
                    break;
            }
            var cx = x + w / 2f;
            var cy = y + hgt / 2f;
            var r = Math.Max(w, hgt) / 2f;
            if (SnapToGrid)
            {
                cx = Snap(cx);
                cy = Snap(cy);
                r = Snap(r);
            }
            c.CenterX = new SvgUnit(c.CenterX.Type, cx);
            c.CenterY = new SvgUnit(c.CenterY.Type, cy);
            c.Radius = new SvgUnit(c.Radius.Type, r);
        }

        void ResizePath(SvgPath p, int h, float ddx, float ddy)
        {
            float x = startRect.Left;
            float y = startRect.Top;
            float w = startRect.Width;
            float hgt = startRect.Height;
            switch (h)
            {
                case 0:
                    x += ddx;
                    y += ddy;
                    w = startRect.Right - x;
                    hgt = startRect.Bottom - y;
                    break;
                case 1:
                    y += ddy;
                    hgt = startRect.Bottom - y;
                    break;
                case 2:
                    y += ddy;
                    w += ddx;
                    hgt = startRect.Bottom - y;
                    break;
                case 3:
                    w += ddx;
                    break;
                case 4:
                    w += ddx;
                    hgt += ddy;
                    break;
                case 5:
                    hgt += ddy;
                    break;
                case 6:
                    x += ddx;
                    w = startRect.Right - x;
                    hgt += ddy;
                    break;
                case 7:
                    x += ddx;
                    w = startRect.Right - x;
                    break;
            }
            if (SnapToGrid)
            {
                x = Snap(x);
                y = Snap(y);
                w = Snap(w);
                hgt = Snap(hgt);
            }
            if (w == 0)
                w = 0.01f;
            if (hgt == 0)
                hgt = 0.01f;
            var sx = w / startRect.Width;
            var sy = hgt / startRect.Height;
            var tx = x - startRect.Left;
            var ty = y - startRect.Top;
            SetScale(p, startScaleX * sx, startScaleY * sy);
            SetTranslation(p, startTransX + tx, startTransY + ty);
        }
    }

    public void SkewElement(SvgVisualElement element, int handle, float dx, float dy, SK.SKRect startRect, float startSkewX, float startSkewY)
    {
        var ax = startSkewX;
        var ay = startSkewY;
        switch (handle)
        {
            case 1:
            case 5:
                ax += (float)(Math.Atan(dx / startRect.Height) * 180.0 / Math.PI);
                break;
            case 3:
            case 7:
                ay += (float)(Math.Atan(dy / startRect.Width) * 180.0 / Math.PI);
                break;
            default:
                ax += (float)(Math.Atan(dx / startRect.Height) * 180.0 / Math.PI);
                ay += (float)(Math.Atan(dy / startRect.Width) * 180.0 / Math.PI);
                break;
        }
        SetSkew(element, ax, ay);
    }

    public bool GetDragProperties(SvgVisualElement element, out List<(PropertyInfo Prop, SvgUnit Unit, char Axis)> props)
    {
        var list = new List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>();
        switch (element)
        {
            case SvgRectangle:
            case SvgImage:
            case SvgUse:
                Add("X", 'x');
                Add("Y", 'y');
                break;
            case SvgCircle:
            case SvgEllipse:
                Add("CenterX", 'x');
                Add("CenterY", 'y');
                break;
            case SvgLine:
                Add("StartX", 'x');
                Add("StartY", 'y');
                Add("EndX", 'x');
                Add("EndY", 'y');
                break;
            case SvgTextBase:
            case SvgPath:
                props = null!;
                return false;
            default:
                props = null!;
                return false;
        }
        props = list;
        return props.Count > 0;

        void Add(string name, char axis)
        {
            var p = element.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(SvgUnit))
            {
                var unit = (SvgUnit)p.GetValue(element)!;
                list.Add((p, unit, axis));
            }
        }
    }

    public const float HandleSize = 10f;
}

