# Svg.Skia

[![Gitter](https://badges.gitter.im/wieslawsoltes/Svg.Skia.svg)](https://gitter.im/wieslawsoltes/Svg.Skia?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

[![Build Status](https://dev.azure.com/wieslawsoltes/GitHub/_apis/build/status/wieslawsoltes.Svg.Skia?branchName=master)](https://dev.azure.com/wieslawsoltes/GitHub/_build/latest?definitionId=93&branchName=master)
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
[SVG Full 1.1](https://www.w3.org/TR/SVG11/) subset to raster images or
to a backend's canvas.

The `Svg.Skia` is using [SVG](https://github.com/vvvv/SVG) library to load `Svg` object model. 

The `Svg.Skia` library is implemented using the `SkiaSharp` rendering backend that aims to be on par 
or more complete than the original `System.Drawing` implementation and more performant and cross-platform.

The `Svg.Skia` can be used in same way as the [SkiaSharp.Extended.Svg](https://github.com/mono/SkiaSharp.Extended/tree/main/source/SkiaSharp.Extended.Svg) 
(load `svg` files as `SKPicture`). 

The `Svg` library has a more complete implementation of the `Svg` document model than [SkiaSharp.Extended.Svg](https://github.com/mono/SkiaSharp.Extended/tree/main/source/SkiaSharp.Extended.Svg)
and the `Svg.Skia` renderer will provide more complete rendering subsystem implementation.

## NuGet

Svg.Skia is delivered as a NuGet package.

You can find the packages here [NuGet](https://www.nuget.org/packages/Svg.Skia/) and install the package like this:

`Install-Package Svg.Skia`

or by using nightly build feed:
* Add `https://www.myget.org/F/svgskia-nightly/api/v2` to your package sources
* Alternative nightly build feed `https://pkgs.dev.azure.com/wieslawsoltes/GitHub/_packaging/Nightly/nuget/v3/index.json`
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
    if (svg.Load("image.svg") is { })
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
    if (svg.Load("image.svg") is { })
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
    if (svg.Load("image.svgz") is { })
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
    if (svg.Load("image.svgz") is { })
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
    if (svg.Load("image.svg") is { })
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
    if (svg.Load("image.svg") is { })
    {
        svg.Picture.ToXps("image.xps", SKColors.Empty, 1f, 1f);
    }
}
```

### Hit Testing

#### SKSvg

The `SKSvg` class provides helpers for retrieving elements or drawables at a
given point. The hit-testing methods expect coordinates in picture space:

```C#
using SkiaSharp;
using Svg.Skia;

var svg = new SKSvg();
if (svg.Load("image.svg") is { })
{
    var element = svg.HitTestElements(new SKPoint(10, 10)).FirstOrDefault();
    if (element is { })
    {
        Console.WriteLine(element.ID);
    }
}
```

When drawing on a transformed canvas you can convert canvas coordinates to
picture coordinates using `TryGetPicturePoint` and then use the hit-testing
methods.

#### Svg control

The `Svg` Avalonia control exposes a `HitTestElements` method that accepts
a point in control coordinates and returns the matching SVG elements:

```C#
var hits = svgControl.HitTestElements(new Point(x, y));
```


### Avalonia

#### Install Package

```
dotnet add package Svg.Controls.Skia.Avalonia
```

```
Install-Package Svg.Controls.Skia.Avalonia
```

### Svg control

```XAML
<Svg Path="/Assets/__AJ_Digital_Camera.svg"/>
```

#### Image control

```XAML
<Image Source="{SvgImage /Assets/__AJ_Digital_Camera.svg}"/>
```

#### Background

```XAML
<Border Width="100"
        Height="100"
        Background="{SvgImage /Assets/__AJ_Digital_Camera.svg}" />
```

### CSS styling

> [!WARNING]  
> For the SVG foreground color to work correctly, the SVG file must not contain any predefined colors, such as a fill attribute.
Additionally, the SVG must use `<path />` elements.
If either of these conditions is not met, the CSS foreground color will not be applied.

```XAML
<Svg Path="/Assets/__tiger.svg" 
     Css="path { fill: #FF0000; }"  />
```

```XAML
<Style Selector="Svg">
  <Setter Property="(Svg.Css)" Value="path { fill: #FF0000; }" />
</Style>
```

```XAML
<SvgSource x:Key="TigerIcon"
           Path="/Assets/__tiger.svg"
           Css="path { fill: #FF0000; }" />
```

```XAML
<Image>
  <Image.Source>
    <SvgImage Source="{DynamicResource TigerIcon}" />
  </Image.Source>
</Image>
```

```XAML
<Image>
  <Image.Source>
    <SvgImage Source="/Assets/__tiger.svg" Css="path { fill: #FF0000; }" />
  </Image.Source>
</Image>
```

#### SvgResourceExtension Markup Extension

The former `SvgBrush` markup extension has been renamed to `SvgResourceExtension`. In XAML you can use the short `{SvgResource ...}` syntax to paint any brush property directly:

```XAML
<Border CornerRadius="12"
        Background="{SvgResource /Assets/__tiger.svg}" />
```

To reuse the brush across your view, declare it in resources (the markup extension type named `SvgResourceExtension` still trims to `SvgResource` when used in XAML):

```XAML
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:svg="clr-namespace:Avalonia.Svg.Skia;assembly=Svg.Controls.Skia.Avalonia">
    <UserControl.Resources>
        <svg:SvgResource x:Key="TigerBrush"
                         Stretch="UniformToFill"
                         AlignmentX="Center"
                         AlignmentY="Center"
                         TileMode="Tile"
                         DestinationRect="0,0,1,1"
                         Opacity="0.85">/Assets/__tiger.svg</svg:SvgResource>
    </UserControl.Resources>

    <Border Background="{DynamicResource TigerBrush}" />
</UserControl>
```

The optional properties mirror those on `VisualBrush`, so you can tweak layout, tiling, opacity, and transforms directly in XAML while the control takes care of loading and rendering the SVG content.

When you need the brush from code-behind, the extension now exposes a `ToBrush(IServiceProvider? serviceProvider = null)` helper and supports implicit conversion to `Brush`, which can be assigned to any `IBrush` property:

```csharp
// Resolve relative paths by providing BaseUri when running outside of XAML.
var brush = new SvgResourceExtension("avares://MyAssembly/Assets/Icon.svg")
{
    BaseUri = new Uri("avares://MyAssembly/")
}.ToBrush();

// Or rely on the implicit conversion to Brush/IBrush.
IBrush background = new SvgResourceExtension("avares://MyAssembly/Assets/Icon.svg");

// When you already have an SvgImage instance, create a brush directly.
var svgImage = new SvgImage
{
    Source = SvgSource.Load("avares://MyAssembly/Assets/Icon.svg", baseUri: null)
};
var fromImage = SvgResourceExtension.CreateBrush(
    svgImage,
    stretch: Stretch.Uniform,
    alignmentX: AlignmentX.Center,
    alignmentY: AlignmentY.Center);

// Or skip creating the markup extension entirely and build a brush from the SVG path in one call.
var fromPath = SvgResourceExtension.CreateBrush(
    "avares://MyAssembly/Assets/Icon.svg",
    stretch: Stretch.Fill,
    alignmentX: AlignmentX.Right,
    alignmentY: AlignmentY.Bottom);
```

The Skia-backed controls also accept optional `css` and `currentCss` arguments on the static helper so you can apply styles while creating the brush from code.

#### Avalonia Previewer

To make controls work with `Avalonia Previewer` please add the following lines to `BuildAvaloniaApp()` method:
```C#
GC.KeepAlive(typeof(SvgImageExtension).Assembly);
GC.KeepAlive(typeof(Svg.Controls.Skia.Avalonia.Svg).Assembly);
```

The `BuildAvaloniaApp()` should look similar to this:
```C#
public static AppBuilder BuildAvaloniaApp()
{
    GC.KeepAlive(typeof(SvgImageExtension).Assembly);
    GC.KeepAlive(typeof(Svg.Controls.Skia.Avalonia.Svg).Assembly);
    return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
```

This is known issue as previewer not always loads all dependencies, especially custom controls in Avalonia xmlns, other solution would be to add xmlns prefix to control with provided assembly path.

### Avalonia SkiaSharp Controls

#### Install Package

```
dotnet add package Skia.Controls.Avalonia
```

```
Install-Package Skia.Controls.Avalonia
```

#### Canvas

Usage:
```xaml
<SKCanvasControl Name="CanvasControl" />
```

```C#
CanvasControl.Draw += (_, e) =>
{
    e.Canvas.DrawRect(SKRect.Create(0f, 0f, 100f, 100f), new SKPaint { Color = SKColors.Aqua });
};
```

### Command-line tool

```
dotnet tool install -g Svg.Skia.Converter
```

```
Svg.Skia.Converter:
  Converts a svg file to an encoded bitmap image.

Usage:
  Svg.Skia.Converter [options]

Options:
  -f, --inputFiles <inputfiles>              The relative or absolute path to the input files
  -d, --inputDirectory <inputdirectory>      The relative or absolute path to the input directory
  -o, --outputDirectory <outputdirectory>    The relative or absolute path to the output directory
  --outputFiles <outputfiles>                The relative or absolute path to the output files
  -p, --pattern <pattern>                    The search string to match against the names of files in the input directory
  --format <format>                          The output image format
  -q, --quality <quality>                    The output image quality
  -b, --background <background>              The output image background
  -s, --scale <scale>                        The output image horizontal and vertical scaling factor
  --scaleX, -sx <scalex>                     The output image horizontal scaling factor
  --scaleY, -sy <scaley>                     The output image vertical scaling factor
  --systemLanguage <systemlanguage>          The system language name as defined in BCP 47
  --quiet                                    Set verbosity level to quiet
  -c, --load-config <load-config>            The relative or absolute path to the config file
  --save-config <save-config>                The relative or absolute path to the config file
  --version                                  Show version information
  -?, -h, --help                             Show help and usage information
```

Supported formats: png, jpg, jpeg, webp, pdf, xps

## SVG to C# Compiler

### About

SVGC compiles SVG drawing markup to C# using SkiaSharp as rendering engine. SVGC can be also used as codegen for upcoming C# 9 Source Generator feature.

[![Demo](images/Demo.png)](images/Demo.png)

### Source Generator Usage

Add NuGet package reference to your `csproj`.

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net6.0</TargetFramework>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

```xml
<ItemGroup>
  <PackageReference Include="Svg.SourceGenerator.Skia" Version="0.5.0" />
</ItemGroup>
```

Include `svg` assets file in your `csproj`.

```xml
<ItemGroup>
  <AdditionalFiles Include="Assets/Sample.svg" NamespaceName="Assets" ClassName="Sample" />
</ItemGroup>
```

Use generated `SKPicture` using static `Picture` property from `Sample` class.

```C#
using SkiaSharp;
using Assets;

public void Draw(SKCanvas canvas)
{
    canvas.DrawPicture(Sample.Picture);
}
```

### Avalonia Usage

`csproj`
```xml
<ItemGroup>
  <AdditionalFiles Include="Assets/__tiger.svg" NamespaceName="AvaloniaSample" ClassName="Tiger" />
</ItemGroup>
```
```xml
<ItemGroup>
  <PackageReference Include="Svg.SourceGenerator.Skia" Version="0.5.0" />
  <PackageReference Include="Skia.Controls.Avalonia" Version="0.5.0" />
</ItemGroup>
```

`xaml`
```xaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:AvaloniaSample;assembly=AvaloniaSample"
        xmlns:skp="clr-namespace:Skia.Controls.Avalonia;assembly=Skia.Controls.Avalonia"
        mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
        Width="900" Height="650" WindowStartupLocation="CenterScreen"
        x:Class="AvaloniaSample.MainWindow"
        Title="AvaloniaSample">
    <Window.Resources>
        <skp:SKPictureImage x:Key="TigeImage" Source="{x:Static local:Tiger.Picture}" />
    </Window.Resources>
    <Grid>
        <Image Source="{StaticResource TigeImage}" />
    </Grid>
</Window>
```

### svgc Usage

```
svgc:
  Converts a svg file to a C# code.

Usage:
  svgc [options]

Options:
  -i, --inputFile <inputfile>      The relative or absolute path to the input file [default: ]
  -o, --outputFile <outputfile>    The relative or absolute path to the output file [default: ]
  -j, --jsonFile <jsonfile>        The relative or absolute path to the json file [default: ]
  -n, --namespace <namespace>      The generated C# namespace name [default: Svg]
  -c, --class <class>              The generated C# class name [default: Generated]
  --version                        Show version information
  -?, -h, --help                   Show help and usage information
```

Json File Format
```json
[
    { "InputFile":"file1.svg", "OutputFile":"file1.svg.cs", "Class":"ClassName1", "Namespace":"NamespaceName" },
    { "InputFile":"file2.svg", "OutputFile":"file2.svg.cs", "Class":"ClassName2", "Namespace":"NamespaceName" }
]
```

### Links

* [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.cookbook.md)
* [Source Generators](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.md)
* [Source Generators Samples](https://github.com/dotnet/roslyn-sdk/tree/master/samples/CSharp/SourceGenerators)
* [Introducing C# Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/)

## Build

To build the projects you need to install [.NET 5.0](https://dotnet.microsoft.com/download/dotnet/5.0) version `SDK 5.0.100`.

```
git clone git@github.com:wieslawsoltes/Svg.Skia.git
cd Svg.Skia
git submodule update --init --recursive
dotnet build -c Release
```

### Publish Managed

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r win7-x64 /p:PublishTrimmed=True /p:PublishReadyToRun=True -o Svg.Skia.Converter_net6.0_win7-x64
```

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r ubuntu.14.04-x64 /p:PublishTrimmed=True /p:PublishReadyToRun=True -o Svg.Skia.Converter_net6.0_ubuntu.14.04-x64
```

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r osx.10.12-x64 /p:PublishTrimmed=True /p:PublishReadyToRun=True -o Svg.Skia.Converter_net6.0_osx.10.12-x64
```

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r debian.8-x64 /p:PublishTrimmed=True /p:PublishReadyToRun=True -o Svg.Skia.Converter_net6.0_debian.8-x64
```

```
cd ./src/SvgToPng
dotnet publish -c Release -f net6.0 -r win7-x64 -o SvgToPng_net6.0_win7-x64
```

```
cd ./src/SvgXml.Diagnostics
dotnet publish -c Release -f net6.0 -r win7-x64 -o SvgXml.Diagnostics_net6.0_win7-x64
```

### Publish Native

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r win-x64 -o Svg.Skia.Converter_net6.0_win-x64
```

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r linux-x64 -o Svg.Skia.Converter_net6.0_linux-x64
```

```
cd ./src/Svg.Skia.Converter
dotnet publish -c Release -f net6.0 -r osx-x64 -o Svg.Skia.Converter_net6.0_osx-x64
```

## Externals

The `Svg.Skia` library is using code from the https://github.com/vvvv/SVG

## License

Parts of Svg.Skia source code are adapted from the https://github.com/vvvv/SVG

Svg.Skia is licensed under the [MIT license](LICENSE.TXT).
