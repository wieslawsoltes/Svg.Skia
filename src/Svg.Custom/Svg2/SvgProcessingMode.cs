namespace Svg
{
    /// <summary>
    /// Describes the SVG processing mode requested for document parsing and static rendering.
    /// </summary>
    public enum SvgProcessingMode
    {
        /// <summary>
        /// Static rendering without animation timelines or interactive DOM behavior.
        /// </summary>
        Static,

        /// <summary>
        /// Static rendering with secure-mode resource restrictions.
        /// </summary>
        SecureStatic,

        /// <summary>
        /// Animated rendering without interactive DOM behavior.
        /// </summary>
        Animated,

        /// <summary>
        /// Animated rendering with secure-mode resource restrictions.
        /// </summary>
        SecureAnimated,

        /// <summary>
        /// Dynamic, interactive processing.
        /// </summary>
        DynamicInteractive
    }
}
