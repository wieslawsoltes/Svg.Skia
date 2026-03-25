using Svg.Controls.ColorPicker.Uno;
using Svg.Controls.ColorPicker.Uno.Models;
using Windows.UI;
using Xunit;

namespace Svg.Controls.ColorPicker.Uno.UnitTests;

public class ColorPickerColorHelperTests
{
    [Fact]
    public void HsvRoundTrip_PreservesColorComponentsWithinTolerance()
    {
        var original = Color.FromArgb(191, 224, 65, 65);

        var hsv = ColorPickerColorHelper.ToHsv(original);
        var roundTripped = ColorPickerColorHelper.FromHsv(hsv.Hue, hsv.Saturation, hsv.Value, original.A / 255.0);

        Assert.InRange(Math.Abs(roundTripped.R - original.R), 0, 1);
        Assert.InRange(Math.Abs(roundTripped.G - original.G), 0, 1);
        Assert.InRange(Math.Abs(roundTripped.B - original.B), 0, 1);
        Assert.Equal(original.A, roundTripped.A);
    }

    [Fact]
    public void TryParseHex_SupportsRgbAndArgbInputs()
    {
        Assert.True(ColorPickerColorHelper.TryParseHex("FF3366", 128, out var rgbColor));
        Assert.Equal(Color.FromArgb(128, 255, 51, 102), rgbColor);

        Assert.True(ColorPickerColorHelper.TryParseHex("CC112233", 255, out var argbColor));
        Assert.Equal(Color.FromArgb(0xCC, 0x11, 0x22, 0x33), argbColor);
    }

    [Fact]
    public void ColorSwatchItem_ExposesStrokeMetadataFromTarget()
    {
        var swatch = new ColorSwatchItem(
            Color.FromArgb(255, 17, 24, 39),
            label: "Outline",
            target: PaintStyleTarget.Stroke,
            strokeWidth: 3.5);

        Assert.False(swatch.SupportsFill);
        Assert.True(swatch.SupportsStroke);
        Assert.Equal("3.5 px", swatch.StrokeWidthLabel);
        Assert.Equal("Stroke style · 3.5 px", swatch.StyleSummary);
    }

    [Fact]
    public void ColorSwatchItem_UsesPaintModeSummaryForNonSolidStyles()
    {
        var swatch = new ColorSwatchItem(
            Color.FromArgb(255, 167, 139, 250),
            label: "Aurora",
            target: PaintStyleTarget.Fill,
            paintMode: ColorPickerPaintMode.LinearGradient);

        Assert.Equal("Linear gradient", swatch.ModeLabel);
        Assert.Equal("Linear gradient · 100%", swatch.StyleSummary);
    }

    [Fact]
    public void CreateDefaultGradientStops_SeedsStartAndEndStops()
    {
        var baseColor = Color.FromArgb(255, 24, 190, 171);

        var stops = ColorPickerPaintModeHelper.CreateDefaultGradientStops(baseColor);

        Assert.Equal(2, stops.Count);
        Assert.Equal(baseColor, stops[0].Color);
        Assert.Equal(0.0, stops[0].Offset);
        Assert.Equal("Start", stops[0].Label);
        Assert.Equal(1.0, stops[1].Offset);
        Assert.Equal("End", stops[1].Label);
    }

    [Fact]
    public void BuildEyeDropperPalette_DeduplicatesAndKeepsCurrentSelectionFirst()
    {
        var current = Color.FromArgb(255, 224, 65, 65);
        var duplicate = new ColorSwatchItem(current, label: "Duplicate current");
        var swatches = new[]
        {
            duplicate,
            new ColorSwatchItem(Color.FromArgb(255, 24, 190, 171), label: "Seafoam")
        };
        var libraries = new[]
        {
            new ColorSwatchItem(Color.FromArgb(255, 24, 190, 171), label: "Seafoam style"),
            new ColorSwatchItem(Color.FromArgb(255, 17, 24, 39), label: "Ink")
        };

        var palette = ColorPickerPaintModeHelper.BuildEyeDropperPalette(current, swatches, libraries);

        Assert.Equal("Current selection", palette[0].Label);
        Assert.Collection(
            palette,
            item => Assert.Equal(current, item.Color),
            item => Assert.Equal(Color.FromArgb(255, 24, 190, 171), item.Color),
            item => Assert.Equal(Color.FromArgb(255, 17, 24, 39), item.Color));
    }
}
