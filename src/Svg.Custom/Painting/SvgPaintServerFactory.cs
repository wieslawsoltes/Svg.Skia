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
            else if (colorValue.Equals("context-fill", StringComparison.OrdinalIgnoreCase))
                return new SvgContextPaintServer(SvgContextPaintKind.Fill);
            else if (colorValue.Equals("context-stroke", StringComparison.OrdinalIgnoreCase))
                return new SvgContextPaintServer(SvgContextPaintKind.Stroke);
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

            // Otherwise try and parse as colour. SvgColourConverter only accepts a subset
            // of CSS Color values; normalize deterministic CSS Color 4 sRGB forms here.
            if (TryParseCssConcreteColor(colorValue, out var cssColor))
            {
                return new SvgColourServer(cssColor);
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

                var contextPaint = value as SvgContextPaintServer;
                if (contextPaint != null)
                {
                    return contextPaint.ToString();
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

        internal static bool TryParseCssConcreteColor(string value, out Color color)
        {
            color = Color.Empty;
            value = value.Trim();

            if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                color = Color.FromArgb(0, 0, 0, 0);
                return true;
            }

            if (SvgSystemColorResolver.TryGetColor(value, out color))
            {
                return true;
            }

            if (TryGetLegacyIccColorFallback(value, out var fallbackColor) &&
                TryParseCssConcreteColor(fallbackColor, out color))
            {
                return true;
            }

            if (TryParseHexColorWithAlpha(value, out color) ||
                TryParseCssFunctionalColor(value, out color))
            {
                return true;
            }

            try
            {
                var converted = _colourConverter.ConvertFrom(value);
                if (converted is Color parsed)
                {
                    color = parsed;
                    return true;
                }
            }
            catch
            {
                // Unsupported CSS color syntax falls back to the caller's conversion path.
            }

            color = Color.Empty;
            return false;
        }

        private static bool TryGetLegacyIccColorFallback(string value, out string fallbackColor)
        {
            fallbackColor = string.Empty;

            var depth = 0;
            var quote = '\0';
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (quote != '\0')
                {
                    if (ch == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth != 0 ||
                    !IsFunctionAt(value, i, "icc-color") ||
                    i == 0 ||
                    !char.IsWhiteSpace(value[i - 1]))
                {
                    continue;
                }

                var openParenthesis = i + "icc-color".Length;
                if (!TryFindCssFunctionEnd(value, openParenthesis, out var closeParenthesis))
                {
                    return false;
                }

                for (var tail = closeParenthesis + 1; tail < value.Length; tail++)
                {
                    if (!char.IsWhiteSpace(value[tail]))
                    {
                        return false;
                    }
                }

                fallbackColor = value.Substring(0, i).Trim();
                return fallbackColor.Length > 0;
            }

            return false;
        }

        private static bool IsFunctionAt(string value, int index, string functionName)
        {
            if (index + functionName.Length >= value.Length ||
                string.Compare(value, index, functionName, 0, functionName.Length, ignoreCase: true, CultureInfo.InvariantCulture) != 0)
            {
                return false;
            }

            var openParenthesisIndex = index + functionName.Length;
            return value[openParenthesisIndex] == '(';
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

        private static bool TryParseCssFunctionalColor(string value, out Color color)
        {
            return TryParseCssRgbColorFunction(value, out color) ||
                   TryParseCssHslColorFunction(value, out color) ||
                   TryParseCssHwbColorFunction(value, out color);
        }

        private static bool TryParseCssRgbColorFunction(string value, out Color color)
        {
            color = Color.Empty;
            if (!TryGetCssFunctionContent(value, "rgb", "rgba", out var content) ||
                !TrySplitCssColorComponents(content, allowCommaSyntax: true, out var components, out var alphaToken) ||
                components.Count != 3)
            {
                return false;
            }

            if (!TryParseCssRgbComponent(components[0], out var red) ||
                !TryParseCssRgbComponent(components[1], out var green) ||
                !TryParseCssRgbComponent(components[2], out var blue) ||
                !TryParseCssAlpha(alphaToken, out var alpha))
            {
                return false;
            }

            color = Color.FromArgb(alpha, red, green, blue);
            return true;
        }

        private static bool TryParseCssHslColorFunction(string value, out Color color)
        {
            color = Color.Empty;
            if (!TryGetCssFunctionContent(value, "hsl", "hsla", out var content) ||
                !TrySplitCssColorComponents(content, allowCommaSyntax: true, out var components, out var alphaToken) ||
                components.Count != 3)
            {
                return false;
            }

            if (!TryParseCssHue(components[0], out var hue) ||
                !TryParseCssPercentage01(components[1], out var saturation) ||
                !TryParseCssPercentage01(components[2], out var lightness) ||
                !TryParseCssAlpha(alphaToken, out var alpha))
            {
                return false;
            }

            color = CreateColorFromHsl(hue, saturation, lightness, alpha);
            return true;
        }

        private static bool TryParseCssHwbColorFunction(string value, out Color color)
        {
            color = Color.Empty;
            if (!TryGetCssFunctionContent(value, "hwb", out var content) ||
                !TrySplitCssColorComponents(content, allowCommaSyntax: false, out var components, out var alphaToken) ||
                components.Count != 3)
            {
                return false;
            }

            if (!TryParseCssHue(components[0], out var hue) ||
                !TryParseCssPercentage01(components[1], out var whiteness) ||
                !TryParseCssPercentage01(components[2], out var blackness) ||
                !TryParseCssAlpha(alphaToken, out var alpha))
            {
                return false;
            }

            color = CreateColorFromHwb(hue, whiteness, blackness, alpha);
            return true;
        }

        private static bool TryGetCssFunctionContent(string value, string name, out string content)
        {
            content = string.Empty;
            var openParenthesis = value.IndexOf('(');
            if (openParenthesis <= 0 ||
                !TryFindCssFunctionEnd(value, openParenthesis, out var closeParenthesis) ||
                closeParenthesis != value.Length - 1)
            {
                return false;
            }

            var functionName = value.Substring(0, openParenthesis).Trim();
            if (!functionName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            content = value.Substring(openParenthesis + 1, closeParenthesis - openParenthesis - 1).Trim();
            return content.Length > 0;
        }

        private static bool TryGetCssFunctionContent(string value, string name, string alias, out string content)
        {
            return TryGetCssFunctionContent(value, name, out content) ||
                   TryGetCssFunctionContent(value, alias, out content);
        }

        private static bool TryFindCssFunctionEnd(string value, int openParenthesisIndex, out int closeParenthesisIndex)
        {
            closeParenthesisIndex = -1;
            var quote = '\0';
            var escape = false;
            var depth = 0;
            for (var i = openParenthesisIndex; i < value.Length; i++)
            {
                var ch = value[i];
                if (quote != '\0')
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (ch == '\\')
                    {
                        escape = true;
                    }
                    else if (ch == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeParenthesisIndex = i;
                        return true;
                    }

                    if (depth < 0)
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        private static bool TrySplitCssColorComponents(
            string value,
            bool allowCommaSyntax,
            out System.Collections.Generic.List<string> components,
            out string alpha)
        {
            components = new System.Collections.Generic.List<string>(3);
            alpha = null;

            var commaParts = SplitTopLevelCssArguments(value, ',');
            if (commaParts.Count > 1)
            {
                if (!allowCommaSyntax ||
                    commaParts.Count != 3 && commaParts.Count != 4)
                {
                    return false;
                }

                for (var i = 0; i < commaParts.Count; i++)
                {
                    if (ContainsTopLevelSolidus(commaParts[i]))
                    {
                        return false;
                    }
                }

                AddFirstThree(components, commaParts);
                alpha = commaParts.Count == 4 ? commaParts[3] : null;
                return true;
            }

            var tokens = SplitCssColorSpaceTokens(value);
            var slashIndex = tokens.IndexOf("/");
            if (slashIndex >= 0)
            {
                if (slashIndex != 3 ||
                    tokens.Count != 5 ||
                    tokens.LastIndexOf("/") != slashIndex)
                {
                    return false;
                }

                AddFirstThree(components, tokens);
                alpha = tokens[4];
                return true;
            }

            if (tokens.Count != 3)
            {
                return false;
            }

            components.AddRange(tokens);
            return true;
        }

        private static void AddFirstThree(System.Collections.Generic.List<string> target, System.Collections.Generic.List<string> source)
        {
            target.Add(source[0]);
            target.Add(source[1]);
            target.Add(source[2]);
        }

        private static System.Collections.Generic.List<string> SplitTopLevelCssArguments(string value, char separator)
        {
            var parts = new System.Collections.Generic.List<string>();
            var start = 0;
            var depth = 0;
            var quote = '\0';
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (quote != '\0')
                {
                    if (ch == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (ch == separator && depth == 0)
                {
                    parts.Add(value.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            parts.Add(value.Substring(start).Trim());
            return parts;
        }

        private static System.Collections.Generic.List<string> SplitCssColorSpaceTokens(string value)
        {
            var tokens = new System.Collections.Generic.List<string>();
            var start = -1;
            var depth = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')' && depth > 0)
                {
                    depth--;
                }

                if ((char.IsWhiteSpace(ch) || ch == '/') && depth == 0)
                {
                    if (start >= 0)
                    {
                        tokens.Add(value.Substring(start, i - start));
                        start = -1;
                    }

                    if (ch == '/')
                    {
                        tokens.Add("/");
                    }

                    continue;
                }

                if (start < 0)
                {
                    start = i;
                }
            }

            if (start >= 0)
            {
                tokens.Add(value.Substring(start));
            }

            return tokens;
        }

        private static bool ContainsTopLevelSolidus(string value)
        {
            var depth = 0;
            var quote = '\0';
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (quote != '\0')
                {
                    if (ch == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    quote = ch;
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (ch == '/' && depth == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseCssRgbComponent(string value, out int component)
        {
            component = 0;
            var componentText = value.Trim();
            var isPercent = componentText.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
            {
                componentText = componentText.Substring(0, componentText.Length - 1).Trim();
            }

            if (!float.TryParse(componentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !IsFinite(parsed))
            {
                return false;
            }

            component = ToByte(isPercent ? parsed * 255f / 100f : parsed);
            return true;
        }

        private static bool TryParseCssAlpha(string value, out int alpha)
        {
            alpha = 255;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var alphaText = value.Trim();
            var isPercent = alphaText.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
            {
                alphaText = alphaText.Substring(0, alphaText.Length - 1).Trim();
            }

            if (!float.TryParse(alphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !IsFinite(parsed))
            {
                return false;
            }

            alpha = ToByte(Clamp01(isPercent ? parsed / 100f : parsed) * 255f);
            return true;
        }

        private static bool TryParseCssHue(string value, out float degrees)
        {
            degrees = default;
            value = value.Trim();
            var multiplier = 1f;
            if (value.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 3);
            }
            else if (value.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 4);
                multiplier = 0.9f;
            }
            else if (value.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 3);
                multiplier = 180f / (float)Math.PI;
            }
            else if (value.EndsWith("turn", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 4);
                multiplier = 360f;
            }

            if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !IsFinite(parsed))
            {
                return false;
            }

            degrees = parsed * multiplier;
            return true;
        }

        private static bool TryParseCssPercentage01(string value, out float normalized)
        {
            normalized = default;
            value = value.Trim();
            if (!value.EndsWith("%", StringComparison.Ordinal))
            {
                return false;
            }

            value = value.Substring(0, value.Length - 1).Trim();
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !IsFinite(parsed))
            {
                return false;
            }

            normalized = Clamp01(parsed / 100f);
            return true;
        }

        private static Color CreateColorFromHsl(float hueDegrees, float saturation, float lightness, int alpha)
        {
            var hue = hueDegrees % 360f;
            if (hue < 0f)
            {
                hue += 360f;
            }

            var h = hue / 360f;
            var s = Clamp01(saturation);
            var l = Clamp01(lightness);
            if (Math.Abs(s) <= float.Epsilon)
            {
                var gray = ToByte(l * 255f);
                return Color.FromArgb(alpha, gray, gray, gray);
            }

            var q = l < 0.5f ? l * (1f + s) : l + s - (l * s);
            var p = (2f * l) - q;
            var r = HueToRgb(p, q, h + (1f / 3f));
            var g = HueToRgb(p, q, h);
            var b = HueToRgb(p, q, h - (1f / 3f));

            return Color.FromArgb(alpha, ToByte(r * 255f), ToByte(g * 255f), ToByte(b * 255f));
        }

        private static Color CreateColorFromHwb(float hueDegrees, float whiteness, float blackness, int alpha)
        {
            if (whiteness + blackness >= 1f)
            {
                var gray = ToByte(whiteness / (whiteness + blackness) * 255f);
                return Color.FromArgb(alpha, gray, gray, gray);
            }

            var hue = CreateColorFromHsl(hueDegrees, 1f, 0.5f, alpha);
            var factor = 1f - whiteness - blackness;
            return Color.FromArgb(
                alpha,
                ToByte((hue.R / 255f * factor + whiteness) * 255f),
                ToByte((hue.G / 255f * factor + whiteness) * 255f),
                ToByte((hue.B / 255f * factor + whiteness) * 255f));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f)
            {
                t += 1f;
            }

            if (t > 1f)
            {
                t -= 1f;
            }

            if (t < 1f / 6f)
            {
                return p + ((q - p) * 6f * t);
            }

            if (t < 1f / 2f)
            {
                return q;
            }

            if (t < 2f / 3f)
            {
                return p + ((q - p) * ((2f / 3f) - t) * 6f);
            }

            return p;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int ToByte(float value)
        {
            return (int)Math.Round(Clamp(value, 0f, 255f));
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
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
