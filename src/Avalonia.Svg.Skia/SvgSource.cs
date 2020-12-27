using System.ComponentModel;
using SkiaSharp;
using Svg.Skia;

namespace Avalonia.Svg.Skia
{
    /// <summary>
    /// Represents a <see cref="SKPicture"/> based image.
    /// </summary>
    [TypeConverter(typeof(SvgSourceTypeConverter))]
    public class SvgSource : SKSvg
    {
    }
}
