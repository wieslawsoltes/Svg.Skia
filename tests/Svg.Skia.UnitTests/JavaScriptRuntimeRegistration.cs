using System.Runtime.CompilerServices;

namespace Svg.Skia.UnitTests;

internal static class JavaScriptRuntimeRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        SKSvgJavaScriptRuntime.Register();
    }
}
