#pragma warning disable CS0618 // FakeBoldText is deprecated on SKPaint; shim keeps the legacy surface for compatibility

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;
using NativeTypeface = SkiaSharp.SKTypeface;

namespace Svg.Skia.UnitTests;

public class Issue462Tests : SvgUnitTest
{
    [Fact]
    public void FindTypefaces_PreservesRequestedBoldForRegularOnlyResolvedFace()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { provider }
        };
        var model = new SkiaModel(settings);
        var assetLoader = new SkiaSvgAssetLoader(model);
        var paint = CreateRequestedBoldPaint(provider.FamilyName);

        var span = Assert.Single(assetLoader.FindTypefaces("Bold Text", paint));

        Assert.NotNull(span.Typeface);
        Assert.Equal(SKFontStyleWeight.Bold, span.Typeface!.FontWeight);
        AssertUsesSyntheticBold(model, span.Typeface);
    }

    [Fact]
    public void FindRunTypeface_PreservesRequestedBoldForRegularOnlyResolvedFace()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { provider }
        };
        var model = new SkiaModel(settings);
        var assetLoader = new SkiaSvgAssetLoader(model);
        var paint = CreateRequestedBoldPaint(provider.FamilyName);

        var typeface = assetLoader.FindRunTypeface("Bold Text", paint);

        Assert.NotNull(typeface);
        Assert.Equal(SKFontStyleWeight.Bold, typeface!.FontWeight);
        AssertUsesSyntheticBold(model, typeface);
    }

    private static SKPaint CreateRequestedBoldPaint(string familyName)
    {
        return new SKPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                familyName,
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };
    }

    private static void AssertUsesSyntheticBold(SkiaModel model, SKTypeface typeface)
    {
        using var localPaint = model.ToSKPaint(new SKPaint
        {
            TextSize = 48f,
            Typeface = typeface
        });

        Assert.NotNull(localPaint);
        Assert.True(localPaint!.FakeBoldText);
    }

    private sealed class RegularOnlyTypefaceProvider : ITypefaceProvider, IDisposable
    {
        private readonly NativeTypeface _typeface;

        public RegularOnlyTypefaceProvider(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Test font was not found.", path);
            }

            _typeface = NativeTypeface.FromFile(path);
            FamilyName = _typeface.FamilyName;
        }

        public string FamilyName { get; }

        public NativeTypeface? FromFamilyName(
            string fontFamily,
            SkiaSharp.SKFontStyleWeight fontWeight,
            SkiaSharp.SKFontStyleWidth fontWidth,
            SkiaSharp.SKFontStyleSlant fontStyle)
        {
            var requestedFamilies = fontFamily
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static family => family.Trim().Trim('"', '\''));

            return requestedFamilies.Contains(FamilyName, StringComparer.OrdinalIgnoreCase)
                ? _typeface
                : null;
        }

        public void Dispose()
        {
            _typeface.Dispose();
        }
    }
}

#pragma warning restore CS0618
