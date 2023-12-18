using System.IO;
using Svg.Skia.TypefaceProviders;

namespace Svg.Skia.UnitTests.Common;

public abstract class SvgUnitTest
{
    protected static string GetFontsPath(string name) 
        => Path.Combine("..", "..", "..", "..", "..", "externals", "resvg", "tests", "fonts", name);

    protected void SetTypefaceProviders(SKSvgSettings settings)
    {
        if (settings.TypefaceProviders is { })
        {
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("Amiri-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("MPLUS1p-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoEmoji-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoMono-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Black.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Bold.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Italic.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Light.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSans-Thin.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("NotoSerif-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("SedgwickAveDisplay-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf")));
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetFontsPath("Yellowtail-Regular.ttf")));
        }
    }
}
