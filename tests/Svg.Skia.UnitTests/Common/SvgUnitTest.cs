using System.IO;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia.UnitTests.Common;

public abstract class SvgUnitTest
{
    private static string GetFontsPath(string name) 
        => Path.Combine("..", "..", "..", "..", "..", "externals", "resvg", "tests", "fonts", name);

    static SvgUnitTest()
    {
        if (SKSvgSettings.s_typefaceProviders is { })
        {
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("Amiri-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("MPLUS1p-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoEmoji-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoMono-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Black.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Bold.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Italic.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Light.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Thin.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSerif-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("SedgwickAveDisplay-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf")));
            SKSvgSettings.s_typefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("Yellowtail-Regular.ttf")));
        }
    }
}
