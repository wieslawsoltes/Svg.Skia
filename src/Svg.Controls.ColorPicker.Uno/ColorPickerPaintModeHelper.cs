using Svg.Controls.ColorPicker.Uno.Models;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno;

public static class ColorPickerPaintModeHelper
{
    public static bool IsGradientMode(ColorPickerPaintMode mode) =>
        mode is ColorPickerPaintMode.LinearGradient or ColorPickerPaintMode.RadialGradient;

    public static bool IsAssetMode(ColorPickerPaintMode mode) =>
        mode is ColorPickerPaintMode.ImagePaint or ColorPickerPaintMode.VideoPaint or ColorPickerPaintMode.ImageAsset;

    public static string GetDisplayName(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.Solid => "Solid",
        ColorPickerPaintMode.LinearGradient => "Linear gradient",
        ColorPickerPaintMode.RadialGradient => "Radial gradient",
        ColorPickerPaintMode.ImagePaint => "Image paint",
        ColorPickerPaintMode.VideoPaint => "Video paint",
        ColorPickerPaintMode.ImageAsset => "Image asset",
        _ => "Paint"
    };

    public static IReadOnlyList<ColorPickerGradientStop> CreateDefaultGradientStops(Color baseColor)
    {
        var hsl = ColorPickerColorHelper.ToHsl(baseColor);
        var secondaryHue = (hsl.Hue + 28.0) % 360.0;
        var secondaryLightness = hsl.Lightness < 0.55
            ? Math.Clamp(hsl.Lightness + 0.24, 0.0, 1.0)
            : Math.Clamp(hsl.Lightness - 0.24, 0.0, 1.0);
        var secondary = ColorPickerColorHelper.FromHsl(
            secondaryHue,
            Math.Clamp(hsl.Saturation * 0.92, 0.0, 1.0),
            secondaryLightness,
            baseColor.A / 255.0);

        return
        [
            new ColorPickerGradientStop(baseColor, 0.0, "Start"),
            new ColorPickerGradientStop(secondary, 1.0, "End")
        ];
    }

    public static IReadOnlyList<ColorSwatchItem> BuildEyeDropperPalette(
        Color selectedColor,
        IEnumerable<ColorSwatchItem>? swatches,
        IEnumerable<ColorSwatchItem>? libraryStyles)
    {
        var palette = new List<ColorSwatchItem>();
        var seen = new HashSet<uint>();

        void Add(ColorSwatchItem item)
        {
            var argb = ToArgb(item.Color);
            if (seen.Add(argb))
            {
                palette.Add(item);
            }
        }

        Add(new ColorSwatchItem(selectedColor, label: "Current selection"));

        foreach (var swatch in swatches ?? Enumerable.Empty<ColorSwatchItem>())
        {
            Add(swatch);
        }

        foreach (var style in libraryStyles ?? Enumerable.Empty<ColorSwatchItem>())
        {
            Add(style);
        }

        return palette;
    }

    private static uint ToArgb(Color color)
    {
        return ((uint)color.A << 24)
             | ((uint)color.R << 16)
             | ((uint)color.G << 8)
             | color.B;
    }
}
