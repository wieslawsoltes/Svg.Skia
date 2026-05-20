using System;

namespace Svg
{
    /// <summary>
    /// Helpers for resolving the SVG 2 effective href value from href-bearing elements.
    /// </summary>
    public static class SvgElementHrefExtensions
    {
        /// <summary>
        /// Attempts to get the effective href as a URI.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <param name="href">The effective href URI when present.</param>
        /// <returns><c>true</c> when an effective href value was found and parsed.</returns>
        public static bool TryGetEffectiveHref(this SvgElement element, out Uri href)
        {
            string hrefText;
            if (TryGetEffectiveHrefString(element, out hrefText))
            {
                if (string.IsNullOrWhiteSpace(hrefText))
                {
                    href = null;
                    return false;
                }

                return Uri.TryCreate(hrefText.Trim(), UriKind.RelativeOrAbsolute, out href);
            }

            href = null;
            return false;
        }

        /// <summary>
        /// Attempts to get the effective href as authored text.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <param name="href">The effective href text when present.</param>
        /// <returns><c>true</c> when an effective href value was found.</returns>
        public static bool TryGetEffectiveHrefString(this SvgElement element, out string href)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            if (TryGetChangedCurrentHref(element, out href))
            {
                return true;
            }

            if (IsSvg2HrefPreferred(element))
            {
                if (TryGetRawHref(element, string.Empty, out href))
                {
                    return true;
                }

                if (TryGetCustomHref(element, "href", out href))
                {
                    return true;
                }

                if (TryGetRawHref(element, SvgNamespaces.XLinkNamespace, out href))
                {
                    return true;
                }

                if (TryGetCustomHref(element, "xlink:href", out href) ||
                    TryGetCustomHref(element, SvgNamespaces.XLinkNamespace + ":href", out href))
                {
                    return true;
                }
            }
            else
            {
                if (TryGetRawHref(element, SvgNamespaces.XLinkNamespace, out href))
                {
                    return true;
                }

                if (TryGetCustomHref(element, "xlink:href", out href) ||
                    TryGetCustomHref(element, SvgNamespaces.XLinkNamespace + ":href", out href))
                {
                    return true;
                }

                if (TryGetRawHref(element, string.Empty, out href))
                {
                    return true;
                }

                if (TryGetCustomHref(element, "href", out href))
                {
                    return true;
                }
            }

            if (element.TryGetAttribute("href", out href) && !string.IsNullOrWhiteSpace(href))
            {
                return true;
            }

            href = null;
            return false;
        }

        internal static SvgDeferredPaintServer GetEffectiveDeferredPaintServer(
            this SvgElement element,
            SvgDeferredPaintServer fallback)
        {
            string hrefText;
            if (TryGetEffectiveHrefString(element, out hrefText))
            {
                return string.IsNullOrWhiteSpace(hrefText)
                    ? null
                    : new SvgDeferredPaintServer(hrefText.Trim());
            }

            return fallback;
        }

        private static bool IsSvg2HrefPreferred(SvgElement element)
        {
            return (element as SvgDocument ?? element.OwnerDocument)?.LoadOptions.PreferSvg2Href != false;
        }

        private static bool TryGetChangedCurrentHref(SvgElement element, out string href)
        {
            if (!TryGetCurrentHrefValue(element, out var currentValue))
            {
                href = null;
                return false;
            }

            if (!element.TryGetCompatibilityHrefAttributeValueAfterParse(out var parsedValue))
            {
                return TryConvertHrefValue(currentValue, out href);
            }

            if (!HrefValuesEqual(currentValue, parsedValue))
            {
                return TryConvertHrefValue(currentValue, out href);
            }

            href = null;
            return false;
        }

        private static bool TryGetCurrentHrefValue(SvgElement element, out object value)
        {
            if (element.Attributes.ContainsKey("href"))
            {
                value = element.Attributes.GetAttribute<object>("href");
                return true;
            }

            value = null;
            return false;
        }

        private static bool HrefValuesEqual(object currentValue, object parsedValue)
        {
            if (ReferenceEquals(currentValue, parsedValue))
            {
                return true;
            }

            string currentText;
            string parsedText;
            if (!TryConvertHrefValue(currentValue, out currentText) ||
                !TryConvertHrefValue(parsedValue, out parsedText))
            {
                return Equals(currentValue, parsedValue);
            }

            return string.Equals(currentText, parsedText, StringComparison.Ordinal);
        }

        private static bool TryConvertHrefValue(object value, out string href)
        {
            switch (value)
            {
                case null:
                    href = string.Empty;
                    return true;
                case string text:
                    href = text;
                    return true;
                case Uri uri:
                    href = uri.OriginalString;
                    return true;
                case SvgDeferredPaintServer deferredPaintServer:
                    href = deferredPaintServer.DeferredId ?? string.Empty;
                    return true;
                default:
                    href = value.ToString();
                    return href != null;
            }
        }

        private static bool TryGetRawHref(SvgElement element, string namespaceName, out string href)
        {
            return element.TryGetCompatibilityHrefAttributeValue(namespaceName, out href);
        }

        private static bool TryGetCustomHref(SvgElement element, string key, out string href)
        {
            if (element.CustomAttributes.TryGetValue(key, out href))
            {
                return true;
            }

            href = null;
            return false;
        }
    }
}
