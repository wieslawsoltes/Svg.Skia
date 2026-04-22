using System.Numerics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp.Views.Maui.Controls;
using SkiaFont = SkiaSharp.SKFont;
using SkiaPaint = SkiaSharp.SKPaint;
using SkiaTypeface = SkiaSharp.SKTypeface;

namespace SvgML;

public partial class svg
{
    private static readonly char[] s_inlineCoordinateSeparators = [' ', '\t', '\r', '\n', ','];

    private readonly AbsoluteLayout _layoutRoot = new();
    private readonly SKCanvasView _drawingSurface = new();
    private readonly List<HostedControlEntry> _hostedControlEntries = [];
    private readonly HashSet<View> _attachedHostedControls = [];

    private void InitializeHostedControls()
    {
        _drawingSurface.PaintSurface += OnDrawingSurfacePaintSurface;
        AbsoluteLayout.SetLayoutFlags(_drawingSurface, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_drawingSurface, new Rect(0D, 0D, 1D, 1D));
        _layoutRoot.Children.Add(_drawingSurface);
        Content = _layoutRoot;
    }

    protected override Size ArrangeOverride(Rect bounds)
    {
        var arranged = base.ArrangeOverride(bounds);
        ArrangeHostedControls();
        return arranged;
    }

    private void SynchronizeHostedControls()
    {
        var desiredEntries = HostedControlTree
            .Enumerate(this)
            .Select(static entry => CreateHostedControlEntry(entry.Element, entry.Host))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!.Value)
            .ToList();

        var desiredViews = new HashSet<View>(desiredEntries.Select(static entry => entry.View));

        foreach (var view in _attachedHostedControls.Where(view => !desiredViews.Contains(view)).ToList())
        {
            _layoutRoot.Children.Remove(view);
            _attachedHostedControls.Remove(view);
        }

        foreach (var view in desiredEntries.Select(static entry => entry.View))
        {
            if (_attachedHostedControls.Add(view))
            {
                _layoutRoot.Children.Add(view);
            }
        }

