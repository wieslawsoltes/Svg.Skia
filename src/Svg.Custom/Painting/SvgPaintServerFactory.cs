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

            // Otherwise try and parse as colour
            return new SvgColourServer((Color)_colourConverter.ConvertFrom(colorValue));
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
    }
}
