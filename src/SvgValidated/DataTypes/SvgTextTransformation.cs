using System;

namespace SvgValidated
{
    [Flags]
    public enum SvgTextTransformation
    {
        Inherit = 0,
        None = 1,
        Capitalize = 2,
        Uppercase = 4,
        Lowercase = 8
    }
}
