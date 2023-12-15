/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
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
