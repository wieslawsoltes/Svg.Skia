using System;
using System.Globalization;

namespace Svg
{
    /// <summary>
    /// Resolves stop-color and stop-opacity with the inheritance rules the SVG 1.1 paint server
    /// model expects.
    ///
    /// The original upstream attribute walker mostly works for typed properties, but gradients hit
    /// two corner cases that matter for W3C parity:
    ///
    /// 1. <c>stop-color="inherit"</c> is parsed as the shared <see cref="SvgPaintServer.Inherit"/>
    ///    sentinel. That sentinel is also a <see cref="SvgColourServer"/>, so generic callers can
    ///    accidentally treat it as a concrete black color instead of the inherit keyword.
    /// 2. <c>stop-opacity="inherit"</c> often survives only as a raw presentation-attribute string
    ///    because the strongly typed float property cannot store the keyword directly.
    ///
    /// Svg.Skia only needs this behavior while reading gradients, so the fix lives in Svg.Custom:
    /// we keep the submodule pristine and still reproduce Chrome/W3C stop inheritance semantics.
    /// </summary>
    internal static class SvgStopInheritanceResolver
    {
        public static SvgPaintServer ResolveStopColor(SvgElement element, bool inherited, SvgPaintServer defaultValue)
        {
            if (TryGetExplicitStopColor(element, out var paintServer, out var shouldInherit))
            {
                return shouldInherit
                    ? ResolveParentStopColor(element.Parent, defaultValue)
                    : paintServer;
            }

            return inherited
                ? ResolveParentStopColor(element.Parent, defaultValue)
                : defaultValue;
        }

        public static float ResolveStopOpacity(SvgElement element, bool inherited, float defaultValue)
        {
            if (TryGetExplicitStopOpacity(element, out var opacity, out var shouldInherit))
            {
                return shouldInherit
                    ? ResolveParentStopOpacity(element.Parent, defaultValue)
                    : opacity;
            }

            return inherited
                ? ResolveParentStopOpacity(element.Parent, defaultValue)
                : defaultValue;
        }

        private static SvgPaintServer ResolveParentStopColor(SvgElement element, SvgPaintServer defaultValue)
        {
            return element is { }
                ? ResolveStopColor(element, false, defaultValue)
                : defaultValue;
        }

        private static float ResolveParentStopOpacity(SvgElement element, float defaultValue)
        {
            return element is { }
                ? ResolveStopOpacity(element, false, defaultValue)
                : defaultValue;
        }

        private static bool TryGetExplicitStopColor(SvgElement element, out SvgPaintServer paintServer, out bool shouldInherit)
        {
            paintServer = null;
            shouldInherit = false;

            if (element.Attributes.ContainsKey("stop-color"))
            {
                var attributeValue = element.Attributes.GetAttribute<object>("stop-color");
                if (ReferenceEquals(attributeValue, SvgPaintServer.Inherit))
                {
                    shouldInherit = true;
                    return true;
                }

                if (attributeValue is SvgPaintServer typedPaintServer)
                {
                    paintServer = typedPaintServer;
                    return true;
                }
            }

            if (element.TryGetAttribute("stop-color", out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
            {
                if (string.Equals(rawValue.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
                {
                    shouldInherit = true;
                    return true;
                }

                paintServer = SvgPaintServerFactory.Create(rawValue, element.OwnerDocument);
                return true;
            }

            return false;
        }

        private static bool TryGetExplicitStopOpacity(SvgElement element, out float opacity, out bool shouldInherit)
        {
            opacity = 1f;
            shouldInherit = false;

            if (element.Attributes.ContainsKey("stop-opacity"))
            {
                var attributeValue = element.Attributes.GetAttribute<object>("stop-opacity");
                switch (attributeValue)
                {
                    case float typedOpacity:
                        opacity = ClampOpacity(typedOpacity);
                        return true;
                    case double typedOpacity:
                        opacity = ClampOpacity((float)typedOpacity);
                        return true;
                    case string rawOpacity when string.Equals(rawOpacity.Trim(), "inherit", StringComparison.OrdinalIgnoreCase):
                        shouldInherit = true;
                        return true;
                }
            }

            if (element.TryGetAttribute("stop-opacity", out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
            {
                if (string.Equals(rawValue.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
                {
                    shouldInherit = true;
                    return true;
                }

                if (TryParseOpacity(rawValue, out opacity))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseOpacity(string rawValue, out float opacity)
        {
            if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                opacity = ClampOpacity(parsed);
                return true;
            }

            opacity = 1f;
            return false;
        }

        private static float ClampOpacity(float value)
        {
            const float min = 0f;
            const float max = 1f;
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
