using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using SkiaSharp;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class AvaloniaTypefaceProviderTests
{
    [AvaloniaTheory]
    [InlineData("Inter")]
    [InlineData("'Inter'")]
    [InlineData("\"Inter\"")]
    [InlineData("fonts:Inter#Inter")]
    [InlineData("avares://Avalonia.Fonts.Inter/Assets#Inter")]
    [InlineData("'Missing SVG family 565', 'Inter', sans-serif")]
    public void ResolvesEmbeddedInterAndPreservesGlyphCoverage(string family)
    {
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, family);

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
        using var font = new SKFont(typeface);
        foreach (var codepoint in "AV Łódź Żółć".EnumerateRunes())
        {
            Assert.True(font.ContainsGlyph(codepoint.Value));
        }
    }

    [AvaloniaTheory]
    [InlineData(SKFontStyleWeight.Thin)]
    [InlineData(SKFontStyleWeight.Light)]
    [InlineData(SKFontStyleWeight.Normal)]
    [InlineData(SKFontStyleWeight.Medium)]
    [InlineData(SKFontStyleWeight.SemiBold)]
    [InlineData(SKFontStyleWeight.Bold)]
    public void ResolvesRequestedEmbeddedWeight(SKFontStyleWeight weight)
    {
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, "fonts:Inter#Inter", weight);

        Assert.NotNull(typeface);
        // Inter's bundled Thin file reports a native weight of 250. Preserve the face Avalonia
        // selected rather than assuming every font's OS/2 metadata equals the request enum.
        Assert.Equal((int)Inter((FontWeight)(int)weight).PlatformTypeface.Weight, typeface.FontWeight);
    }

    [AvaloniaTheory]
    [InlineData("sans-serif")]
    [InlineData("SERIF")]
    [InlineData("monospace")]
    [InlineData("system-ui")]
    [InlineData("ui-sans-serif")]
    [InlineData("Default")]
    [InlineData("")]
    [InlineData(null)]
    public void GenericFallbackIsExplicitlyControllable(string? family)
    {
        Assert.Null(Request(new AvaloniaTypefaceProvider(false), family!));

        using var typeface = Request(new AvaloniaTypefaceProvider(true), family!);

        Assert.NotNull(typeface);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(Typeface.Default, out var expected));
        Assert.Equal(expected.FamilyName, typeface.FamilyName);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownConcreteFamilyDoesNotConsumeFallbackCandidates(bool fallback)
    {
        var provider = new AvaloniaTypefaceProvider(fallback);

        Assert.Null(Request(provider, "Unknown SVG font " + Guid.NewGuid()));
        using var typeface = Request(provider, "Unknown SVG font 565, Inter");

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaFact]
    public void DefaultPolicyOnlyEnablesGenericFallbackInBrowser()
    {
        Assert.Equal(OperatingSystem.IsBrowser(), new AvaloniaTypefaceProvider().UseDefaultFontFallback);
    }

    [AvaloniaFact]
    public void ExactNativeDefaultFamilyRemainsAvailableWithoutCollection()
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(Typeface.Default, out var expected));
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, expected.FamilyName);

        Assert.NotNull(typeface);
        Assert.Equal(expected.FamilyName, typeface.FamilyName);
        using var font = new SKFont(typeface);
        Assert.True(font.ContainsGlyph('A'));
    }

    [AvaloniaFact]
    public void BrokenOptionalCollectionDoesNotHideExactNativeFamily()
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(Typeface.Default, out var expected));
        var platform = StreamTypeface.Wrap(expected.PlatformTypeface,
            () => throw new IOException("Optional collection stream unavailable."));
        var key = new Uri("fonts:" + Uri.EscapeDataString(expected.FamilyName));
        using var fonts = new FontCollectionScope(new GlyphTypeface(platform), key);
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, expected.FamilyName);

        Assert.NotNull(typeface);
        Assert.Equal(expected.FamilyName, typeface.FamilyName);
    }

    [AvaloniaTheory]
    [InlineData("fonts:Inter#MissingSVGFont565")]
    [InlineData("avares://Avalonia.Fonts.Inter/Assets#MissingSVGFont565")]
    public void MissingFamilyInExistingCollectionDoesNotUseItsFirstFont(string family)
    {
        var provider = new AvaloniaTypefaceProvider(true);

        Assert.Null(Request(provider, family));
        using var typeface = Request(provider, family + ", Inter");

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaFact]
    public void DisposingReturnedTypefaceDoesNotDisposeAvaloniaFontOrNextResult()
    {
        var provider = new AvaloniaTypefaceProvider(false);
        var glyphTypeface = Inter();

        var first = Request(provider, "Inter");
        Assert.NotNull(first);
        first.Dispose();

        using var second = Request(provider, "Inter");
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotEqual(IntPtr.Zero, second.Handle);
        Assert.True(glyphTypeface.PlatformTypeface.TryGetStream(out var stream));
        using (stream)
        {
            Assert.True(stream.Length > 0);
        }
    }

    [AvaloniaTheory]
    [InlineData(SKFontStyleSlant.Upright, FontStyle.Normal)]
    [InlineData(SKFontStyleSlant.Italic, FontStyle.Italic)]
    [InlineData(SKFontStyleSlant.Oblique, FontStyle.Oblique)]
    public void ForwardsWeightStretchAndSlantToAvalonia(SKFontStyleSlant slant, FontStyle expectedStyle)
    {
        using var fonts = new FontCollectionScope(Inter());
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, fonts.Family, SKFontStyleWeight.SemiBold, SKFontStyleWidth.Condensed, slant);

        Assert.NotNull(typeface);
        Assert.Equal((expectedStyle, FontWeight.SemiBold, FontStretch.Condensed), fonts.Collection.LastRequest);
    }

    [AvaloniaFact]
    public void FontRegisteredAfterFirstMissIsAvailableOnNextRequest()
    {
        var provider = new AvaloniaTypefaceProvider(false);
        var key = new Uri("fonts:SvgSkiaIssue565" + Guid.NewGuid().ToString("N"));
        var family = key + "#Inter";
        Assert.Null(Request(provider, family));

        using var fonts = new FontCollectionScope(Inter(), key);
        using var typeface = Request(provider, family);

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaFact]
    public void ReplacingCollectionDoesNotReusePreviousFontData()
    {
        var provider = new AvaloniaTypefaceProvider(false);
        using var fonts = new FontCollectionScope(Inter());
        using var first = Request(provider, fonts.Family);
        Assert.NotNull(first);
        Assert.Equal(400, first.FontWeight);

        fonts.Replace(Inter(FontWeight.Bold));
        using var second = Request(provider, fonts.Family);

        Assert.NotNull(second);
        Assert.Equal(700, second.FontWeight);
    }

    [AvaloniaFact]
    public void FontDataIsReadOnceAndEveryOpenedStreamIsDisposed()
    {
        var provider = new AvaloniaTypefaceProvider(false);
        var bytes = FontBytes(Inter());
        var opened = new List<TrackedStream>();
        var platform = StreamTypeface.Wrap(Inter().PlatformTypeface, () =>
        {
            var stream = new TrackedStream(bytes);
            opened.Add(stream);
            return stream;
        });
        using var fonts = new FontCollectionScope(new GlyphTypeface(platform));

        for (var index = 0; index < 3; index++)
        {
            using var typeface = Request(provider, fonts.Family);
            Assert.NotNull(typeface);
            using var font = new SKFont(typeface);
            Assert.True(font.ContainsGlyph('A'));
        }

        Assert.Single(opened);
        Assert.True(opened[0].WasDisposed);
    }

    [AvaloniaFact]
    public void UnavailableFontStreamCanBecomeAvailableLater()
    {
        var provider = new AvaloniaTypefaceProvider(false);
        var bytes = FontBytes(Inter());
        var available = false;
        var platform = StreamTypeface.Wrap(Inter().PlatformTypeface,
            () => available ? new MemoryStream(bytes) : null);
        using var fonts = new FontCollectionScope(new GlyphTypeface(platform));

        Assert.Null(Request(provider, fonts.Family));
        available = true;
        using var typeface = Request(provider, fonts.Family);

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaFact]
    public void UnreadableFontStreamDoesNotPreventRemainingFamilies()
    {
        var platform = StreamTypeface.Wrap(Inter().PlatformTypeface,
            () => throw new IOException("Font stream unavailable."));
        using var fonts = new FontCollectionScope(new GlyphTypeface(platform));
        var provider = new AvaloniaTypefaceProvider(false);

        using var typeface = Request(provider, fonts.Family + ", Inter");

        Assert.NotNull(typeface);
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaFact]
    public void CollectionFontUsesSelectedFaceInsteadOfAlwaysFaceZero()
    {
        var regular = Inter();
        var bold = Inter(FontWeight.Bold);
        var bytes = CreateCollection(FontBytes(regular), FontBytes(bold));
        var platform = StreamTypeface.Wrap(bold.PlatformTypeface, () => new MemoryStream(bytes));
        using var fonts = new FontCollectionScope(new GlyphTypeface(platform));
        var provider = new AvaloniaTypefaceProvider(false);

        for (var index = 0; index < 2; index++)
        {
            using var typeface = Request(provider, fonts.Family, SKFontStyleWeight.Bold);

            Assert.NotNull(typeface);
            Assert.Equal("Inter", typeface.FamilyName);
            Assert.Equal(700, typeface.FontWeight);
            using var font = new SKFont(typeface);
            Assert.True(font.ContainsGlyph('A'));
        }
    }

    private static SKTypeface? Request(AvaloniaTypefaceProvider provider, string family,
        SKFontStyleWeight weight = SKFontStyleWeight.Normal,
        SKFontStyleWidth width = SKFontStyleWidth.Normal,
        SKFontStyleSlant slant = SKFontStyleSlant.Upright)
    {
        return provider.FromFamilyName(family, weight, width, slant);
    }

    private static GlyphTypeface Inter(FontWeight weight = FontWeight.Normal)
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface("fonts:Inter#Inter", weight: weight), out var glyphTypeface));
        return glyphTypeface;
    }

    private static byte[] FontBytes(GlyphTypeface glyphTypeface)
    {
        Assert.True(glyphTypeface.PlatformTypeface.TryGetStream(out var stream));
        using (stream)
        using (var data = new MemoryStream())
        {
            stream.CopyTo(data);
            return data.ToArray();
        }
    }

    // Build a real TTC from the already bundled Inter fonts. SFNT table offsets are absolute
    // within the TTC, so relocate each font's table records after writing the TTC header.
    private static byte[] CreateCollection(params byte[][] fonts)
    {
        var headerLength = 12 + fonts.Length * 4;
        var bytes = new byte[headerLength + fonts.Sum(font => (font.Length + 3) & ~3)];
        "ttcf"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), (uint)fonts.Length);
        var offset = headerLength;
        for (var index = 0; index < fonts.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12 + index * 4, 4), (uint)offset);
            fonts[index].CopyTo(bytes, offset);
            var tableCount = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 4, 2));
            for (var table = 0; table < tableCount; table++)
            {
                var position = offset + 12 + table * 16 + 8;
                var original = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
                BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(position, 4), original + (uint)offset);
            }
            offset += (fonts[index].Length + 3) & ~3;
        }
        return bytes;
    }

    private sealed class TrackedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FontCollectionScope : IDisposable
    {
        public FontCollectionScope(GlyphTypeface glyphTypeface, Uri? key = null)
        {
            Key = key ?? new Uri("fonts:SvgSkiaIssue565" + Guid.NewGuid().ToString("N"));
            Collection = new TestFontCollection(Key, glyphTypeface);
            FontManager.Current.AddFontCollection(Collection);
        }

        public Uri Key { get; }
        public string Family => Key + "#Inter";
        public TestFontCollection Collection { get; private set; }

        public void Replace(GlyphTypeface glyphTypeface)
        {
            Collection = new TestFontCollection(Key, glyphTypeface);
            FontManager.Current.AddFontCollection(Collection);
        }

        public void Dispose() => FontManager.Current.RemoveFontCollection(Key);
    }

    private sealed class TestFontCollection(Uri key, GlyphTypeface glyphTypeface) : IFontCollection
    {
        public Uri Key => key;
        public int Count => 1;
        public FontFamily this[int index] => index == 0 ? new FontFamily(key + "#Inter") : throw new ArgumentOutOfRangeException(nameof(index));
        public (FontStyle Style, FontWeight Weight, FontStretch Stretch) LastRequest { get; private set; }

        public bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? result)
        {
            LastRequest = (style, weight, stretch);
            result = glyphTypeface;
            return true;
        }

        public bool TryMatchCharacter(int codepoint, FontStyle style, FontWeight weight, FontStretch stretch,
            string? familyName, CultureInfo? culture, out Typeface typeface)
        {
            typeface = default;
            return false;
        }

        public bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out IReadOnlyList<Typeface>? typefaces)
        {
            typefaces = null;
            return false;
        }

        public bool TryCreateSyntheticGlyphTypeface(GlyphTypeface typeface, FontStyle style, FontWeight weight,
            FontStretch stretch, [NotNullWhen(true)] out GlyphTypeface? synthetic)
        {
            synthetic = null;
            return false;
        }

        public bool TryGetNearestMatch(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? result) => TryGetGlyphTypeface(familyName, style, weight, stretch, out result);

        public IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)new[] { this[0] }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { } // Borrowed typeface is owned by Avalonia's Inter font collection.
    }

    // IPlatformTypeface is NotClientImplementable. The test proxy forwards real font tables,
    // while controlling stream availability/lifetime without introducing production reflection.
    public class StreamTypeface : DispatchProxy
    {
        private IPlatformTypeface _inner = null!;
        private Func<Stream?> _openStream = null!;

        public static IPlatformTypeface Wrap(IPlatformTypeface inner, Func<Stream?> openStream)
        {
            var proxy = Create<IPlatformTypeface, StreamTypeface>();
            var implementation = (StreamTypeface)(object)proxy;
            implementation._inner = inner;
            implementation._openStream = openStream;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IPlatformTypeface.TryGetStream))
            {
                var stream = _openStream();
                args![0] = stream;
                return stream is not null;
            }

            return targetMethod.Name == nameof(IDisposable.Dispose) ? null : targetMethod.Invoke(_inner, args);
        }
    }
}
