using System.Runtime.InteropServices;
using Xunit;

namespace Svg.Skia.UnitTests.Common;

public sealed class WindowsAndOSXTheory : TheoryAttribute
{
    public WindowsAndOSXTheory(string message = "Windows and OSX only theory")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip = message;
        }
    }
}
