# Svg.Skia

[![Gitter](https://badges.gitter.im/wieslawsoltes/Svg.Skia.svg)](https://gitter.im/wieslawsoltes/Svg.Skia?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

[![Build status](https://dev.azure.com/wieslawsoltes/GitHub/_apis/build/status/Sources/Svg.Skia)](https://dev.azure.com/wieslawsoltes/GitHub/_build/latest?definitionId=-1)
![CI](https://github.com/wieslawsoltes/Svg.Skia/workflows/CI/badge.svg)

[![NuGet](https://img.shields.io/nuget/v/svg.skia.svg)](https://www.nuget.org/packages/svg.skia)
[![NuGet](https://img.shields.io/nuget/dt/svg.skia.svg)](https://www.nuget.org/packages/svg.skia)
[![MyGet](https://img.shields.io/myget/svgskia-nightly/vpre/svg.skia.svg?label=myget)](https://www.myget.org/gallery/svgskia-nightly)

[![GitHub release](https://img.shields.io/github/release/wieslawsoltes/svg.skia.svg)](https://github.com/wieslawsoltes/svg.skia)
[![Github All Releases](https://img.shields.io/github/downloads/wieslawsoltes/svg.skia/total.svg)](https://github.com/wieslawsoltes/svg.skia)
[![Github Releases](https://img.shields.io/github/downloads/wieslawsoltes/svg.skia/latest/total.svg)](https://github.com/wieslawsoltes/svg.skia)

*Svg.Skia* is an [SVG](https://en.wikipedia.org/wiki/Scalable_Vector_Graphics) rendering library.

## About

*Svg.Skia* can be used as a .NET library or as a CLI application
to render SVG files based on a [static](http://www.w3.org/TR/SVG11/feature#SVG-static)
[SVG Full 1.1](https://www.w3.org/TR/SVG/Overview.html) subset to raster images or
to a backend's canvas.

The `Svg.Skia` is using [SVG](https://github.com/vvvv/SVG) library to load `Svg` object model. 

The `Svg.Skia` library is implemented using `SkiaSharp` rendering backend that aims to be on par 
or more complete then original `System.Drawing` implementation and more performant and cross-platform.

The `Svg.Skia` can be used in same way as the [SkiaSharp.Extended.Svg](https://github.com/mono/SkiaSharp.Extended/tree/master/SkiaSharp.Extended.Svg) 
(load `svg` files as `SKPicture`). 

The `Svg` library has more complete implementation of `Svg` document model then [SkiaSharp.Extended.Svg](https://github.com/mono/SkiaSharp.Extended/tree/master/SkiaSharp.Extended.Svg)
and the `Svg.Skia` renderer will provide more complete rendering subsystem implementation.

## NuGet

Svg.Skia is delivered as a NuGet package.

You can find the packages here [NuGet](https://www.nuget.org/packages/Svg.Skia/) and install the package like this:

`Install-Package Svg.Skia`

or by using nightly build feed:
* Add `https://www.myget.org/F/svgskia-nightly/api/v2` to your package sources
* Alternative nightly build feed `https://pkgs.dev.azure.com/wieslawsoltes/GitHub/_packaging/CI/nuget/v3/index.json`
* Update your package using `Svg.Skia` feed

and install the package like this:

`Install-Package Svg.Skia -Pre`

## Usage

### Library

#### Install Package

```
dotnet add package Svg.Skia
```

```
Install-Package Svg.Skia
```

#### Draw on Canvas

```C#
using SkiaSharp;
using Svg.Skia;

var svg = new SKSvg();

svg.Load("image.svg");

SKCanvas canvas = ...
canvas.DrawPicture(svg.Picture);
```

#### Save as Png

```C#
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svg") != null)
    {
        svg.Save("image.png", SKEncodedImageFormat.Png, 100, 1f, 1f);
    }
}
```

```C#
using System.IO;
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svg") != null)
    {
        using (var stream = File.OpenWrite("image.png"))
        {
            svg.Picture.ToImage(stream, SKColors.Empty, SKEncodedImageFormat.Png, 100, 1f, 1f);
        }
    }
}
```

```C#
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svgz") != null)
    {
        svg.Save("image.png", SKEncodedImageFormat.Png, 100, 1f, 1f);
    }
}
```

```C#
using System.IO;
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svgz") != null)
    {
        using (var stream = File.OpenWrite("image.png"))
        {
            svg.Picture.ToImage(stream, SKColors.Empty, SKEncodedImageFormat.Png, 100, 1f, 1f);
        }
    }
}
```

#### Save as Pdf

```C#
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svg") != null)
    {
        svg.Picture.ToPdf("image.pdf", SKColors.Empty, 1f, 1f);
    }
}
```

#### Save as Xps

```C#
using SkiaSharp;
using Svg.Skia;

using (var svg = new SKSvg())
{
    if (svg.Load("image.svg") != null)
    {
        svg.Picture.ToXps("image.xps", SKColors.Empty, 1f, 1f);
    }
}
```

### Tool

```
dotnet tool install -g Svg.Skia.Converter
```

```
Svg.Skia.Converter:
  Converts a svg file to an encoded bitmap image.

Usage:
  Svg.Skia.Converter [options]

Options:
  -f, --inputFiles <inputfiles>                The relative or absolute path to the input files
  -d, --inputDirectories <inputdirectories>    The relative or absolute path to the input directories
  -o, --outputDirectory <outputdirectory>      The relative or absolute path to the output directory
  --outputFile <outputfile>                    The relative or absolute path to the output file
  -p, --pattern <pattern>                      The search string to match against the names of files in the input directory
  --format <format>                            The output image format
  -q, --quality <quality>                      The output image quality
  -b, --background <background>                The output image background
  -s, --scale <scale>                          The output image horizontal and vertical scaling factor
  --scaleX, -sx <scalex>                       The output image horizontal scaling factor
  --scaleY, -sy <scaley>                       The output image vertical scaling factor
  --quiet                                      Set verbosity level to quiet
  -c, --load-config <load-config>              The relative or absolute path to the config file
  --save-config <save-config>                  The relative or absolute path to the config file
  --version                                    Show version information
  -?, -h, --help                               Show help and usage information
```

Supported formats: png, jpg, jpeg, webp, pdf, xps

## Build

To build the projects you need to install [.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) version `SDK 3.0.100`.

```
git clone git@github.com:wieslawsoltes/Svg.Skia.git
cd Svg.Skia
git submodule update --init --recursive
dotnet build -c Release
```

## Externals

The `Svg.Skia` library is using code from the https://github.com/vvvv/SVG

## License

Parts of Svg.Skia source code are adapted from the https://github.com/vvvv/SVG

Svg.Skia is licensed under the [MIT license](LICENSE.TXT).
