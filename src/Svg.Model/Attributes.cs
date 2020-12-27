using System;

namespace Svg.Model
{
    [Flags]
    public enum Attributes
    {
        None = 0,
        Display = 1,
        Visibility = 2,
        Opacity = 4,
        Filter = 8,
        ClipPath = 16,
        Mask = 32,
        RequiredFeatures = 64,
        RequiredExtensions = 128,
        SystemLanguage = 256
    }
}
