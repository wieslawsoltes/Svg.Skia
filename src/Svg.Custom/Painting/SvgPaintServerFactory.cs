using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace Svg
{
    /// <summary>
    /// Svg.Custom override of the upstream paint server factory.
    ///
    /// The original SVG source created deferred paint servers from <c>url(...)</c> values
    /// without consistently threading the parsing document through every constructor path.
    /// That was usually harmless until retained-scene <c>&lt;use&gt;</c> expansion started
    /// temporarily reparenting referenced elements: once the owning document changes, deferred
    /// gradients and patterns can resolve against the wrong document.
    ///
    /// This copy keeps the original parsing behavior intact but always passes the parse-time
    /// <see cref="SvgDocument"/> into <see cref="SvgDeferredPaintServer"/> so the override in
    /// <c>SvgDeferredPaintServer.cs</c> can resolve against the same document the upstream parser
    /// saw, without needing a direct submodule modification.
    /// </summary>
    internal class SvgPaintServerFactory : TypeConverter
    {
        private static readonly SvgColourConverter _colourConverter;

        static SvgPaintServerFactory()
        {
            _colourConverter = new SvgColourConverter();
        }

        public static SvgPaintServer Create(string value, SvgDocument document)
        {
            if (value == null)
                return SvgPaintServer.NotSet;

            var colorValue = value.Trim();
            if (SvgCssVariableResolver.TryResolveFallbacks(colorValue, out var resolvedColorValue))
            {
                colorValue = resolvedColorValue.Trim();
            }

            // If it's pointing to a paint server
            if (string.IsNullOrEmpty(colorValue))
                return SvgPaintServer.NotSet;
            else if (colorValue.Equals("none", StringComparison.OrdinalIgnoreCase))
                return SvgPaintServer.None;
            else if (colorValue.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
                // Keep the parse-time document for consistency with url(...) paint servers.
                return new SvgDeferredPaintServer(document, "currentColor");
            else if (colorValue.Equals("inherit", StringComparison.OrdinalIgnoreCase))
                return SvgPaintServer.Inherit;
            else if (colorValue.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                var nextIndex = colorValue.IndexOf(')', 4) + 1;

                // Malformed url, missing closing parenthesis
                if (nextIndex == 0)
                    // Upstream used the overload that dropped the source document here.
                    // The local override preserves it so deferred resolution behaves the
                    // same for malformed and well-formed values once the runtime asks for it.
                    return new SvgDeferredPaintServer(document, colorValue + ")", null);

                var id = colorValue.Substring(0, nextIndex);

                colorValue = colorValue.Substring(nextIndex).Trim();
                var fallbackServer = string.IsNullOrEmpty(colorValue) ? null : Create(colorValue, document);

                // This is the main behavioral difference from the upstream file: thread the
                // parse-time document into the deferred server so later resolution stays bound
                // to the original document rather than whichever temporary owner is active.
                return new SvgDeferredPaintServer(document, id, fallbackServer);
            }

            // Otherwise try and parse as colour. SvgColourConverter only accepts SVG 1.1
            // hex colours (#RGB/#RRGGBB); support CSS Color 4 alpha forms here because
            // CSS style declarations can legally feed #RGBA/#RRGGBBAA into this factory.
            if (TryParseHexColorWithAlpha(colorValue, out var hexColor))
            {
                return new SvgColourServer(hexColor);
            }

            var converted = _colourConverter.ConvertFrom(colorValue);
            return converted is Color color
                ? new SvgColourServer(color)
                : throw new InvalidCastException();
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
                return Create((string)value, (SvgDocument)context);

            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                return true;
            }

            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                // check for constant
                if (value == SvgPaintServer.None || value == SvgPaintServer.Inherit || value == SvgPaintServer.NotSet)
                    return value.ToString();

                var colourServer = value as SvgColourServer;
                if (colourServer != null)
                {
                    if (colourServer.Colour.A != 255)
                    {
                        return FormatHexColor(colourServer.Colour);
                    }

                    return new SvgColourConverter().ConvertTo(colourServer.Colour, typeof(string));
                }

                var deferred = value as SvgDeferredPaintServer;
                if (deferred != null)
                {
                    return deferred.ToString();
                }

                if (value != null)
                {
                    return string.Format(CultureInfo.InvariantCulture, "url(#{0})", ((SvgPaintServer)value).ID);
                }
                else
                {
                    return "none";
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        private static bool TryParseHexColorWithAlpha(string value, out Color color)
        {
            color = Color.Empty;

            if (value.Length != 5 && value.Length != 9)
            {
                return false;
            }

            if (value[0] != '#')
            {
                return false;
            }

            if (value.Length == 5)
            {
                var red = FromHex(value[1]);
                var green = FromHex(value[2]);
                var blue = FromHex(value[3]);
                var alpha = FromHex(value[4]);

                if (red < 0 || green < 0 || blue < 0 || alpha < 0)
                {
                    return false;
                }

                color = Color.FromArgb(
                    ExpandHex(alpha),
                    ExpandHex(red),
                    ExpandHex(green),
                    ExpandHex(blue));
                return true;
            }

            if (!TryParseHexByte(value, 1, out var r) ||
                !TryParseHexByte(value, 3, out var g) ||
                !TryParseHexByte(value, 5, out var b) ||
                !TryParseHexByte(value, 7, out var a))
            {
                return false;
            }

            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        private static bool TryParseHexByte(string value, int index, out byte component)
        {
            component = 0;

            var high = FromHex(value[index]);
            var low = FromHex(value[index + 1]);

            if (high < 0 || low < 0)
            {
                return false;
            }

            component = (byte)((high << 4) | low);
            return true;
        }

        private static byte ExpandHex(int value)
        {
            return (byte)((value << 4) | value);
        }

        private static int FromHex(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }

            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }

            return -1;
        }

        private static string FormatHexColor(Color color)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "#{0:X2}{1:X2}{2:X2}{3:X2}",
                color.R,
                color.G,
                color.B,
                color.A);
        }
    }
}
