using System;

namespace SvgValidated
{
    [Flags]
    public enum SvgFontWeight
    {
        Inherit,
        Normal = 1,
        Bold = 2,
        Bolder = 4,
        Lighter = 8,
        W100 = 1 << 8,
        W200 = 2 << 8,
        W300 = 4 << 8,
        W400 = 8 << 8,
        W500 = 16 << 8,
        W600 = 32 << 8,
        W700 = 64 << 8,
        W800 = 128 << 8,
        W900 = 256 << 8,
        All = Normal | Bold | W100 | W200 | W300 | W400 | W500 | W600 | W700 | W800 | W900
    }
}