        _hostedControlEntries.Clear();
        _hostedControlEntries.AddRange(desiredEntries);
        InvalidateMeasure();
    }

    private void ArrangeHostedControls()
    {
        foreach (var entry in _hostedControlEntries)
        {
            var bounds = GetHostedControlBounds(entry);
            if (bounds.Width <= 0D || bounds.Height <= 0D)
            {
                entry.View.Arrange(default);
                continue;
            }

            AbsoluteLayout.SetLayoutFlags(entry.View, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(entry.View, bounds);
            entry.View.Arrange(bounds);
        }
    }

    private Rect GetHostedControlBounds(HostedControlEntry entry)
    {
        var isInline = IsInlineHostedControl(entry);
        var bounds = GetControlBounds(entry.Element);
        if (isInline && TryGetInlineHostedControlBounds(entry.Element, entry.Host, out var inlineBounds))
        {
            bounds = inlineBounds;
        }

        if (!isInline || bounds.Width <= 0D || bounds.Height <= 0D)
        {
            return !isInline && TryGetForeignObjectHostedControlBounds(entry.Element, entry.Host, out var fallbackBounds)
                ? fallbackBounds
                : bounds;
        }

        var desired = entry.View.Measure(double.PositiveInfinity, double.PositiveInfinity);
        var width = desired.Width > 0D ? desired.Width : bounds.Width;
        var height = desired.Height > 0D ? desired.Height : bounds.Height;
        return new Rect(bounds.X, bounds.Bottom - height, width, height);
    }

    private bool TryGetForeignObjectHostedControlBounds(
        element hostedElement,
        IHostedControlElement host,
        out Rect bounds)
    {
        bounds = default;

        if (host is not foreignObject foreignObject
            || !TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        var slot = foreignObject.GetHostSlot();
        if (slot.IsEmpty)
        {
            return false;
        }

        var localBounds = new Rect(slot.X, slot.Y, slot.Width, slot.Height);
        var pictureBounds = TransformPictureBoundsToControl(localBounds, hostedElement.TotalTransformMatrix);
        bounds = TransformPictureBoundsToControl(pictureBounds, renderInfo.Matrix);
        return bounds.Width > 0D && bounds.Height > 0D;
    }

    private bool TryGetInlineHostedControlBounds(element inlineElement, IHostedControlElement host, out Rect bounds)
    {
        bounds = default;

        if (!TryGetRenderInfo(out var renderInfo))
        {
            return false;
        }

        if (!TryGetInlinePictureBounds(inlineElement, host, out var pictureBounds))
        {
            return false;
        }

        bounds = TransformPictureBoundsToControl(pictureBounds, renderInfo.Matrix);
        return bounds.Width > 0D && bounds.Height > 0D;
    }

    private bool TryGetInlinePictureBounds(element inlineElement, IHostedControlElement host, out Rect bounds)
    {
        bounds = default;

        var layoutRoot = GetInlineLayoutRoot(inlineElement);
        if (layoutRoot is null)
        {
            return false;
        }

        var currentX = 0D;
        var currentY = 0D;
        if (!TryLocateInlinePictureBounds(
                layoutRoot,
                inlineElement,
                host,
                ref currentX,
                ref currentY,
                out var localBounds,
                out var boundsContainer))
        {
            return false;
        }

        var localToPicture = boundsContainer.TotalTransformMatrix;
        if (localToPicture == Matrix3x2.Identity && !ReferenceEquals(boundsContainer, layoutRoot))
        {
            localToPicture = layoutRoot.TotalTransformMatrix;
        }

        bounds = TransformPictureBoundsToControl(localBounds, localToPicture);
        return bounds.Width > 0D && bounds.Height > 0D;
    }

    private static text_base? GetInlineLayoutRoot(element inlineElement)
    {
        text_base? result = null;

        for (var current = inlineElement.ParentElement; current is not null; current = current.ParentElement)
        {
            if (current is text_base textBase)
            {
                result = textBase;
            }
        }

        return result;
    }

    private bool TryLocateInlinePictureBounds(
        text_base container,
        element target,
        IHostedControlElement targetHost,
        ref double currentX,
        ref double currentY,
        out Rect bounds,
        out text_base boundsContainer)
    {
        ApplyTextPosition(container, ref currentX, ref currentY);

        foreach (var child in container.Children)
        {
            switch (child)
            {
                case content textContent:
                    currentX += MeasureTextContentWidth(textContent.Content, container);
                    break;

                case element hostedElement when hostedElement is IHostedControlElement hostedChild
                    && hostedChild.HostedControl is not null:
                    var size = hostedChild.GetHostedControlSize().OrFallback();
                    if (ReferenceEquals(hostedElement, target) && ReferenceEquals(hostedChild, targetHost))
                    {
                        bounds = new Rect(
                            currentX,
                            GetInlinePictureTop(container, size.Height, currentY),
                            size.Width,
                            size.Height);
                        boundsContainer = container;
                        return true;
                    }

                    currentX += size.Width;
                    break;

                case text_base nestedText:
                    var childX = currentX;
                    var childY = currentY;
                    if (TryLocateInlinePictureBounds(
                            nestedText,
                            target,
                            targetHost,
                            ref childX,
                            ref childY,
                            out bounds,
                            out boundsContainer))
                    {
                        currentX = childX;
                        currentY = childY;
                        return true;
                    }

                    currentX = childX;
                    currentY = childY;
                    break;
            }
        }

        bounds = default;
        boundsContainer = null!;
        return false;
    }

    private static double GetInlinePictureTop(element styleSource, double height, double baselineY)
    {
        return baselineY + GetTextCenterOffset(styleSource) - height / 2D;
    }

    private static double GetTextCenterOffset(element styleSource)
    {
        SkiaTypeface? typeface = null;
        if (ResolveFontFamily(styleSource) is { Length: > 0 } familyName)
        {
            typeface = SkiaTypeface.FromFamilyName(familyName);
        }

        try
        {
            using var font = typeface is null
                ? new SkiaFont { Size = (float)ResolveFontSize(styleSource) }
                : new SkiaFont(typeface, (float)ResolveFontSize(styleSource));
            font.GetFontMetrics(out var metrics);
            return (metrics.Ascent + metrics.Descent) / 2D;
        }
        finally
        {
            typeface?.Dispose();
        }
    }

    private void ApplyTextPosition(text_base textBase, ref double currentX, ref double currentY)
    {
        if (TryResolveFirstCoordinate(textBase.x, textBase, InlineCoordinateAxis.X, out var x))
        {
            currentX = x;
        }

        if (TryResolveFirstCoordinate(textBase.y, textBase, InlineCoordinateAxis.Y, out var y))
        {
            currentY = y;
        }

        if (TryResolveFirstCoordinate(textBase.dx, textBase, InlineCoordinateAxis.X, out var dx))
        {
            currentX += dx;
        }

        if (TryResolveFirstCoordinate(textBase.dy, textBase, InlineCoordinateAxis.Y, out var dy))
        {
            currentY += dy;
        }
    }

    private bool TryResolveFirstCoordinate(
        string? value,
        element styleSource,
        InlineCoordinateAxis axis,
        out double coordinate)
    {
        coordinate = 0D;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(s_inlineCoordinateSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var token = parts[0];
        if (TryParseSvgUnit(token, out var unit) && TryConvertCoordinateUnit(unit, styleSource, axis, out coordinate))
        {
            return true;
        }

        var parsed = numbers.Parse(token).Number;
        if (parsed is null || parsed.Count == 0)
        {
            return false;
        }

        coordinate = parsed[0];
        return true;
    }

    private bool TryConvertCoordinateUnit(
        Svg.SvgUnit unit,
        element styleSource,
        InlineCoordinateAxis axis,
        out double coordinate)
    {
        coordinate = 0D;

        if (unit.IsEmpty || unit.IsNone)
        {
            return false;
        }

        var fontSize = ResolveFontSize(styleSource);
        var reference = GetCoordinateReference(axis);
        coordinate = unit.Type switch
        {
            Svg.SvgUnitType.Pixel or Svg.SvgUnitType.User => unit.Value,
            Svg.SvgUnitType.Em => fontSize * unit.Value,
            Svg.SvgUnitType.Ex => fontSize * unit.Value * 0.5D,
            Svg.SvgUnitType.Percentage => reference * unit.Value / 100D,
            Svg.SvgUnitType.Inch => unit.Value * 96D,
            Svg.SvgUnitType.Centimeter => unit.Value * 96D / 2.54D,
            Svg.SvgUnitType.Millimeter => unit.Value * 96D / 25.4D,
            Svg.SvgUnitType.Pica => unit.Value * 16D,
            Svg.SvgUnitType.Point => unit.Value * 96D / 72D,
            _ => 0D
        };

        return unit.Type is Svg.SvgUnitType.Pixel
            or Svg.SvgUnitType.User
            or Svg.SvgUnitType.Em
            or Svg.SvgUnitType.Ex
            or Svg.SvgUnitType.Percentage
            or Svg.SvgUnitType.Inch
            or Svg.SvgUnitType.Centimeter
            or Svg.SvgUnitType.Millimeter
            or Svg.SvgUnitType.Pica
            or Svg.SvgUnitType.Point;
    }

    private double GetCoordinateReference(InlineCoordinateAxis axis)
    {
        var picture = _picture;
        if (picture is null)
        {
            return 0D;
        }

        return axis == InlineCoordinateAxis.X
            ? picture.CullRect.Width
            : picture.CullRect.Height;
    }

    private static double MeasureTextContentWidth(string? text, element styleSource)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0D;
        }

        SkiaTypeface? typeface = null;
        if (ResolveFontFamily(styleSource) is { Length: > 0 } familyName)
        {
            typeface = SkiaTypeface.FromFamilyName(familyName);
        }

        try
        {
            using var paint = new SkiaPaint
            {
                Typeface = typeface,
                TextSize = (float)ResolveFontSize(styleSource)
            };
            return paint.MeasureText(text);
        }
        finally
        {
            typeface?.Dispose();
        }
    }

    private static double ResolveFontSize(element element)
    {
        var inherited = element.ParentElement is { } parent
            ? ResolveFontSize(parent)
            : 16D;

        if (element.IsSet(element.font_sizeProperty))
        {
            return ConvertFontSizeToPixels(element.font_size, inherited);
        }

        return TryGetStyleDeclaration(element.style, "font-size", out var styleFontSize)
            && TryParseSvgUnit(styleFontSize, out var styleFontSizeUnit)
            ? ConvertFontSizeToPixels(styleFontSizeUnit, inherited)
            : inherited;
    }

    private static double ConvertFontSizeToPixels(Svg.SvgUnit unit, double inherited)
    {
        if (unit.IsEmpty || unit.IsNone || unit.Value <= 0f)
        {
            return inherited;
        }

        return unit.Type switch
        {
            Svg.SvgUnitType.Pixel or Svg.SvgUnitType.User => unit.Value,
            Svg.SvgUnitType.Em => inherited * unit.Value,
            Svg.SvgUnitType.Ex => inherited * unit.Value * 0.5D,
            Svg.SvgUnitType.Percentage => inherited * unit.Value / 100D,
            Svg.SvgUnitType.Inch => unit.Value * 96D,
            Svg.SvgUnitType.Centimeter => unit.Value * 96D / 2.54D,
            Svg.SvgUnitType.Millimeter => unit.Value * 96D / 25.4D,
            Svg.SvgUnitType.Pica => unit.Value * 16D,
            Svg.SvgUnitType.Point => unit.Value * 96D / 72D,
            _ => inherited
        };
    }

    private static string? ResolveFontFamily(element element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            var family = current.IsSet(element.font_familyProperty)
                ? current.font_family
                : null;
            if (string.IsNullOrWhiteSpace(family)
                && TryGetStyleDeclaration(current.style, "font-family", out var styleFamily))
            {
                family = styleFamily;
            }

            if (string.IsNullOrWhiteSpace(family))
            {
                continue;
            }

            return family.Split(',')[0].Trim().Trim('\'', '"');
        }

        return null;
    }

    private static bool TryGetStyleDeclaration(string? style, string propertyName, out string? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        foreach (var declaration in style.Split(';'))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = declaration[..separator].Trim();
            if (!string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = declaration[(separator + 1)..].Trim();
            return value.Length > 0;
        }

        return false;
    }

    private static bool TryParseSvgUnit(string? value, out Svg.SvgUnit unit)
    {
        unit = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (new Svg.SvgUnitConverter().ConvertFromString(value.Trim()) is Svg.SvgUnit parsed)
            {
                unit = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return false;
    }

    private static Rect TransformPictureBoundsToControl(Rect pictureBounds, Matrix3x2 pictureToControl)
    {
        var topLeft = Vector2.Transform(new Vector2((float)pictureBounds.Left, (float)pictureBounds.Top), pictureToControl);
        var topRight = Vector2.Transform(new Vector2((float)pictureBounds.Right, (float)pictureBounds.Top), pictureToControl);
        var bottomRight = Vector2.Transform(new Vector2((float)pictureBounds.Right, (float)pictureBounds.Bottom), pictureToControl);
        var bottomLeft = Vector2.Transform(new Vector2((float)pictureBounds.Left, (float)pictureBounds.Bottom), pictureToControl);

        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private void InvalidateDrawingSurface()
    {
        _drawingSurface.InvalidateSurface();
    }

    private void OnDrawingSurfacePaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
    {
        Render(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    private static HostedControlEntry? CreateHostedControlEntry(element element, IHostedControlElement host)
    {
        return host.HostedControl is View view
            ? new HostedControlEntry(element, view, host)
            : null;
    }

    private static bool IsInlineHostedControl(HostedControlEntry entry)
    {
        return GetInlineLayoutRoot(entry.Element) is not null;
    }

    private enum InlineCoordinateAxis
    {
        X,
        Y
    }

    private readonly record struct HostedControlEntry(element Element, View View, IHostedControlElement Host);
}
