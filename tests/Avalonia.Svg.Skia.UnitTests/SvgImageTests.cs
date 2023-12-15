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
using System;
using Avalonia.Platform;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgImageTests
{
    [Fact]
    public void SvgImage_Load()
    {
        var uri = new Uri($"avares://Avalonia.Svg.Skia.UnitTests/Assets/Icon.svg");
        var assetLoader = new StandardAssetLoader(); // AvaloniaLocator.Current.GetService<IAssetLoader>()

        var svgFile = assetLoader.Open(uri);
        Assert.NotNull(svgFile);

        var svgSource = new SvgSource();
        var picture = svgSource.Load(svgFile);
        Assert.NotNull(picture);

        var svgImage = new SvgImage() { Source = svgSource };
        Assert.NotNull(svgImage);
    }
}
