namespace Svg
{
    /// <summary>
    /// Describes which external resources may be resolved while loading or rendering an SVG document.
    /// </summary>
    public enum SvgExternalResourcePolicy
    {
        /// <summary>
        /// External resources are allowed by the caller's resource loader.
        /// </summary>
        Enabled,

        /// <summary>
        /// External resources are limited to the same origin as the owner document.
        /// </summary>
        SameOrigin,

        /// <summary>
        /// Only same-document references and data URLs are allowed.
        /// </summary>
        SameDocumentAndDataOnly,

        /// <summary>
        /// External resources are disabled.
        /// </summary>
        Disabled
    }
}
