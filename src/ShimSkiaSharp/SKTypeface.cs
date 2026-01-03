// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public class SKTypeface : ICloneable, IDeepCloneable<SKTypeface>
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

    public SKTypeface Clone() => DeepClone(new CloneContext());

    public SKTypeface DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKTypeface DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKTypeface existing))
        {
            return existing;
        }

        var clone = new SKTypeface();
        context.Add(this, clone);

        clone.FamilyName = FamilyName;
        clone.FontWeight = FontWeight;
        clone.FontWidth = FontWidth;
        clone.FontSlant = FontSlant;

        return clone;
    }
}
