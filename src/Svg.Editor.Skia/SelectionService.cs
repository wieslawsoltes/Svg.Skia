using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Svg;
using Svg.Editor.Svg;
using Svg.Model.Drawables;
using Svg.Pathing;
using Svg.Skia;
using Svg.Transforms;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public class SelectionService
{
    private const string TextBoxLeftAttribute = "data-svgskia-text-box-left";
    private const string TextBoxTopAttribute = "data-svgskia-text-box-top";
    private const string TextBoxWidthAttribute = "data-svgskia-text-box-width";
    private const string TextBoxHeightAttribute = "data-svgskia-text-box-height";

    public bool SnapToGrid { get; set; }
    public double GridSize { get; set; } = 10.0;

    public static SK.SKPoint Mid(SK.SKPoint a, SK.SKPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    public static SK.SKPoint GetLocalCenter(DrawableBase drawable)
    {
        var rect = GetInteractiveBounds(drawable);
        return new SK.SKPoint((rect.Left + rect.Right) / 2f, (rect.Top + rect.Bottom) / 2f);
    }

    public BoundsInfo GetBoundsInfo(DrawableBase drawable, SKSvg skSvg, Func<float> getScale)
    {
        _ = skSvg;
        var rect = GetInteractiveBounds(drawable);
        var m = drawable.TotalTransform;
        var mappedTl = m.MapPoint(new Shim.SKPoint(rect.Left, rect.Top));
        var mappedTr = m.MapPoint(new Shim.SKPoint(rect.Right, rect.Top));
        var mappedBr = m.MapPoint(new Shim.SKPoint(rect.Right, rect.Bottom));
        var mappedBl = m.MapPoint(new Shim.SKPoint(rect.Left, rect.Bottom));
        var tl = new SK.SKPoint(mappedTl.X, mappedTl.Y);
        var tr = new SK.SKPoint(mappedTr.X, mappedTr.Y);
        var br = new SK.SKPoint(mappedBr.X, mappedBr.Y);
        var bl = new SK.SKPoint(mappedBl.X, mappedBl.Y);
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

    public bool NormalizeWorldTranslation(SvgVisualElement element)
    {
        if (element.Transforms is not { Count: > 1 } transforms)
        {
            return false;
        }

        SvgTranslate? activeTranslate = null;
        var activeIndex = -1;
        var translateCount = 0;
        for (var index = 0; index < transforms.Count; index++)
        {
            if (transforms[index] is not SvgTranslate translate)
            {
                continue;
            }

            translateCount++;
            if (activeTranslate is null)
            {
                activeTranslate = translate;
                activeIndex = index;
            }
        }

        if (translateCount != 1 || activeTranslate is null || activeIndex == transforms.Count - 1)
        {
            return false;
        }

        var downstreamMatrix = SK.SKMatrix.CreateIdentity();
        for (var index = activeIndex + 1; index < transforms.Count; index++)
        {
            downstreamMatrix = downstreamMatrix.PreConcat(ToSkMatrix(transforms[index]));
        }

        var worldDelta = MapVector(downstreamMatrix, activeTranslate.X, activeTranslate.Y);
        transforms.RemoveAt(activeIndex);

        if (Math.Abs(worldDelta.X) > 0.0001f || Math.Abs(worldDelta.Y) > 0.0001f)
        {
            transforms.Add(new SvgTranslate(worldDelta.X, worldDelta.Y));
        }

        return true;
    }

    public static Shim.SKRect GetInteractiveBounds(DrawableBase drawable)
    {
        if (drawable.Element is not SvgVisualElement element)
        {
            return drawable.GeometryBounds;
        }

        if (element is SvgGroup group && FrameService.TryGetBackground(group, out var background))
        {
            return new Shim.SKRect(
                background.X.Value,
                background.Y.Value,
                background.X.Value + background.Width.Value,
                background.Y.Value + background.Height.Value);
        }

        if (element is SvgUse use)
        {
            return new Shim.SKRect(
                use.X.Value,
                use.Y.Value,
                use.X.Value + use.Width.Value,
                use.Y.Value + use.Height.Value);
        }

        if (element is SvgTextBase text
            && TryGetTextBoxRect(text, out var textRect))
        {
            return textRect;
        }

        return drawable.GeometryBounds;
    }

    private static bool TryGetTextBoxRect(SvgTextBase text, out Shim.SKRect rect)
    {
        rect = default;
        return TryParseTextBoxValue(text, TextBoxLeftAttribute, out var left)
            && TryParseTextBoxValue(text, TextBoxTopAttribute, out var top)
            && TryParseTextBoxValue(text, TextBoxWidthAttribute, out var width)
            && TryParseTextBoxValue(text, TextBoxHeightAttribute, out var height)
            && width >= 0f
            && height >= 0f
            && TryAssignRect(out rect, left, top, width, height);
    }

    private static bool TryParseTextBoxValue(SvgTextBase text, string key, out float value)
    {
        value = 0f;
        return text.CustomAttributes.TryGetValue(key, out var raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryAssignRect(out Shim.SKRect rect, float left, float top, float width, float height)
    {
        rect = new Shim.SKRect(left, top, left + width, top + height);
        return true;
    }

    public static SK.SKRect GetBoundsRect(BoundsInfo b)
    {
        var left = Math.Min(Math.Min(b.TL.X, b.TR.X), Math.Min(b.BL.X, b.BR.X));
        var top = Math.Min(Math.Min(b.TL.Y, b.TR.Y), Math.Min(b.BL.Y, b.BR.Y));
        var right = Math.Max(Math.Max(b.TL.X, b.TR.X), Math.Max(b.BL.X, b.BR.X));
        var bottom = Math.Max(Math.Max(b.TL.Y, b.TR.Y), Math.Max(b.BL.Y, b.BR.Y));
        return new SK.SKRect(left, top, right, bottom);
    }

    public static bool ContainsRect(SK.SKRect outer, SK.SKRect inner)
    {
        return outer.Left <= inner.Left && outer.Right >= inner.Right &&
               outer.Top <= inner.Top && outer.Bottom >= inner.Bottom;
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
        NormalizeWorldTranslation(element);
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
            var rotation = new SvgRotate(angle, center.X, center.Y);
            if (element.Transforms.Count > 0 && element.Transforms[element.Transforms.Count - 1] is SvgTranslate)
            {
                element.Transforms.Insert(element.Transforms.Count - 1, rotation);
            }
            else
            {
                element.Transforms.Add(rotation);
            }
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

    public void FlipHorizontal(SvgVisualElement element, SK.SKPoint center)
    {
        element.Transforms ??= new SvgTransformCollection();
        element.Transforms.Add(new SvgTranslate(center.X, center.Y));
        element.Transforms.Add(new SvgScale(-1, 1));
        element.Transforms.Add(new SvgTranslate(-center.X, -center.Y));
    }

    public void FlipVertical(SvgVisualElement element, SK.SKPoint center)
    {
        element.Transforms ??= new SvgTransformCollection();
        element.Transforms.Add(new SvgTranslate(center.X, center.Y));
        element.Transforms.Add(new SvgScale(1, -1));
        element.Transforms.Add(new SvgTranslate(-center.X, -center.Y));
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
            case SvgRectangle rect:
                ResizeBox(rect.X.Value, rect.Y.Value, rect.Width.Value, rect.Height.Value,
                    handle, dx, dy,
                    (x, y, w, hgt) =>
                    {
                        rect.X = new SvgUnit(rect.X.Type, x);
                        rect.Y = new SvgUnit(rect.Y.Type, y);
                        rect.Width = new SvgUnit(rect.Width.Type, w);
                        rect.Height = new SvgUnit(rect.Height.Type, hgt);
                    });
                break;
            case SvgEllipse ellipse:
                ResizeEllipse(ellipse, handle, dx, dy);
                break;
            case SvgImage image:
                ResizeBox(image.X.Value, image.Y.Value, image.Width.Value, image.Height.Value,
                    handle, dx, dy,
                    (x, y, w, hgt) =>
                    {
                        image.X = new SvgUnit(image.X.Type, x);
                        image.Y = new SvgUnit(image.Y.Type, y);
                        image.Width = new SvgUnit(image.Width.Type, w);
                        image.Height = new SvgUnit(image.Height.Type, hgt);
                    });
                break;
            case SvgUse use:
                ResizeBox(use.X.Value, use.Y.Value, use.Width.Value, use.Height.Value,
                    handle, dx, dy,
                    (x, y, w, hgt) =>
                    {
                        use.X = new SvgUnit(use.X.Type, x);
                        use.Y = new SvgUnit(use.Y.Type, y);
                        use.Width = new SvgUnit(use.Width.Type, w);
                        use.Height = new SvgUnit(use.Height.Type, hgt);
                    });
                break;
            case SvgCircle circle:
                ResizeCircle(circle, handle, dx, dy);
                break;
            case SvgLine line:
                ResizeLine(line, handle, dx, dy);
                break;
            case SvgPolyline polyline:
                ResizePointCollection(polyline.Points, handle, dx, dy);
                break;
            case SvgPolygon polygon:
                ResizePointCollection(polygon.Points, handle, dx, dy);
                break;
            case SvgGroup group when IsFrameGroup(group):
                ResizeFrame(group, handle, dx, dy);
                break;
            case SvgPath path:
                ResizePath(path, handle, dx, dy);
                break;
        }

        void GetResizedRect(int h, float ddx, float ddy, out float x, out float y, out float w, out float hgt)
        {
            x = startRect.Left;
            y = startRect.Top;
            w = startRect.Width;
            hgt = startRect.Height;
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
        }

        void ResizeBox(
            float initialX,
            float initialY,
            float initialWidth,
            float initialHeight,
            int h,
            float ddx,
            float ddy,
            Action<float, float, float, float> apply)
        {
            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
            if (SnapToGrid)
            {
                x = Snap(x);
                y = Snap(y);
                w = Snap(w);
                hgt = Snap(hgt);
            }
            apply(x, y, w, hgt);
        }

        void ResizeEllipse(SvgEllipse ellipse, int h, float ddx, float ddy)
        {
            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
            var centerX = x + (w / 2f);
            var centerY = y + (hgt / 2f);
            var radiusX = Math.Abs(w) / 2f;
            var radiusY = Math.Abs(hgt) / 2f;
            if (SnapToGrid)
            {
                centerX = Snap(centerX);
                centerY = Snap(centerY);
                radiusX = Math.Abs(Snap(radiusX));
                radiusY = Math.Abs(Snap(radiusY));
            }

            ellipse.CenterX = new SvgUnit(ellipse.CenterX.Type, centerX);
            ellipse.CenterY = new SvgUnit(ellipse.CenterY.Type, centerY);
            ellipse.RadiusX = new SvgUnit(ellipse.RadiusX.Type, radiusX);
            ellipse.RadiusY = new SvgUnit(ellipse.RadiusY.Type, radiusY);
        }

        void ResizeCircle(SvgCircle c, int h, float ddx, float ddy)
        {
            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
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

        void ResizeLine(SvgLine line, int h, float ddx, float ddy)
        {
            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
            var minX = Math.Min(startRect.Left, startRect.Right);
            var minY = Math.Min(startRect.Top, startRect.Bottom);
            var width = startRect.Width == 0f ? 0.01f : startRect.Width;
            var height = startRect.Height == 0f ? 0.01f : startRect.Height;
            var sx = w / width;
            var sy = hgt / height;

            var startX = ScaleCoordinate(line.StartX.Value, minX, x, sx, false);
            var startY = ScaleCoordinate(line.StartY.Value, minY, y, sy, false);
            var endX = ScaleCoordinate(line.EndX.Value, minX, x, sx, false);
            var endY = ScaleCoordinate(line.EndY.Value, minY, y, sy, false);
            if (SnapToGrid)
            {
                startX = Snap(startX);
                startY = Snap(startY);
                endX = Snap(endX);
                endY = Snap(endY);
            }

            line.StartX = new SvgUnit(line.StartX.Type, startX);
            line.StartY = new SvgUnit(line.StartY.Type, startY);
            line.EndX = new SvgUnit(line.EndX.Type, endX);
            line.EndY = new SvgUnit(line.EndY.Type, endY);
        }

        void ResizePointCollection(SvgPointCollection points, int h, float ddx, float ddy)
        {
            if (points.Count < 2)
            {
                return;
            }

            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
            var minX = Math.Min(startRect.Left, startRect.Right);
            var minY = Math.Min(startRect.Top, startRect.Bottom);
            var width = startRect.Width == 0f ? 0.01f : startRect.Width;
            var height = startRect.Height == 0f ? 0.01f : startRect.Height;
            var sx = w / width;
            var sy = hgt / height;

            for (var index = 0; index + 1 < points.Count; index += 2)
            {
                var pointX = ScaleCoordinate(points[index].Value, minX, x, sx, false);
                var pointY = ScaleCoordinate(points[index + 1].Value, minY, y, sy, false);
                if (SnapToGrid)
                {
                    pointX = Snap(pointX);
                    pointY = Snap(pointY);
                }

                points[index] = new SvgUnit(points[index].Type, pointX);
                points[index + 1] = new SvgUnit(points[index + 1].Type, pointY);
            }
        }

        void ResizePath(SvgPath p, int h, float ddx, float ddy)
        {
            if (p.PathData is null || p.PathData.Count == 0)
            {
                return;
            }

            GetResizedRect(h, ddx, ddy, out float x, out float y, out float w, out float hgt);
            if (SnapToGrid)
            {
                x = Snap(x);
                y = Snap(y);
                w = Snap(w);
                hgt = Snap(hgt);
            }

            var width = startRect.Width == 0f ? 0.01f : startRect.Width;
            var height = startRect.Height == 0f ? 0.01f : startRect.Height;
            var sx = w / width;
            var sy = hgt / height;
            var originX = Math.Min(startRect.Left, startRect.Right);
            var originY = Math.Min(startRect.Top, startRect.Bottom);

            foreach (var segment in p.PathData)
            {
                switch (segment)
                {
                    case SvgMoveToSegment move:
                        move.End = ScalePoint(move.End, originX, originY, x, y, sx, sy, move.IsRelative);
                        break;
                    case SvgLineSegment line:
                        line.End = ScalePoint(line.End, originX, originY, x, y, sx, sy, line.IsRelative);
                        break;
                    case SvgQuadraticCurveSegment quadratic:
                        quadratic.ControlPoint = ScalePoint(quadratic.ControlPoint, originX, originY, x, y, sx, sy, quadratic.IsRelative);
                        quadratic.End = ScalePoint(quadratic.End, originX, originY, x, y, sx, sy, quadratic.IsRelative);
                        break;
                    case SvgCubicCurveSegment cubic:
                        cubic.FirstControlPoint = ScalePoint(cubic.FirstControlPoint, originX, originY, x, y, sx, sy, cubic.IsRelative);
                        cubic.SecondControlPoint = ScalePoint(cubic.SecondControlPoint, originX, originY, x, y, sx, sy, cubic.IsRelative);
                        cubic.End = ScalePoint(cubic.End, originX, originY, x, y, sx, sy, cubic.IsRelative);
                        break;
                    case SvgArcSegment arc:
                        arc.RadiusX = Math.Abs(arc.RadiusX * sx);
                        arc.RadiusY = Math.Abs(arc.RadiusY * sy);
                        arc.End = ScalePoint(arc.End, originX, originY, x, y, sx, sy, arc.IsRelative);
                        break;
                }
            }

            p.OnPathUpdated();
        }

        static float ScaleCoordinate(float value, float oldOrigin, float newOrigin, float scale, bool isRelative)
        {
            return isRelative ? value * scale : newOrigin + ((value - oldOrigin) * scale);
        }

        static System.Drawing.PointF ScalePoint(System.Drawing.PointF point, float oldOriginX, float oldOriginY, float newOriginX, float newOriginY, float scaleX, float scaleY, bool isRelative)
        {
            return new System.Drawing.PointF(
                ScaleCoordinate(point.X, oldOriginX, newOriginX, scaleX, isRelative),
                ScaleCoordinate(point.Y, oldOriginY, newOriginY, scaleY, isRelative));
        }

        void ResizeFrame(SvgGroup group, int h, float ddx, float ddy)
        {
            if (!FrameService.TryGetBackground(group, out var background))
            {
                return;
            }

            ResizeBox(background.X.Value, background.Y.Value, background.Width.Value, background.Height.Value,
                h, ddx, ddy,
                (x, y, w, hgt) =>
                {
                    background.X = new SvgUnit(background.X.Type, x);
                    background.Y = new SvgUnit(background.Y.Type, y);
                    background.Width = new SvgUnit(background.Width.Type, w);
                    background.Height = new SvgUnit(background.Height.Type, hgt);
                    FrameService.SyncMetadata(group);
                });
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

    private static bool IsFrameGroup(SvgGroup group)
    {
        return FrameService.IsFrameLikeGroup(group);
    }

    private static SK.SKPoint MapVector(SK.SKMatrix matrix, float x, float y)
    {
        var origin = matrix.MapPoint(0f, 0f);
        var mapped = matrix.MapPoint(x, y);
        return new SK.SKPoint(mapped.X - origin.X, mapped.Y - origin.Y);
    }

    private static SK.SKMatrix ToSkMatrix(SvgTransform transform)
    {
        return transform switch
        {
            SvgMatrix svgMatrix => new SK.SKMatrix
            {
                ScaleX = svgMatrix.Points[0],
                SkewY = svgMatrix.Points[1],
                SkewX = svgMatrix.Points[2],
                ScaleY = svgMatrix.Points[3],
                TransX = svgMatrix.Points[4],
                TransY = svgMatrix.Points[5],
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            },
            SvgRotate svgRotate => SK.SKMatrix.CreateRotationDegrees(svgRotate.Angle, svgRotate.CenterX, svgRotate.CenterY),
            SvgScale svgScale => SK.SKMatrix.CreateScale(svgScale.X, svgScale.Y),
            SvgSkew svgSkew => SK.SKMatrix.CreateSkew(
                (float)Math.Tan(Math.PI * svgSkew.AngleX / 180.0),
                (float)Math.Tan(Math.PI * svgSkew.AngleY / 180.0)),
            SvgTranslate svgTranslate => SK.SKMatrix.CreateTranslation(svgTranslate.X, svgTranslate.Y),
            _ => SK.SKMatrix.CreateIdentity()
        };
    }
}
