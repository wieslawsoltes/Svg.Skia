// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public class SKTypeface
{
    public string? FamilyName { get; private set; }
    public SKFontStyleWeight FontWeight { get; private set; }
    public SKFontStyleWidth FontWidth { get; private set; }
    public SKFontStyleSlant FontSlant { get; private set; }

    private SKTypeface()
    {
    }

    public static SKTypeface FromFamilyName(
        string familyName,
        SKFontStyleWeight weight,
        SKFontStyleWidth width,
        SKFontStyleSlant slant)
    {
        return new()
        {
            FamilyName = familyName,
            FontWeight = weight,
            FontWidth = width,
            FontSlant = slant
        };
    }
}
