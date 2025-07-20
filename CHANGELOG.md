# Svg.Skia Changelog

## 3.0.5

* Updated Avalonia packages to 11.3.0.2.

## 0.3.0

* Updated NuGet packages.
* Update SVG sources.

## 0.2.0

* Updated NuGet packages.

## 0.1.9

* Updated NuGet packages.

## 0.1.8

* Added fixes for Xamarin.Forms Android/iOS.

## 0.1.7

* Strong name signed assemblies.

## 0.1.6

* Fixed `marker` exception.
* Fixed `use` to accept `svg` element.
* Added native build support using CoreRT.
* Added referenced properties support for `filter` element.
* Added `feImage` referenced image `preserveAspectRatio` support.
* Improved `Filter Effects` validation.
* Fixed `fill` and `stroke` validation.
* Added `SKFontManager` typeface provider.
* Added custom font loader helper class `CustomTypefaceProvider`.

## 0.1.5

* Fixed `systemLanguage` validation.
* Removed debug code.

## 0.1.4

* Added `switch` element support.
* Added `systemLanguage` attribute support.

## 0.1.3

* Updated `Svg.Skia.Converter` tool.
* Use `Svg.Custom` build of the `Svg` library.
* Initial support for new `Filter Effects`.

## 0.1.2

* Added referenced properties support for `linearGradient` element.
* Added referenced properties support for `radialGradient` element.
* Changed bitmap creation to use `SKImageInfo`.

## 0.1.1

* Added `Overflow` property to `Drawable`.
* Added `FilterQuality=SKFilterQuality.High` for `ImageDrawable`.
* Added transform support for `image` `svg` fragment.
* Added support for embeded `svgz` images.

## 0.1.0

* Added `Svg.Custom` project for `Svg` library.
* Refactored utility classes.
* Added custom font support via `ITypefaceProvider`.

## 0.0.12

* Fixed deffered `stop` color paint server.
* Fixed invalid `SvgUnit` default value handling.
* Added `Filer Effects` utility class.

## 0.0.11

* Fixed `mask` processing.
* Updaed `feColorMatrix` filter processing.

## 0.0.10

* Added new `Filter Effects` support.
* Added `mask` element support.
* Fixed `clipPath` element processing.

## 0.0.9

* Added `Filer Effects` prcessing.

## 0.0.8

* Fixed `stoke` and `file` validation.
* Refactored utility classes.
* Added generic referenced element support.

## 0.0.7

* Added `Xamarin.Forms` sample application.
* Initial `IImage` implemetation for `Avalonia`.
* Fixed `rect` attributes validation.

## 0.0.6

* Made `Drawable` classes public.
* Added initial `HitTest` implemetation for `Drawable`.

## 0.0.5

* Removed `SKSvgRenderer` implemetation.
* Added `Drawable` object model.

## 0.0.4

* Refactored `SKSvgRenderer` class.

## 0.0.3

* Added `marker` element support.

## 0.0.2

* Added `pattern` element support.
* Added `image` element support.

## 0.0.1

* Initial release.
