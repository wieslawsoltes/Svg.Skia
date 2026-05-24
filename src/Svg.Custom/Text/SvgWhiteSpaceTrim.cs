#nullable enable

using System;

namespace Svg;

[Flags]
internal enum SvgWhiteSpaceTrim
{
    None = 0,
    DiscardBefore = 1,
    DiscardAfter = 2,
    DiscardInner = 4
}
