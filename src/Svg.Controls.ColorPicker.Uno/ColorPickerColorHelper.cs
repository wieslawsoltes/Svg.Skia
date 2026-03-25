using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno;

public static class ColorPickerColorHelper
{
    public static Color FromHsv(double hue, double saturation, double value, double alpha = 1.0)
    {
        hue = NormalizeHue(hue);
        saturation = Clamp01(saturation);
        value = Clamp01(value);
        alpha = Clamp01(alpha);

        var chroma = value * saturation;
        var section = hue / 60.0;
        var secondary = chroma * (1.0 - Math.Abs((section % 2.0) - 1.0));
        var match = value - chroma;

        var (r, g, b) = section switch
        {
            >= 0.0 and < 1.0 => (chroma, secondary, 0.0),
            >= 1.0 and < 2.0 => (secondary, chroma, 0.0),
            >= 2.0 and < 3.0 => (0.0, chroma, secondary),
            >= 3.0 and < 4.0 => (0.0, secondary, chroma),
            >= 4.0 and < 5.0 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary)
        };

        return Color.FromArgb(
            ToByte(alpha * 255.0),
            ToByte((r + match) * 255.0),
            ToByte((g + match) * 255.0),
            ToByte((b + match) * 255.0));
    }

    public static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue;
        if (delta == 0.0)
        {
            hue = 0.0;
        }
        else if (max == r)
        {
            hue = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (max == g)
        {
            hue = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            hue = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (hue < 0.0)
        {
            hue += 360.0;
        }

        var saturation = max == 0.0 ? 0.0 : delta / max;
        return (NormalizeHue(hue), saturation, max);
    }

    public static (double Hue, double Saturation, double Lightness) ToHsl(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2.0;

        if (delta == 0.0)
        {
            return (0.0, 0.0, lightness);
        }

        var saturation = delta / (1.0 - Math.Abs((2.0 * lightness) - 1.0));
        double hue;
        if (max == r)
        {
            hue = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (max == g)
        {
            hue = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            hue = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (hue < 0.0)
        {
            hue += 360.0;
        }

        return (NormalizeHue(hue), Clamp01(saturation), Clamp01(lightness));
    }

    public static Color FromHsl(double hue, double saturation, double lightness, double alpha = 1.0)
    {
        hue = NormalizeHue(hue);
        saturation = Clamp01(saturation);
        lightness = Clamp01(lightness);
        alpha = Clamp01(alpha);

        var chroma = (1.0 - Math.Abs((2.0 * lightness) - 1.0)) * saturation;
        var section = hue / 60.0;
        var secondary = chroma * (1.0 - Math.Abs((section % 2.0) - 1.0));
        var match = lightness - (chroma / 2.0);

        var (r, g, b) = section switch
        {
            >= 0.0 and < 1.0 => (chroma, secondary, 0.0),
            >= 1.0 and < 2.0 => (secondary, chroma, 0.0),
            >= 2.0 and < 3.0 => (0.0, chroma, secondary),
            >= 3.0 and < 4.0 => (0.0, secondary, chroma),
            >= 4.0 and < 5.0 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary)
        };

        return Color.FromArgb(
            ToByte(alpha * 255.0),
            ToByte((r + match) * 255.0),
            ToByte((g + match) * 255.0),
            ToByte((b + match) * 255.0));
    }

    public static Color WithAlpha(Color color, double alpha)
    {
        return Color.FromArgb(ToByte(Clamp01(alpha) * 255.0), color.R, color.G, color.B);
    }

    public static int ToPercent(byte component)
    {
        return (int)Math.Round(component / 255.0 * 100.0);
    }

    public static byte FromPercent(double percent)
    {
        return ToByte(Clamp01(percent / 100.0) * 255.0);
    }

    public static string ToHexRgb(Color color)
    {
        return $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static bool TryParseHex(string? text, byte fallbackAlpha, out Color color)
    {
        color = Color.FromArgb(fallbackAlpha, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var raw = text.Trim().TrimStart('#');
        if (raw.Length == 6 && uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            color = Color.FromArgb(
                fallbackAlpha,
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF));
            return true;
        }

        if (raw.Length == 8 && uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            color = Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
            return true;
        }

        return false;
    }

    private static double NormalizeHue(double hue)
    {
        hue %= 360.0;
        return hue < 0.0 ? hue + 360.0 : hue;
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
