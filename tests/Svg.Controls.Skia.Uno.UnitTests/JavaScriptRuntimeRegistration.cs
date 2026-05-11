using System.Runtime.CompilerServices;
using Svg.Skia;

namespace Uno.Svg.Skia.UnitTests;

internal static class JavaScriptRuntimeRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        SKSvgJavaScriptRuntime.Register();
    }
}
