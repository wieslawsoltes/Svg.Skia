namespace Svg.Model;

/// <summary>
///     Specifies how the pixel content of a mask element is used when applied.
///     Corresponds to the CSS <c>mask-type</c> property on the mask element.
/// </summary>
/// <remarks>
///     SVG spec: https://www.w3.org/TR/css-masking-1/#the-mask-type
/// </remarks>
public enum MaskType
{
    /// <summary>
    ///     Default. The luminance values of the mask content determine the mask.
    ///     White (high luminance) = fully visible, Black (zero luminance) = fully masked.
    /// </summary>
    Luminance,
    
    /// <summary>
    ///     The alpha channel of the mask content determines the mask.
    ///     Opaque pixels = fully visible, transparent pixels = fully masked.
    ///     This is what <c>style="mask-type:alpha"</c> sets.
    /// </summary>
    Alpha
}
