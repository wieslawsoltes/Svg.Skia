using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using SkiaCanvas = SkiaSharp.SKCanvas;
using SkiaFont = SkiaSharp.SKFont;
using SkiaTypeface = SkiaSharp.SKTypeface;

namespace SvgML;

public partial class svg
{
    private readonly Grid _layoutRoot = new();
    private SvgDrawingSurface? _drawingSurface;
    private readonly List<HostedControlEntry> _hostedControlEntries = [];
    private readonly Dictionary<UIElement, Border> _hostedControlPresenters = [];

    private void InitializeHostedControls()
    {
        _drawingSurface = new SvgDrawingSurface(this);
        _drawingSurface.IsHitTestVisible = false;
        _layoutRoot.Children.Add(_drawingSurface);
        Content = _layoutRoot;
    }

    private void SynchronizeHostedControls()
    {
        var desiredEntries = HostedControlTree
            .Enumerate(this)
            .Select(static entry => CreateHostedControlEntry(entry.Element, entry.Host))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!.Value)
            .ToList();

        var desiredControls = new HashSet<UIElement>(desiredEntries.Select(static entry => entry.Control));

        foreach (var control in _hostedControlPresenters.Keys.Where(control => !desiredControls.Contains(control)).ToList())
        {
            DisposeHostedControlPresenter(control);
        }

        foreach (var control in desiredControls)
        {
            _ = GetOrCreateHostedControlPresenter(control);
        }

        _hostedControlEntries.Clear();
        _hostedControlEntries.AddRange(desiredEntries);
        InvalidateArrange();
    }

    private void ArrangeHostedControls()
    {
        foreach (var entry in _hostedControlEntries)
        {
            var bounds = GetHostedControlBounds(entry);
            var presenter = GetOrCreateHostedControlPresenter(entry.Control);
            if (bounds.Width <= 0D || bounds.Height <= 0D || !IsLoaded)
            {
                HideHostedControlPresenter(presenter);
                continue;
            }

            presenter.Visibility = Visibility.Visible;
            presenter.HorizontalAlignment = HorizontalAlignment.Left;
            presenter.VerticalAlignment = VerticalAlignment.Top;
            presenter.Margin = new Thickness(0D);
            presenter.Width = bounds.Width;
            presenter.Height = bounds.Height;
            presenter.Measure(new Size(bounds.Width, bounds.Height));
            presenter.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
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
            return bounds;
        }

        entry.Control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = entry.Control.DesiredSize;
        var width = desired.Width > 0D ? desired.Width : bounds.Width;
        var height = desired.Height > 0D ? desired.Height : bounds.Height;
        return new Rect(bounds.X, bounds.Bottom - height, width, height);
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
        return TryLocateInlinePictureBounds(layoutRoot, inlineElement, host, ref currentX, ref currentY, out bounds);
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
        out Rect bounds)
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
                        return true;
                    }

                    currentX += size.Width;
                    break;

                case text_base nestedText:
                    var childX = currentX;
                    var childY = currentY;
                    if (TryLocateInlinePictureBounds(nestedText, target, targetHost, ref childX, ref childY, out bounds))
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

    private static void ApplyTextPosition(text_base textBase, ref double currentX, ref double currentY)
    {
        if (TryParseFirstCoordinate(textBase.x, out var x))
        {
            currentX = x;
        }

        if (TryParseFirstCoordinate(textBase.y, out var y))
        {
            currentY = y;
        }

        if (TryParseFirstCoordinate(textBase.dx, out var dx))
        {
            currentX += dx;
        }

        if (TryParseFirstCoordinate(textBase.dy, out var dy))
        {
            currentY += dy;
        }
    }

    private static bool TryParseFirstCoordinate(string? value, out double coordinate)
    {
        coordinate = 0D;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parsed = numbers.Parse(value).Number;
        if (parsed is null || parsed.Count == 0)
        {
            return false;
        }

        coordinate = parsed[0];
        return true;
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
            using var font = typeface is null
                ? new SkiaFont { Size = (float)ResolveFontSize(styleSource) }
                : new SkiaFont(typeface, (float)ResolveFontSize(styleSource));
            return font.MeasureText(text);
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

        if (element.ReadLocalValue(element.font_sizeProperty) != DependencyProperty.UnsetValue)
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
            var family = current.ReadLocalValue(element.font_familyProperty) != DependencyProperty.UnsetValue
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
        _drawingSurface?.Invalidate();
    }

    private void RenderPicture(SkiaCanvas canvas, Size area)
    {
        var picture = _picture;
        if (picture is null)
        {
            return;
        }

        if (!SvgRenderLayout.TryCreateRenderInfo(
                new SvgSize(area.Width, area.Height),
                new SvgRect(
                    picture.CullRect.Left,
                    picture.CullRect.Top,
                    picture.CullRect.Width,
                    picture.CullRect.Height),
                Stretch,
                StretchDirection,
                out var renderInfo))
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(ToSKRect(renderInfo.DestinationRect));
        var matrix = ToSKMatrix(renderInfo.Matrix);
        canvas.Concat(in matrix);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private static HostedControlEntry? CreateHostedControlEntry(element element, IHostedControlElement host)
    {
        return host.HostedControl is UIElement control
            ? new HostedControlEntry(element, control, host)
            : null;
    }

    private static bool IsInlineHostedControl(HostedControlEntry entry)
    {
        return GetInlineLayoutRoot(entry.Element) is not null;
    }

    private Border GetOrCreateHostedControlPresenter(UIElement control)
    {
        if (_hostedControlPresenters.TryGetValue(control, out var presenter))
        {
            return presenter;
        }

        presenter = new Border
        {
            Child = control,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        Canvas.SetZIndex(presenter, 1);
        _hostedControlPresenters.Add(control, presenter);
        _layoutRoot.Children.Add(presenter);
        return presenter;
    }

    private static void HideHostedControlPresenter(Border presenter)
    {
        presenter.Visibility = Visibility.Collapsed;
    }

    private void CloseHostedControlPresenters()
    {
        foreach (var presenter in _hostedControlPresenters.Values)
        {
            presenter.Visibility = Visibility.Collapsed;
        }
    }

    private void DisposeHostedControlPresenter(UIElement control)
    {
        if (!_hostedControlPresenters.Remove(control, out var presenter))
        {
            return;
        }

        presenter.Child = null;
        _layoutRoot.Children.Remove(presenter);
    }

    private sealed class SvgDrawingSurface(svg owner) : SKCanvasElement
    {
        protected override void RenderOverride(SkiaCanvas canvas, Size area)
        {
            owner.RenderPicture(canvas, area);
        }
    }

    private readonly record struct HostedControlEntry(element Element, UIElement Control, IHostedControlElement Host);
}
