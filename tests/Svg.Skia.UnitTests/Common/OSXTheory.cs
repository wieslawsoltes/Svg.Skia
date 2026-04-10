using System.Runtime.InteropServices;
using Xunit;

namespace Svg.Skia.UnitTests.Common;

public sealed class OSXTheory : TheoryAttribute
{
    public OSXTheory(string message = "macOS only theory")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        Skip = message;
    }
}
