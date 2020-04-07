using System;

namespace SvgValidated
{
    [Flags]
    public enum SvgFontStyle
    {
        Inherit,
        Normal = 1,
        Oblique = 2,
        Italic = 4,
        All = Normal | Oblique | Italic
    }
}
