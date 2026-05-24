using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia
{
    /// <summary>
    /// Provides repo-owned SVG font rendering on top of the parsed Svg.Custom model so Skia text can
    /// consume embedded/external SVG glyph outlines without modifying the upstream externals/SVG sources.
    /// </summary>
    internal static class SvgFontTextRenderer
    {
        private const string EmptyAltGlyphLookupText = "\uFFFC";

        private static readonly ConditionalWeakTable<SvgDocument, SvgFontRegistry> s_registryCache = new();

        internal static bool TryGetLayout(
            SvgTextBase svgTextBase,
            string text,
            SKPaint paint,
            out SvgFontLayout? layout)
            => TryGetLayout(svgTextBase, text, paint, assetLoader: null, out layout);

        internal static bool TryGetLayout(
            SvgTextBase svgTextBase,
            string text,
            SKPaint paint,
            ISvgAssetLoader? assetLoader,
            out SvgFontLayout? layout)
            => TryGetLayout(svgTextBase, text, paint, assetLoader, includeGlyphTexts: false, out layout);

        internal static bool TryGetLayout(
            SvgTextBase svgTextBase,
            string text,
            SKPaint paint,
            ISvgAssetLoader? assetLoader,
            bool includeGlyphTexts,
            out SvgFontLayout? layout)
        {
            layout = null;

            if (assetLoader is { EnableSvgFonts: false })
            {
                return false;
            }

            var isAltGlyph = svgTextBase is SvgAltGlyph;
            var isEmptyAltGlyph = isAltGlyph && string.IsNullOrEmpty(text);
            if (paint.TextSize <= 0f ||
                (string.IsNullOrEmpty(text) && !isEmptyAltGlyph))
            {
                return false;
            }

            var document = svgTextBase.OwnerDocument;
            if (document is null || string.IsNullOrWhiteSpace(svgTextBase.FontFamily))
            {
                return false;
            }

            var registry = s_registryCache.GetValue(document, static doc => SvgFontRegistry.Create(doc));
            if (!registry.HasEntries)
            {
                return false;
            }

            var request = SvgFontRequest.Create(
                svgTextBase,
                isEmptyAltGlyph ? EmptyAltGlyphLookupText : text,
                paint.TextSize);
            if (TryCreateAltGlyphLayout(registry, request, paint, assetLoader, includeGlyphTexts, out layout))
            {
                return true;
            }

            if (isEmptyAltGlyph)
            {
                return false;
            }

            foreach (var family in request.Families)
            {
                if (!registry.TryGetEntries(family, out var familyEntries))
                {
                    continue;
                }

                if (familyEntries.Count == 1)
                {
                    var entry = familyEntries[0];
                    if (IsCompatibleLayoutCandidate(entry, request) &&
                        entry.TryCreateLayout(request, paint, assetLoader, includeGlyphTexts, out layout) &&
                        layout is not null)
                    {
                        return true;
                    }

                    continue;
                }

                var compatibleEntries = new List<SvgFontEntry>(familyEntries.Count);
                for (var i = 0; i < familyEntries.Count; i++)
                {
                    var entry = familyEntries[i];
                    if (IsCompatibleLayoutCandidate(entry, request))
                    {
                        compatibleEntries.Add(entry);
                    }
                }

                compatibleEntries.Sort((left, right) => CompareCompatibleLayoutCandidates(left, right, request));
                for (var i = 0; i < compatibleEntries.Count; i++)
                {
                    if (compatibleEntries[i].TryCreateLayout(request, paint, assetLoader, includeGlyphTexts, out layout) && layout is not null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsCompatibleLayoutCandidate(SvgFontEntry entry, SvgFontRequest request)
        {
            return entry.IsVariantCompatible(request) &&
                   entry.IsStyleCompatible(request) &&
                   entry.SupportsAnyText(request.Text);
        }

        private static int CompareCompatibleLayoutCandidates(SvgFontEntry left, SvgFontEntry right, SvgFontRequest request)
        {
            var styleDistance = left.GetStyleDistance(request).CompareTo(right.GetStyleDistance(request));
            if (styleDistance != 0)
            {
                return styleDistance;
            }

            var weightDistance = left.GetWeightDistance(request).CompareTo(right.GetWeightDistance(request));
            return weightDistance != 0
                ? weightDistance
                : left.Order.CompareTo(right.Order);
        }

        private static bool TryCreateAltGlyphLayout(
            SvgFontRegistry registry,
            SvgFontRequest request,
            SKPaint paint,
            ISvgAssetLoader? assetLoader,
            bool includeGlyphTexts,
            out SvgFontLayout? layout)
        {
            layout = null;
            if (request.StyleSource is not SvgAltGlyph altGlyph)
            {
                return false;
            }

            if (!TryResolveAltGlyphEntries(registry, request, altGlyph, out var glyphEntries) ||
                glyphEntries.Count == 0)
            {
                return false;
            }

            var entry = glyphEntries[0].Entry;
            var glyphs = new SvgGlyphDefinition[glyphEntries.Count];
            for (var i = 0; i < glyphEntries.Count; i++)
            {
                if (!ReferenceEquals(glyphEntries[i].Entry, entry))
                {
                    return false;
                }

                glyphs[i] = glyphEntries[i].Glyph;
            }

            layout = entry.CreateAltGlyphLayout(request, glyphs, paint, assetLoader, includeGlyphTexts);
            return layout.GlyphPlacements.Count > 0;
        }

        private static bool TryResolveAltGlyphEntries(
            SvgFontRegistry registry,
            SvgFontRequest request,
            SvgAltGlyph altGlyph,
            out IReadOnlyList<SvgGlyphEntry> glyphEntries)
        {
            if (TryResolveReferencedAltGlyphTarget(registry, request, altGlyph, altGlyph.ReferencedElement, out glyphEntries))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(altGlyph.GlyphRef) &&
                registry.TryResolveGlyphName(request, altGlyph.GlyphRef, out var glyphEntry))
            {
                glyphEntries = new[] { glyphEntry };
                return true;
            }

            glyphEntries = Array.Empty<SvgGlyphEntry>();
            return false;
        }

        private static bool TryResolveReferencedAltGlyphTarget(
            SvgFontRegistry registry,
            SvgFontRequest request,
            SvgElement owner,
            Uri? reference,
            out IReadOnlyList<SvgGlyphEntry> glyphEntries)
        {
            glyphEntries = Array.Empty<SvgGlyphEntry>();
            var uri = SvgService.GetEffectiveReferenceUri(owner, reference);
            if (uri is null)
            {
                return false;
            }

            var referencedElement = SvgService.GetReference<SvgElement>(owner, uri);
            return referencedElement switch
            {
                SvgGlyph glyph when registry.TryResolveGlyph(glyph, out var glyphEntry) => ReturnSingle(glyphEntry, out glyphEntries),
                SvgGlyphRef glyphRef => TryResolveGlyphRef(registry, request, glyphRef, out var glyphRefEntry) && ReturnSingle(glyphRefEntry, out glyphEntries),
                SvgAltGlyphDef altGlyphDef => TryResolveAltGlyphDef(registry, request, altGlyphDef, out glyphEntries),
                SvgAltGlyphItem altGlyphItem => TryResolveGlyphRefSequence(registry, request, altGlyphItem.Children.OfType<SvgGlyphRef>(), out glyphEntries),
                _ => false
            };
        }

        private static bool ReturnSingle(SvgGlyphEntry glyphEntry, out IReadOnlyList<SvgGlyphEntry> glyphEntries)
        {
            glyphEntries = new[] { glyphEntry };
            return true;
        }

        private static bool TryResolveAltGlyphDef(
            SvgFontRegistry registry,
            SvgFontRequest request,
            SvgAltGlyphDef altGlyphDef,
            out IReadOnlyList<SvgGlyphEntry> glyphEntries)
        {
            var directGlyphRefs = altGlyphDef.Children.OfType<SvgGlyphRef>().ToArray();
            if (directGlyphRefs.Length > 0 &&
                TryResolveGlyphRefSequence(registry, request, directGlyphRefs, out glyphEntries))
            {
                return true;
            }

            foreach (var item in altGlyphDef.Children.OfType<SvgAltGlyphItem>())
            {
                if (TryResolveGlyphRefSequence(registry, request, item.Children.OfType<SvgGlyphRef>(), out glyphEntries))
                {
                    return true;
                }
            }

            glyphEntries = Array.Empty<SvgGlyphEntry>();
            return false;
        }

        private static bool TryResolveGlyphRefSequence(
            SvgFontRegistry registry,
            SvgFontRequest request,
            IEnumerable<SvgGlyphRef> glyphRefs,
            out IReadOnlyList<SvgGlyphEntry> glyphEntries)
        {
            var resolved = new List<SvgGlyphEntry>();
            foreach (var glyphRef in glyphRefs)
            {
                if (!TryResolveGlyphRef(registry, request, glyphRef, out var glyphEntry))
                {
                    glyphEntries = Array.Empty<SvgGlyphEntry>();
                    return false;
                }

                resolved.Add(glyphEntry);
            }

            glyphEntries = resolved;
            return resolved.Count > 0;
        }

        private static bool TryResolveGlyphRef(
            SvgFontRegistry registry,
            SvgFontRequest request,
            SvgGlyphRef glyphRef,
            out SvgGlyphEntry glyphEntry)
        {
            if (TryResolveReferencedAltGlyphTarget(registry, request, glyphRef, glyphRef.ReferencedElement, out var referencedEntries) &&
                referencedEntries.Count == 1)
            {
                glyphEntry = referencedEntries[0];
                return true;
            }

            if (!string.IsNullOrWhiteSpace(glyphRef.GlyphRef) &&
                registry.TryResolveGlyphName(request, glyphRef.GlyphRef, out glyphEntry))
            {
                return true;
            }

            glyphEntry = default;
            return false;
        }

        private enum SvgTextDirection
        {
            LeftToRight,
            RightToLeft
        }

        internal enum SvgArabicForm
        {
            None,
            Isolated,
            Initial,
            Medial,
            Terminal
        }

        private enum ArabicJoiningType
        {
            NonJoining,
            RightJoining,
            DualJoining,
            JoinCausing,
            Transparent
        }

        private readonly record struct SvgFontRequest(
            SvgTextBase StyleSource,
            string Text,
            float TextSize,
            string[] Families,
            string? Language,
            SvgFontStyle Style,
            SvgFontVariant Variant,
            int Weight,
            SvgTextDirection Direction)
        {
            public static SvgFontRequest Create(SvgTextBase svgTextBase, string text, float textSize)
            {
                return new SvgFontRequest(
                    svgTextBase,
                    text,
                    textSize,
                    SplitFamilies(svgTextBase.FontFamily),
                    GetLanguage(svgTextBase),
                    NormalizeFontStyle(svgTextBase.FontStyle),
                    NormalizeFontVariant(svgTextBase.FontVariant),
                    NormalizeFontWeight(svgTextBase),
                    GetDirection(svgTextBase));
            }

            private static string[] SplitFamilies(string? fontFamily)
            {
                return (fontFamily ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim().Trim('"', '\''))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
            }

            private static string? GetLanguage(SvgTextBase svgTextBase)
            {
                for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
                {
                    if (current.TryGetAttribute("xml:lang", out var xmlLang) && !string.IsNullOrWhiteSpace(xmlLang))
                    {
                        return NormalizeLanguageTag(xmlLang);
                    }

                    if (current.TryGetAttribute("lang", out var lang) && !string.IsNullOrWhiteSpace(lang))
                    {
                        return NormalizeLanguageTag(lang);
                    }
                }

                return null;
            }

            private static string NormalizeLanguageTag(string value)
            {
                return value.Trim().Replace('_', '-');
            }

            private static SvgTextDirection GetDirection(SvgTextBase svgTextBase)
            {
                if (svgTextBase.TryGetAttribute("direction", out var direction) &&
                    direction.Equals("rtl", StringComparison.OrdinalIgnoreCase))
                {
                    return SvgTextDirection.RightToLeft;
                }

                return SvgTextDirection.LeftToRight;
            }

            private static SvgFontStyle NormalizeFontStyle(SvgFontStyle style)
            {
                return style switch
                {
                    SvgFontStyle.Italic => SvgFontStyle.Italic,
                    SvgFontStyle.Oblique => SvgFontStyle.Oblique,
                    _ => SvgFontStyle.Normal
                };
            }

            private static SvgFontVariant NormalizeFontVariant(SvgFontVariant variant)
            {
                return variant == SvgFontVariant.SmallCaps
                    ? SvgFontVariant.SmallCaps
                    : SvgFontVariant.Normal;
            }

            private static int NormalizeFontWeight(SvgTextBase svgTextBase)
            {
                var weight = PaintingService.ResolveFontWeight(svgTextBase, svgTextBase.FontWeight);
                if (weight.HasFlag(SvgFontWeight.W900))
                {
                    return 900;
                }
                if (weight.HasFlag(SvgFontWeight.W800))
                {
                    return 800;
                }
                if (weight.HasFlag(SvgFontWeight.W700) || weight == SvgFontWeight.Bold)
                {
                    return 700;
                }
                if (weight.HasFlag(SvgFontWeight.W600))
                {
                    return 600;
                }
                if (weight.HasFlag(SvgFontWeight.W500))
                {
                    return 500;
                }
                if (weight.HasFlag(SvgFontWeight.W400) || weight == SvgFontWeight.Normal)
                {
                    return 400;
                }
                if (weight.HasFlag(SvgFontWeight.W300))
                {
                    return 300;
                }
                if (weight.HasFlag(SvgFontWeight.W200))
                {
                    return 200;
                }
                if (weight.HasFlag(SvgFontWeight.W100))
                {
                    return 100;
                }

                return 400;
            }
        }

        internal sealed class SvgFontLayout
        {
            private readonly IReadOnlyList<SvgGlyphPlacementResult> _glyphs;
            private readonly IReadOnlyList<string>? _glyphTexts;
            private readonly SKRect _relativeBounds;

            internal SvgFontLayout(
                IReadOnlyList<SvgGlyphPlacementResult> glyphs,
                float advance,
                SKRect relativeBounds,
                IReadOnlyList<string>? glyphTexts = null)
            {
                _glyphs = glyphs;
                _glyphTexts = glyphTexts;
                Advance = advance;
                _relativeBounds = relativeBounds;
            }

            public float Advance { get; }

            public IReadOnlyList<SvgGlyphPlacementResult> GlyphPlacements => _glyphs;

            public IReadOnlyList<string>? GlyphTexts => _glyphTexts;

            public SKRect GetBounds(float startX, float baselineY)
            {
                if (_relativeBounds.IsEmpty)
                {
                    return SKRect.Empty;
                }

                return new SKRect(
                    _relativeBounds.Left + startX,
                    _relativeBounds.Top + baselineY,
                    _relativeBounds.Right + startX,
                    _relativeBounds.Bottom + baselineY);
            }

            public void AppendPath(SKPath targetPath, float startX, float baselineY)
            {
                for (var i = 0; i < _glyphs.Count; i++)
                {
                    var glyph = _glyphs[i];
                    if (glyph.RelativePath is null || glyph.RelativePath.IsEmpty)
                    {
                        continue;
                    }

                    var translated = glyph.RelativePath.DeepClone();
                    translated.Transform(SKMatrix.CreateTranslation(startX, baselineY));
                    SvgFontTextRenderer.AppendPathCommands(targetPath, translated);
                }
            }

            public void Draw(SKCanvas canvas, SKPaint paint, float startX, float baselineY)
            {
                var path = new SKPath();
                AppendPath(path, startX, baselineY);
                if (!path.IsEmpty)
                {
                    canvas.DrawPath(path, paint);
                }
            }

        }

        internal sealed record SvgGlyphPlacementResult(SvgGlyphDefinition? Glyph, float RelativeX, float Advance, SKPath? RelativePath, SKRect RelativeBounds);

        private sealed class SvgFontRegistry
        {
            private readonly Dictionary<string, List<SvgFontEntry>> _entriesByFamily = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<SvgGlyph, SvgGlyphEntry> _entriesByGlyph = new();
            private int _nextOrder;

            private SvgFontRegistry()
            {
            }

            public bool HasEntries => _entriesByFamily.Count > 0;

            public bool TryGetEntries(string family, out IReadOnlyList<SvgFontEntry> entries)
            {
                if (_entriesByFamily.TryGetValue(family, out var list))
                {
                    entries = list;
                    return true;
                }

                entries = Array.Empty<SvgFontEntry>();
                return false;
            }

            public bool TryResolveGlyph(SvgGlyph glyph, out SvgGlyphEntry entry)
                => _entriesByGlyph.TryGetValue(glyph, out entry);

            public bool TryResolveGlyphName(SvgFontRequest request, string glyphName, out SvgGlyphEntry entry)
            {
                foreach (var family in request.Families)
                {
                    if (!_entriesByFamily.TryGetValue(family, out var familyEntries))
                    {
                        continue;
                    }

                    for (var i = 0; i < familyEntries.Count; i++)
                    {
                        var fontEntry = familyEntries[i];
                        if (!fontEntry.IsVariantCompatible(request) ||
                            !fontEntry.IsStyleCompatible(request))
                        {
                            continue;
                        }

                        if (fontEntry.TryResolveGlyphName(glyphName, out var glyph))
                        {
                            entry = new SvgGlyphEntry(fontEntry, glyph);
                            return true;
                        }
                    }
                }

                entry = default;
                return false;
            }

            public static SvgFontRegistry Create(SvgDocument document)
            {
                var registry = new SvgFontRegistry();
                registry.AddEmbeddedFonts(document);
                registry.AddExternalFontFaces(document);
                registry.AddCssFontFaces(document);
                return registry;
            }

            private void AddEmbeddedFonts(SvgDocument document)
            {
                foreach (var font in document.Descendants().OfType<SvgFont>())
                {
                    var metricsFace = font.Children.OfType<SvgFontFace>().FirstOrDefault();
                    if (metricsFace is null)
                    {
                        continue;
                    }

                    if (!TryReadAttribute(metricsFace, "font-family", out var family) || string.IsNullOrWhiteSpace(family))
                    {
                        continue;
                    }

                    var descriptor = SvgFontDescriptor.FromSvgFontFace(metricsFace, family, uri: null);
                    AddEntry(new SvgFontEntry(_nextOrder++, font, metricsFace, descriptor));
                }
            }

            private void AddExternalFontFaces(SvgDocument document)
            {
                foreach (var fontFace in document.Descendants().OfType<SvgFontFace>())
                {
                    if (fontFace.Parent is SvgFont)
                    {
                        continue;
                    }

                    var uri = fontFace.Descendants().OfType<SvgFontFaceUri>().FirstOrDefault()?.ReferencedElement;
                    if (uri is null)
                    {
                        continue;
                    }

                    var font = document.GetElementById<SvgFont>(uri);
                    var metricsFace = font?.Children.OfType<SvgFontFace>().FirstOrDefault();
                    if (font is null || metricsFace is null)
                    {
                        continue;
                    }

                    var family = TryReadAttribute(fontFace, "font-family", out var declaredFamily) && !string.IsNullOrWhiteSpace(declaredFamily)
                        ? declaredFamily
                        : TryReadAttribute(metricsFace, "font-family", out var actualFamily) ? actualFamily : null;
                    if (string.IsNullOrWhiteSpace(family))
                    {
                        continue;
                    }

                    var descriptor = SvgFontDescriptor.FromSvgFontFace(fontFace, family!, uri);
                    AddEntry(new SvgFontEntry(_nextOrder++, font, metricsFace, descriptor));
                }
            }

            private void AddCssFontFaces(SvgDocument document)
            {
                foreach (var unknown in document.Descendants().OfType<SvgUnknownElement>())
                {
                    if (string.IsNullOrWhiteSpace(unknown.Content) ||
                        unknown.Content.IndexOf("@font-face", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    foreach (var cssFace in ParseCssFontFaces(unknown.Content))
                    {
                        if (!CanResolveSvgFontReference(cssFace.SourceUri))
                        {
                            continue;
                        }

                        var font = document.GetElementById<SvgFont>(cssFace.SourceUri);
                        var metricsFace = font?.Children.OfType<SvgFontFace>().FirstOrDefault();
                        if (font is null || metricsFace is null)
                        {
                            continue;
                        }

                        AddEntry(new SvgFontEntry(_nextOrder++, font, metricsFace, cssFace));
                    }
                }
            }

            private static bool CanResolveSvgFontReference(Uri? sourceUri)
            {
                if (sourceUri is null)
                {
                    return false;
                }

                if (sourceUri.IsAbsoluteUri)
                {
                    return !string.IsNullOrWhiteSpace(sourceUri.Fragment);
                }

                return sourceUri.OriginalString.IndexOf('#') >= 0;
            }

            private void AddEntry(SvgFontEntry entry)
            {
                if (!_entriesByFamily.TryGetValue(entry.Descriptor.Family, out var list))
                {
                    list = new List<SvgFontEntry>();
                    _entriesByFamily[entry.Descriptor.Family] = list;
                }

                list.Add(entry);

                foreach (var glyph in entry.Glyphs)
                {
                    if (glyph.SourceGlyph is not null && !_entriesByGlyph.ContainsKey(glyph.SourceGlyph))
                    {
                        _entriesByGlyph[glyph.SourceGlyph] = new SvgGlyphEntry(entry, glyph);
                    }
                }
            }

            private static IEnumerable<SvgFontDescriptor> ParseCssFontFaces(string css)
            {
                foreach (Match blockMatch in s_fontFaceBlockRegex.Matches(css))
                {
                    var body = blockMatch.Groups["body"].Value;
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match declarationMatch in s_fontFaceDeclarationRegex.Matches(body))
                    {
                        values[declarationMatch.Groups["name"].Value.Trim()] = declarationMatch.Groups["value"].Value.Trim();
                    }

                    if (!values.TryGetValue("font-family", out var family) || string.IsNullOrWhiteSpace(family) ||
                        !values.TryGetValue("src", out var src))
                    {
                        continue;
                    }

                    var urlMatch = s_cssUrlRegex.Match(src);
                    if (!urlMatch.Success)
                    {
                        continue;
                    }

                    family = family.Trim().Trim('"', '\'');
                    var uriValue = urlMatch.Groups["url"].Value.Trim().Trim('"', '\'');
                    if (!Uri.TryCreate(uriValue, UriKind.RelativeOrAbsolute, out var sourceUri) || string.IsNullOrWhiteSpace(family))
                    {
                        continue;
                    }

                    yield return SvgFontDescriptor.FromCssFontFace(
                        family,
                        sourceUri,
                        values.TryGetValue("font-style", out var style) ? style : null,
                        values.TryGetValue("font-variant", out var variant) ? variant : null,
                        values.TryGetValue("font-weight", out var weight) ? weight : null,
                        values.TryGetValue("unicode-range", out var unicodeRange) ? unicodeRange : null);
                }
            }

            private static readonly Regex s_fontFaceBlockRegex = new(@"@font-face\s*\{(?<body>.*?)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            private static readonly Regex s_fontFaceDeclarationRegex = new(@"(?<name>[a-zA-Z-]+)\s*:\s*(?<value>[^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static readonly Regex s_cssUrlRegex = new(@"url\((?<url>[^\)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private sealed class SvgFontEntry
        {
            private readonly IReadOnlyList<SvgGlyphDefinition> _glyphs;
            private readonly Dictionary<char, List<SvgGlyphDefinition>> _glyphsByFirstChar;
            private readonly IReadOnlyList<SvgKernRule> _kernRules;

            public SvgFontEntry(int order, SvgFont font, SvgFontFace metricsFace, SvgFontDescriptor descriptor)
            {
                Order = order;
                Font = font;
                MetricsFace = metricsFace;
                Descriptor = descriptor;
                _glyphs = CreateGlyphs(font);
                _glyphsByFirstChar = CreateGlyphLookup(_glyphs);
                _kernRules = font.Descendants().OfType<SvgKern>().Select(rule => SvgKernRule.Create(rule)).Where(rule => rule is not null).Cast<SvgKernRule>().ToList();
                MissingGlyph = font.Children.OfType<SvgMissingGlyph>().FirstOrDefault() is { } missingGlyph
                    ? SvgGlyphDefinition.Create(missingGlyph, font.HorizOriginX)
                    : null;
                UnitsPerEm = metricsFace.UnitsPerEm > 0f ? metricsFace.UnitsPerEm : 1000f;
                Ascent = metricsFace.Ascent;
                Descent = Math.Abs(metricsFace.Descent);
                Alphabetic = metricsFace.Alphabetic;
            }

            public int Order { get; }
            public SvgFont Font { get; }
            public SvgFontFace MetricsFace { get; }
            public SvgFontDescriptor Descriptor { get; }
            public SvgGlyphDefinition? MissingGlyph { get; }
            public IReadOnlyList<SvgGlyphDefinition> Glyphs => _glyphs;
            public float UnitsPerEm { get; }
            public float Ascent { get; }
            public float Descent { get; }
            public float Alphabetic { get; }

            public bool SupportsText(string text)
            {
                return Descriptor.UnicodeRange is null || Descriptor.UnicodeRange.Supports(text);
            }

            public bool SupportsAnyText(string text)
            {
                return Descriptor.UnicodeRange is null || Descriptor.UnicodeRange.SupportsAny(text);
            }

            public bool TryResolveGlyphName(string glyphName, out SvgGlyphDefinition glyph)
            {
                for (var i = 0; i < _glyphs.Count; i++)
                {
                    var candidate = _glyphs[i];
                    if (candidate.MatchesGlyphName(glyphName))
                    {
                        glyph = candidate;
                        return true;
                    }
                }

                glyph = null!;
                return false;
            }

            public bool IsVariantCompatible(SvgFontRequest request)
            {
                return Descriptor.Variant == request.Variant;
            }

            public bool IsStyleCompatible(SvgFontRequest request)
            {
                return request.Style switch
                {
                    SvgFontStyle.Normal => Descriptor.Style == SvgFontStyle.Normal,
                    SvgFontStyle.Italic => Descriptor.Style == SvgFontStyle.Italic || Descriptor.Style == SvgFontStyle.Oblique,
                    SvgFontStyle.Oblique => Descriptor.Style == SvgFontStyle.Oblique,
                    _ => Descriptor.Style == SvgFontStyle.Normal
                };
            }

            public int GetStyleDistance(SvgFontRequest request)
            {
                if (Descriptor.Style == request.Style)
                {
                    return 0;
                }

                return request.Style switch
                {
                    SvgFontStyle.Italic when Descriptor.Style == SvgFontStyle.Oblique => 1,
                    _ => 10
                };
            }

            public int GetWeightDistance(SvgFontRequest request)
            {
                return Math.Abs(Descriptor.Weight - request.Weight);
            }

            public bool TryCreateLayout(
                SvgFontRequest request,
                SKPaint paint,
                ISvgAssetLoader? assetLoader,
                bool includeGlyphTexts,
                out SvgFontLayout? layout)
            {
                layout = null;

                var codepoints = CodepointInfo.Parse(request.Text);
                if (codepoints.Count == 0)
                {
                    return false;
                }

                var logicalItems = new List<SvgResolvedItem>(codepoints.Count);
                var hasSvgGlyph = false;
                for (var codepointIndex = 0; codepointIndex < codepoints.Count;)
                {
                    var start = codepoints[codepointIndex];
                    if (!SupportsCodepoint(start))
                    {
                        logicalItems.Add(new SvgFallbackTextItem(start.GetText(request.Text)));
                        codepointIndex++;
                        continue;
                    }

                    if (!TryResolveGlyph(request.Text, codepoints, codepointIndex, request.Language, out var glyph, out var consumedCodepoints, out var requiresFontFallback))
                    {
                        var fallbackText = start.GetText(request.Text);
                        if (requiresFontFallback ||
                            MissingGlyph is null ||
                            HasUsableFallbackText(fallbackText, paint, assetLoader))
                        {
                            logicalItems.Add(new SvgFallbackTextItem(fallbackText));
                            codepointIndex++;
                            continue;
                        }

                        glyph = MissingGlyph;
                        consumedCodepoints = 1;
                    }

                    var endCharIndex = codepointIndex + consumedCodepoints < codepoints.Count
                        ? codepoints[codepointIndex + consumedCodepoints].CharIndex
                        : request.Text.Length;
                    var consumedText = CreateResolvedGlyphText(request.Text, start.CharIndex, endCharIndex, glyph);
                    logicalItems.Add(new SvgResolvedGlyphItem(glyph, consumedText, consumedText));
                    hasSvgGlyph = true;
                    codepointIndex += consumedCodepoints;
                }

                if (!hasSvgGlyph)
                {
                    return false;
                }

                var visualItems = request.Direction == SvgTextDirection.RightToLeft
                    ? logicalItems.AsEnumerable().Reverse().ToList()
                    : logicalItems;
                layout = CreateLayout(visualItems, request, paint, assetLoader, includeGlyphTexts);
                return true;
            }

            public SvgFontLayout CreateAltGlyphLayout(
                SvgFontRequest request,
                IReadOnlyList<SvgGlyphDefinition> glyphs,
                SKPaint paint,
                ISvgAssetLoader? assetLoader,
                bool includeGlyphTexts)
            {
                var logicalItems = new List<SvgResolvedItem>(glyphs.Count);
                IReadOnlyList<SvgGlyphDefinition> orderedGlyphs = request.Direction == SvgTextDirection.RightToLeft
                    ? glyphs.Reverse().ToArray()
                    : glyphs;
                var logicalText = request.Text == EmptyAltGlyphLookupText
                    ? string.Empty
                    : request.Text;
                var baselineText = string.IsNullOrEmpty(logicalText)
                    ? CreateAltGlyphBaselineText(orderedGlyphs)
                    : logicalText;
                for (var i = 0; i < orderedGlyphs.Count; i++)
                {
                    logicalItems.Add(new SvgResolvedGlyphItem(
                        orderedGlyphs[i],
                        i == 0 ? logicalText : string.Empty,
                        baselineText));
                }

                return CreateLayout(logicalItems, request, paint, assetLoader, includeGlyphTexts);
            }

            private static string CreateAltGlyphBaselineText(IReadOnlyList<SvgGlyphDefinition> glyphs)
            {
                for (var i = 0; i < glyphs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(glyphs[i].Unicode))
                    {
                        return glyphs[i].Unicode!;
                    }
                }

                for (var i = 0; i < glyphs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(glyphs[i].GlyphName))
                    {
                        return glyphs[i].GlyphName!;
                    }
                }

                return string.Empty;
            }

            private static bool HasUsableFallbackText(string text, SKPaint paint, ISvgAssetLoader? assetLoader)
            {
                if (assetLoader is null || string.IsNullOrEmpty(text))
                {
                    return false;
                }

                var spans = assetLoader.FindTypefaces(text, paint);
                if (spans.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < spans.Count; i++)
                {
                    if (string.IsNullOrEmpty(spans[i].Text))
                    {
                        continue;
                    }

                    if (spans[i].Typeface is not null && spans[i].Advance > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool SupportsCodepoint(CodepointInfo codepoint)
            {
                return Descriptor.UnicodeRange is null || Descriptor.UnicodeRange.SupportsCodepoint(codepoint.Scalar);
            }

            private SvgFontLayout CreateLayout(
                IReadOnlyList<SvgResolvedItem> resolvedItems,
                SvgFontRequest request,
                SKPaint paint,
                ISvgAssetLoader? assetLoader,
                bool includeGlyphTexts)
            {
                var textSize = request.TextSize;
                var scale = textSize / UnitsPerEm;
                var ascent = Ascent * scale;
                var descent = Descent * scale;
                var placements = new List<SvgGlyphPlacementResult>(resolvedItems.Count);
                List<string>? placementTexts = includeGlyphTexts ? new List<string>(resolvedItems.Count) : null;
                var bounds = SKRect.Empty;
                var currentX = 0f;
                SvgResolvedGlyphItem? previousSvgGlyph = null;
                var previousAdvance = 0f;
                var hasPrevious = false;

                for (var i = 0; i < resolvedItems.Count; i++)
                {
                    var resolvedItem = resolvedItems[i];
                    if (hasPrevious)
                    {
                        currentX += previousAdvance;
                        if (previousSvgGlyph is not null && resolvedItem is SvgResolvedGlyphItem currentSvgGlyph)
                        {
                            currentX -= GetKerning(
                                new SvgResolvedGlyph(previousSvgGlyph.Definition, previousSvgGlyph.Text),
                                new SvgResolvedGlyph(currentSvgGlyph.Definition, currentSvgGlyph.Text)) * scale;
                        }
                    }

                    SvgGlyphDefinition? glyphDefinition = null;
                    SKPath? relativePath = null;
                    SKRect glyphBounds;
                    float advance;

                    if (resolvedItem is SvgResolvedGlyphItem svgGlyphItem)
                    {
                        glyphDefinition = svgGlyphItem.Definition;
                        var glyphOriginX = currentX;
                        advance = svgGlyphItem.Definition.HorizAdvX * scale;
                        var pathX = glyphOriginX - (svgGlyphItem.Definition.HorizOriginX * scale);
                        var transY = GetGlyphTransY(svgGlyphItem.BaselineText, request.StyleSource, textSize);
                        if (svgGlyphItem.Definition.PathTemplate is { IsEmpty: false } pathTemplate)
                        {
                            relativePath = pathTemplate.DeepClone();
                            var transform = new SKMatrix
                            {
                                ScaleX = scale,
                                SkewX = 0f,
                                TransX = pathX,
                                SkewY = 0f,
                                ScaleY = -scale,
                                TransY = transY,
                                Persp0 = 0f,
                                Persp1 = 0f,
                                Persp2 = 1f
                            };
                            relativePath.Transform(transform);
                        }

                        glyphBounds = relativePath is { IsEmpty: false }
                            ? relativePath.Bounds
                            : new SKRect(glyphOriginX, transY - ascent, glyphOriginX + advance, transY + descent);
                        previousSvgGlyph = svgGlyphItem;
                    }
                    else
                    {
                        if (assetLoader is null)
                        {
                            if (placements.Count > 0)
                            {
                                var lastPlacement = placements[placements.Count - 1];
                                return new SvgFontLayout(placements, lastPlacement.RelativeX + lastPlacement.Advance, bounds, placementTexts);
                            }

                            return new SvgFontLayout(placements, 0f, bounds, placementTexts);
                        }

                        var fallbackPlacement = CreateFallbackPlacement(resolvedItem.Text, currentX, paint, assetLoader);
                        relativePath = fallbackPlacement.RelativePath;
                        glyphBounds = fallbackPlacement.RelativeBounds;
                        advance = fallbackPlacement.Advance;
                        previousSvgGlyph = null;
                    }

                    bounds = bounds.IsEmpty ? glyphBounds : SKRect.Union(bounds, glyphBounds);
                    placements.Add(new SvgGlyphPlacementResult(glyphDefinition, currentX, advance, relativePath, glyphBounds));
                    placementTexts?.Add(resolvedItem.Text);
                    previousAdvance = advance;
                    hasPrevious = true;
                }

                var totalAdvance = 0f;
                if (placements.Count > 0)
                {
                    var lastPlacement = placements[placements.Count - 1];
                    totalAdvance = lastPlacement.RelativeX + lastPlacement.Advance;
                }

                return new SvgFontLayout(placements, totalAdvance, bounds, placementTexts);
            }

            private static SvgGlyphPlacementResult CreateFallbackPlacement(string text, float currentX, SKPaint paint, ISvgAssetLoader assetLoader)
            {
                var relativePath = new SKPath();
                var totalAdvance = 0f;
                var localX = currentX;
                var spans = assetLoader.FindTypefaces(text, paint);
                if (spans.Count == 0)
                {
                    AppendPathCommands(relativePath, assetLoader.GetTextPath(text, paint, currentX, 0f));
                    var measuredBounds = new SKRect();
                    totalAdvance = assetLoader.MeasureText(text, paint, ref measuredBounds);
                    var fallbackBounds = relativePath.IsEmpty
                        ? new SKRect(currentX, measuredBounds.Top, currentX + totalAdvance, measuredBounds.Bottom)
                        : relativePath.Bounds;
                    return new SvgGlyphPlacementResult(null, currentX, totalAdvance, relativePath.IsEmpty ? null : relativePath, fallbackBounds);
                }

                for (var i = 0; i < spans.Count; i++)
                {
                    var localPaint = paint.Clone();
                    localPaint.TextAlign = SKTextAlign.Left;
                    localPaint.Typeface = spans[i].Typeface;
                    AppendPathCommands(relativePath, assetLoader.GetTextPath(spans[i].Text, localPaint, localX, 0f));
                    localX += spans[i].Advance;
                    totalAdvance += spans[i].Advance;
                }

                var metrics = assetLoader.GetFontMetrics(paint);
                var bounds = relativePath.IsEmpty
                    ? new SKRect(currentX, metrics.Ascent, currentX + totalAdvance, metrics.Descent)
                    : relativePath.Bounds;
                return new SvgGlyphPlacementResult(null, currentX, totalAdvance, relativePath.IsEmpty ? null : relativePath, bounds);
            }

            private float GetKerning(SvgResolvedGlyph left, SvgResolvedGlyph right)
            {
                for (var i = 0; i < _kernRules.Count; i++)
                {
                    if (_kernRules[i].Matches(left, right))
                    {
                        return _kernRules[i].Kerning;
                    }
                }

                return 0f;
            }

            private float GetGlyphTransY(string text, SvgTextBase styleSource, float textSize)
            {
                var scriptBaseline = ResolveScriptBaseline(text);
                var scriptCoordinate = GetBaselineCoordinate(MetricsFace, scriptBaseline);
                var dominantCoordinate = GetBaselineCoordinate(MetricsFace, ResolveDominantBaseline(styleSource, text));
                var tableTextSize = ResolveBaselineTableTextSize(styleSource, textSize);
                var glyphScale = textSize / UnitsPerEm;
                var tableScale = tableTextSize / UnitsPerEm;
                return (scriptCoordinate * glyphScale) + ((dominantCoordinate - scriptCoordinate) * tableScale);
            }

            private static float ResolveBaselineTableTextSize(SvgTextBase styleSource, float fallbackTextSize)
            {
                if (CreatesOwnBaselineTable(styleSource))
                {
                    return fallbackTextSize;
                }

                for (SvgElement? current = styleSource.Parent; current is not null; current = current.Parent)
                {
                    if (current is not SvgTextBase parentText)
                    {
                        continue;
                    }

                    return ResolveTextSize(parentText, fallbackTextSize);
                }

                return fallbackTextSize;
            }

            private static bool CreatesOwnBaselineTable(SvgTextBase styleSource)
            {
                if (!TryReadSpecifiedDominantBaseline(styleSource, out var value))
                {
                    return styleSource.Parent is not SvgTextBase;
                }

                value = value.Trim();
                return !value.Equals("inherit", StringComparison.OrdinalIgnoreCase) &&
                       !value.Equals("no-change", StringComparison.OrdinalIgnoreCase);
            }

            private static float ResolveTextSize(SvgTextBase svgTextBase, float fallbackTextSize)
            {
                if (!svgTextBase.FontSize.IsEmpty &&
                    !svgTextBase.FontSize.IsNone &&
                    svgTextBase.FontSize.Value > 0f &&
                    svgTextBase.FontSize.Type is SvgUnitType.User or SvgUnitType.Pixel)
                {
                    return svgTextBase.FontSize.Value;
                }

                var fontSize = svgTextBase.FontSize.ToDeviceValue(UnitRenderingType.Vertical, svgTextBase, SKRect.Empty);
                if (fontSize > 0f)
                {
                    return fontSize;
                }

                var paint = new SKPaint();
                PaintingService.SetPaintText(svgTextBase, SKRect.Empty, paint);
                return paint.TextSize > 0f ? paint.TextSize : fallbackTextSize;
            }

            private static SvgDominantBaseline ResolveDominantBaseline(SvgTextBase styleSource, string text)
            {
                if (TryReadAlignmentBaseline(styleSource, out var alignmentBaseline))
                {
                    return alignmentBaseline;
                }

                if (TryReadSpecifiedDominantBaseline(styleSource, out var value))
                {
                    return value.Trim().ToLowerInvariant() switch
                    {
                        "ideographic" => SvgDominantBaseline.Ideographic,
                        "alphabetic" => SvgDominantBaseline.Alphabetic,
                        "hanging" => SvgDominantBaseline.Hanging,
                        "mathematical" => SvgDominantBaseline.Mathematical,
                        "central" => SvgDominantBaseline.Central,
                        "middle" => SvgDominantBaseline.Middle,
                        "text-after-edge" or "after-edge" or "text-bottom" => SvgDominantBaseline.TextAfterEdge,
                        "text-before-edge" or "before-edge" or "text-top" => SvgDominantBaseline.TextBeforeEdge,
                        "use-script" => ResolveScriptBaseline(text),
                        "inherit" or "no-change" => ResolveInheritedDominantBaseline(styleSource),
                        _ => GetDefaultDominantBaseline(styleSource)
                    };
                }

                return styleSource.Parent is SvgTextBase
                    ? ResolveInheritedDominantBaseline(styleSource)
                    : GetDefaultDominantBaseline(styleSource);
            }

            private static bool TryReadAlignmentBaseline(SvgTextBase styleSource, out SvgDominantBaseline baseline)
            {
                baseline = SvgDominantBaseline.Auto;
                if ((!styleSource.ComputedStyle.TryGetPropertyValue("alignment-baseline", out var value) ||
                     string.IsNullOrWhiteSpace(value)) &&
                    (!styleSource.TryGetAttribute("alignment-baseline", out value) ||
                     string.IsNullOrWhiteSpace(value)))
                {
                    return false;
                }

                baseline = value.Trim().ToLowerInvariant() switch
                {
                    "baseline" or "alphabetic" => SvgDominantBaseline.Alphabetic,
                    "middle" or "center" => SvgDominantBaseline.Middle,
                    "central" => SvgDominantBaseline.Central,
                    "mathematical" => SvgDominantBaseline.Mathematical,
                    "ideographic" => SvgDominantBaseline.Ideographic,
                    "hanging" => SvgDominantBaseline.Hanging,
                    "text-before-edge" or "before-edge" or "text-top" => SvgDominantBaseline.TextBeforeEdge,
                    "text-after-edge" or "after-edge" or "text-bottom" => SvgDominantBaseline.TextAfterEdge,
                    "inherit" => ResolveInheritedDominantBaseline(styleSource),
                    _ => SvgDominantBaseline.Auto
                };
                return baseline != SvgDominantBaseline.Auto;
            }

            private static bool TryReadSpecifiedDominantBaseline(SvgTextBase styleSource, out string value)
            {
                if (styleSource.TryGetAttribute("dominant-baseline", out value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                value = string.Empty;
                return false;
            }

            private static SvgDominantBaseline ResolveInheritedDominantBaseline(SvgTextBase styleSource)
            {
                for (SvgElement? current = styleSource.Parent; current is not null; current = current.Parent)
                {
                    if (current is not SvgTextBase parentText)
                    {
                        continue;
                    }

                    return ResolveDominantBaseline(parentText, string.Empty);
                }

                return GetDefaultDominantBaseline(styleSource);
            }

            private static SvgDominantBaseline GetDefaultDominantBaseline(SvgTextBase styleSource)
                => IsVerticalWritingMode(styleSource) ? SvgDominantBaseline.Central : SvgDominantBaseline.Alphabetic;

            private static bool IsVerticalWritingMode(SvgTextBase styleSource)
            {
                if ((!styleSource.ComputedStyle.TryGetPropertyValue("writing-mode", out var value) ||
                     string.IsNullOrWhiteSpace(value)) &&
                    (!styleSource.TryGetAttribute("writing-mode", out value) ||
                     string.IsNullOrWhiteSpace(value)))
                {
                    return false;
                }

                return value.IndexOf("vertical", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       value.IndexOf("tb", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static SvgDominantBaseline ResolveScriptBaseline(string text)
            {
                var charIndex = 0;
                while (TryReadNextCodepoint(text, ref charIndex, out var scalar))
                {
                    if (IsCjkBaselineScript(scalar))
                    {
                        return SvgDominantBaseline.Ideographic;
                    }

                    if (IsMathematicalBaselineScript(scalar))
                    {
                        return SvgDominantBaseline.Mathematical;
                    }

                    if (IsHangingBaselineScript(scalar))
                    {
                        return SvgDominantBaseline.Hanging;
                    }
                }

                return SvgDominantBaseline.Alphabetic;
            }

            private static bool TryReadNextCodepoint(string text, ref int charIndex, out int scalar)
            {
                while (charIndex < text.Length)
                {
                    var current = text[charIndex++];
                    if (char.IsWhiteSpace(current))
                    {
                        continue;
                    }

                    if (char.IsHighSurrogate(current) &&
                        charIndex < text.Length &&
                        char.IsLowSurrogate(text[charIndex]))
                    {
                        scalar = char.ConvertToUtf32(current, text[charIndex]);
                        charIndex++;
                        return true;
                    }

                    scalar = current;
                    return true;
                }

                scalar = 0;
                return false;
            }

            private static bool IsCjkBaselineScript(int scalar)
            {
                return scalar is >= 0x2E80 and <= 0xA4CF or
                       >= 0xAC00 and <= 0xD7AF or
                       >= 0xF900 and <= 0xFAFF or
                       >= 0xFE30 and <= 0xFE4F or
                       >= 0x20000 and <= 0x2FA1F;
            }

            private static bool IsMathematicalBaselineScript(int scalar)
            {
                return scalar is >= 0x2200 and <= 0x22FF or
                       >= 0x27C0 and <= 0x27EF or
                       >= 0x2980 and <= 0x2AFF or
                       >= 0x1D400 and <= 0x1D7FF;
            }

            private static bool IsHangingBaselineScript(int scalar)
            {
                return scalar is >= 0x0900 and <= 0x0D7F or
                       >= 0x0F00 and <= 0x0FFF or
                       >= 0x1000 and <= 0x109F;
            }

            private static bool HasBaselineCoordinate(float value)
            {
                return value != float.MinValue &&
                       !float.IsNaN(value) &&
                       !float.IsInfinity(value);
            }

            private static float ResolveBaselineCoordinate(float value, float fallback)
            {
                return HasBaselineCoordinate(value) ? value : fallback;
            }

            private static float GetBaselineCoordinate(SvgFontFace fontFace, SvgDominantBaseline baseline)
            {
                var ascent = ResolveBaselineCoordinate(fontFace.Ascent, 0f);
                var descent = ResolveBaselineCoordinate(fontFace.Descent, 0f);
                var alphabetic = ResolveBaselineCoordinate(fontFace.Alphabetic, 0f);
                var central = (ascent - descent) * 0.5f;

                return baseline switch
                {
                    SvgDominantBaseline.Ideographic => ResolveBaselineCoordinate(fontFace.Ideographic, -Math.Abs(descent)),
                    SvgDominantBaseline.Hanging => ResolveBaselineCoordinate(fontFace.Hanging, ascent * 0.8f),
                    SvgDominantBaseline.Mathematical => ResolveBaselineCoordinate(fontFace.Mathematical, central),
                    SvgDominantBaseline.Middle when HasBaselineCoordinate(fontFace.XHeight) => fontFace.XHeight * 0.5f,
                    SvgDominantBaseline.Middle when HasBaselineCoordinate(fontFace.CapHeight) => fontFace.CapHeight * 0.5f,
                    SvgDominantBaseline.Middle or SvgDominantBaseline.Central => central,
                    SvgDominantBaseline.TextBeforeEdge or SvgDominantBaseline.TextTop => ascent,
                    SvgDominantBaseline.TextAfterEdge or SvgDominantBaseline.TextBottom => -Math.Abs(descent),
                    _ => alphabetic
                };
            }

            private bool TryResolveGlyph(
                string text,
                IReadOnlyList<CodepointInfo> codepoints,
                int currentCodepointIndex,
                string? language,
                out SvgGlyphDefinition glyph,
                out int consumedCodepoints,
                out bool requiresFontFallback)
            {
                var current = codepoints[currentCodepointIndex];
                if (!_glyphsByFirstChar.TryGetValue(text[current.CharIndex], out var firstCharCandidates))
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = false;
                    return false;
                }

                var requiredForm = GetArabicForm(codepoints, currentCodepointIndex);
                var matchingLanguageCandidates = new SvgGlyphCandidateBucket();
                var genericLanguageCandidates = new SvgGlyphCandidateBucket();
                var hasCandidate = false;
                for (var i = 0; i < firstCharCandidates.Count; i++)
                {
                    var candidate = firstCharCandidates[i];
                    if (current.CharIndex + candidate.Unicode.Length > text.Length ||
                        string.CompareOrdinal(text, current.CharIndex, candidate.Unicode, 0, candidate.Unicode.Length) != 0 ||
                        !SupportsText(candidate.Unicode))
                    {
                        continue;
                    }

                    hasCandidate = true;
                    if (string.IsNullOrWhiteSpace(candidate.Language))
                    {
                        genericLanguageCandidates.Add(candidate, requiredForm);
                    }
                    else if (!string.IsNullOrWhiteSpace(language) && LanguageMatches(language!, candidate.Language!))
                    {
                        matchingLanguageCandidates.Add(candidate, requiredForm);
                    }
                }

                if (!hasCandidate)
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = false;
                    return false;
                }

                var selectedCandidates = matchingLanguageCandidates.HasCandidates
                    ? matchingLanguageCandidates
                    : genericLanguageCandidates;
                if (!selectedCandidates.TryGetBest(out glyph))
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = true;
                    return false;
                }

                consumedCodepoints = glyph.CodepointCount;
                requiresFontFallback = false;
                return true;
            }

            private static SvgArabicForm GetArabicForm(IReadOnlyList<CodepointInfo> codepoints, int currentIndex)
            {
                var currentType = codepoints[currentIndex].JoiningType;
                var joinsPrevious =
                    TryGetJoiningNeighbor(codepoints, currentIndex, -1, out var previousType) &&
                    CanJoinToNext(previousType) &&
                    CanJoinToPrevious(currentType);
                var joinsNext =
                    TryGetJoiningNeighbor(codepoints, currentIndex, 1, out var nextType) &&
                    CanJoinToNext(currentType) &&
                    CanJoinToPrevious(nextType);

                if (joinsPrevious && joinsNext)
                {
                    return SvgArabicForm.Medial;
                }
                if (joinsPrevious)
                {
                    return SvgArabicForm.Terminal;
                }
                if (joinsNext)
                {
                    return SvgArabicForm.Initial;
                }

                return SvgArabicForm.Isolated;
            }

            private static bool TryGetJoiningNeighbor(IReadOnlyList<CodepointInfo> codepoints, int currentIndex, int step, out ArabicJoiningType joiningType)
            {
                for (var i = currentIndex + step; i >= 0 && i < codepoints.Count; i += step)
                {
                    joiningType = codepoints[i].JoiningType;
                    if (joiningType != ArabicJoiningType.Transparent)
                    {
                        return true;
                    }
                }

                joiningType = ArabicJoiningType.NonJoining;
                return false;
            }

            private static bool CanJoinToPrevious(ArabicJoiningType joiningType)
                => joiningType is ArabicJoiningType.RightJoining or ArabicJoiningType.DualJoining or ArabicJoiningType.JoinCausing;

            private static bool CanJoinToNext(ArabicJoiningType joiningType)
                => joiningType is ArabicJoiningType.DualJoining or ArabicJoiningType.JoinCausing;

            private static bool LanguageMatches(string requestedLanguage, string glyphLanguage)
            {
                requestedLanguage = requestedLanguage.Replace('_', '-');
                glyphLanguage = glyphLanguage.Replace('_', '-');
                return requestedLanguage.Equals(glyphLanguage, StringComparison.OrdinalIgnoreCase) ||
                       (requestedLanguage.Length > glyphLanguage.Length &&
                        requestedLanguage.StartsWith(glyphLanguage, StringComparison.OrdinalIgnoreCase) &&
                        requestedLanguage[glyphLanguage.Length] == '-');
            }

            private static int CountCodepoints(string text)
            {
                var count = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    count++;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }
                }

                return count;
            }

            private static IReadOnlyList<SvgGlyphDefinition> CreateGlyphs(SvgFont font)
            {
                var glyphs = new List<SvgGlyphDefinition>();
                foreach (var child in font.Children)
                {
                    if (child is SvgGlyph glyph && child is not SvgMissingGlyph)
                    {
                        glyphs.Add(SvgGlyphDefinition.Create(glyph, font.HorizOriginX));
                    }
                }

                return glyphs;
            }

            private static Dictionary<char, List<SvgGlyphDefinition>> CreateGlyphLookup(IReadOnlyList<SvgGlyphDefinition> glyphs)
            {
                var lookup = new Dictionary<char, List<SvgGlyphDefinition>>();
                for (var i = 0; i < glyphs.Count; i++)
                {
                    var glyph = glyphs[i];
                    if (string.IsNullOrEmpty(glyph.Unicode))
                    {
                        continue;
                    }

                    var firstChar = glyph.Unicode[0];
                    if (!lookup.TryGetValue(firstChar, out var matches))
                    {
                        matches = new List<SvgGlyphDefinition>();
                        lookup[firstChar] = matches;
                    }

                    matches.Add(glyph);
                }

                return lookup;
            }

            private static string CreateResolvedGlyphText(string text, int startCharIndex, int endCharIndex, SvgGlyphDefinition glyph)
            {
                var length = endCharIndex - startCharIndex;
                return glyph.Unicode.Length == length &&
                       string.CompareOrdinal(text, startCharIndex, glyph.Unicode, 0, length) == 0
                    ? glyph.Unicode
                    : text.Substring(startCharIndex, length);
            }

            private struct SvgGlyphCandidateBucket
            {
                private SvgGlyphDefinition? _any;
                private SvgGlyphDefinition? _matchingForm;
                private SvgGlyphDefinition? _genericForm;
                private bool _hasSpecificForms;

                public bool HasCandidates { get; private set; }

                public void Add(SvgGlyphDefinition candidate, SvgArabicForm requiredForm)
                {
                    HasCandidates = true;
                    AddBetter(ref _any, candidate);
                    if (candidate.ArabicForm == SvgArabicForm.None)
                    {
                        AddBetter(ref _genericForm, candidate);
                        return;
                    }

                    _hasSpecificForms = true;
                    if (candidate.ArabicForm == requiredForm)
                    {
                        AddBetter(ref _matchingForm, candidate);
                    }
                }

                public bool TryGetBest(out SvgGlyphDefinition glyph)
                {
                    glyph = _hasSpecificForms
                        ? _matchingForm ?? _genericForm!
                        : _any!;
                    return glyph is not null;
                }

                private static void AddBetter(ref SvgGlyphDefinition? current, SvgGlyphDefinition candidate)
                {
                    if (current is null || candidate.CodepointCount > current.CodepointCount)
                    {
                        current = candidate;
                    }
                }
            }
        }

        private abstract record SvgResolvedItem(string Text);
        private sealed record SvgResolvedGlyphItem(SvgGlyphDefinition Definition, string Text, string BaselineText) : SvgResolvedItem(Text);
        private sealed record SvgFallbackTextItem(string Text) : SvgResolvedItem(Text);
        private sealed record SvgResolvedGlyph(SvgGlyphDefinition Definition, string Text);
        private readonly record struct SvgGlyphEntry(SvgFontEntry Entry, SvgGlyphDefinition Glyph);

        private static void AppendPathCommands(SKPath targetPath, SKPath? sourcePath)
        {
            if (sourcePath is null || sourcePath.IsEmpty)
            {
                return;
            }

            if (sourcePath.Commands is not { Count: > 0 } commands)
            {
                return;
            }

            if (targetPath.Commands is not { } targetCommands)
            {
                return;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                targetCommands.Add(commands[i].DeepClone());
            }
        }

        internal sealed class SvgGlyphDefinition
        {
            public SvgGlyphDefinition(
                string? unicode,
                string glyphName,
                float horizAdvX,
                float horizOriginX,
                string? language,
                SvgArabicForm arabicForm,
                SKPath? pathTemplate,
                SvgGlyph? sourceGlyph)
            {
                Unicode = unicode ?? string.Empty;
                GlyphName = glyphName;
                HorizAdvX = horizAdvX;
                HorizOriginX = horizOriginX;
                Language = language;
                ArabicForm = arabicForm;
                PathTemplate = pathTemplate;
                SourceGlyph = sourceGlyph;
                CodepointCount = CountCodepoints(Unicode);
            }

            public string Unicode { get; }
            public string GlyphName { get; }
            public float HorizAdvX { get; }
            public float HorizOriginX { get; }
            public string? Language { get; }
            public SvgArabicForm ArabicForm { get; }
            public SKPath? PathTemplate { get; }
            public SvgGlyph? SourceGlyph { get; }
            public int CodepointCount { get; }

            public bool MatchesGlyphName(string glyphName)
            {
                return !string.IsNullOrWhiteSpace(glyphName) &&
                       (string.Equals(GlyphName, glyphName, StringComparison.Ordinal) ||
                        string.Equals(SourceGlyph?.ID, glyphName, StringComparison.Ordinal));
            }

            public static SvgGlyphDefinition Create(SvgGlyph glyph, float defaultHorizOriginX)
            {
                TryReadAttribute(glyph, "lang", out var language);
                TryReadAttribute(glyph, "horiz-origin-x", out var glyphOriginXRaw);
                var glyphOriginX = TryParseFloat(glyphOriginXRaw, out var parsedOriginX) ? parsedOriginX : defaultHorizOriginX;
                var arabicForm = TryReadAttribute(glyph, "arabic-form", out var arabicFormValue)
                    ? ParseArabicForm(arabicFormValue)
                    : SvgArabicForm.None;
                return new SvgGlyphDefinition(
                    glyph.Unicode,
                    string.IsNullOrWhiteSpace(glyph.GlyphName) ? glyph.ID ?? glyph.Unicode ?? string.Empty : glyph.GlyphName,
                    glyph.HorizAdvX,
                    glyphOriginX,
                    language,
                    arabicForm,
                    glyph.PathData?.ToPath(glyph.FillRule),
                    glyph);
            }

            private static SvgArabicForm ParseArabicForm(string? value)
            {
                return value?.Trim().ToLowerInvariant() switch
                {
                    "isolated" => SvgArabicForm.Isolated,
                    "initial" => SvgArabicForm.Initial,
                    "medial" => SvgArabicForm.Medial,
                    "terminal" => SvgArabicForm.Terminal,
                    _ => SvgArabicForm.None
                };
            }

            private static int CountCodepoints(string text)
            {
                var count = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    count++;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }
                }

                return count;
            }
        }

        private sealed class SvgFontDescriptor
        {
            private SvgFontDescriptor(string family, SvgFontStyle style, SvgFontVariant variant, int weight, SvgUnicodeRangeMatcher? unicodeRange, Uri? sourceUri)
            {
                Family = family;
                Style = style;
                Variant = variant;
                Weight = weight;
                UnicodeRange = unicodeRange;
                SourceUri = sourceUri;
            }

            public string Family { get; }
            public SvgFontStyle Style { get; }
            public SvgFontVariant Variant { get; }
            public int Weight { get; }
            public SvgUnicodeRangeMatcher? UnicodeRange { get; }
            public Uri? SourceUri { get; }

            public static SvgFontDescriptor FromSvgFontFace(SvgFontFace fontFace, string family, Uri? uri)
            {
                TryReadAttribute(fontFace, "font-style", out var styleValue);
                TryReadAttribute(fontFace, "font-variant", out var variantValue);
                TryReadAttribute(fontFace, "font-weight", out var weightValue);
                TryReadAttribute(fontFace, "unicode-range", out var unicodeRangeValue);
                return new SvgFontDescriptor(
                    family.Trim().Trim('"', '\''),
                    ParseFontStyle(styleValue),
                    ParseFontVariant(variantValue),
                    ParseFontWeight(weightValue),
                    SvgUnicodeRangeMatcher.TryCreate(unicodeRangeValue, out var matcher) ? matcher : null,
                    uri);
            }

            public static SvgFontDescriptor FromCssFontFace(string family, Uri sourceUri, string? styleValue, string? variantValue, string? weightValue, string? unicodeRangeValue)
            {
                return new SvgFontDescriptor(
                    family.Trim().Trim('"', '\''),
                    ParseFontStyle(styleValue),
                    ParseFontVariant(variantValue),
                    ParseFontWeight(weightValue),
                    SvgUnicodeRangeMatcher.TryCreate(unicodeRangeValue, out var matcher) ? matcher : null,
                    sourceUri);
            }

            private static SvgFontStyle ParseFontStyle(string? value)
            {
                return value?.Trim().ToLowerInvariant() switch
                {
                    "italic" => SvgFontStyle.Italic,
                    "oblique" => SvgFontStyle.Oblique,
                    _ => SvgFontStyle.Normal
                };
            }

            private static SvgFontVariant ParseFontVariant(string? value)
            {
                return value?.Trim().Equals("small-caps", StringComparison.OrdinalIgnoreCase) == true
                    ? SvgFontVariant.SmallCaps
                    : SvgFontVariant.Normal;
            }

            private static int ParseFontWeight(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return 400;
                }

                var trimmedValue = value!.Trim();
                if (int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericWeight))
                {
                    return numericWeight;
                }

                return trimmedValue.Equals("bold", StringComparison.OrdinalIgnoreCase) ? 700 : 400;
            }
        }

        private sealed class SvgUnicodeRangeMatcher
        {
            private readonly IReadOnlyList<SvgUnicodeRangeToken> _tokens;

            private SvgUnicodeRangeMatcher(IReadOnlyList<SvgUnicodeRangeToken> tokens)
            {
                _tokens = tokens;
            }

            public bool Supports(string text)
            {
                if (_tokens.Count == 0)
                {
                    return true;
                }

                for (var i = 0; i < text.Length; i++)
                {
                    var codepoint = char.ConvertToUtf32(text, i);
                    if (!Supports(codepoint))
                    {
                        return false;
                    }

                    if (char.IsHighSurrogate(text[i]))
                    {
                        i++;
                    }
                }

                return true;
            }

            public bool SupportsAny(string text)
            {
                if (_tokens.Count == 0)
                {
                    return true;
                }

                for (var i = 0; i < text.Length; i++)
                {
                    if (Supports(char.ConvertToUtf32(text, i)))
                    {
                        return true;
                    }

                    if (char.IsHighSurrogate(text[i]))
                    {
                        i++;
                    }
                }

                return false;
            }

            public bool SupportsCodepoint(int codepoint)
            {
                return _tokens.Count == 0 || Supports(codepoint);
            }

            private bool Supports(int codepoint)
            {
                for (var tokenIndex = 0; tokenIndex < _tokens.Count; tokenIndex++)
                {
                    if (_tokens[tokenIndex].Matches(codepoint))
                    {
                        return true;
                    }
                }

                return false;
            }

            public static bool TryCreate(string? value, out SvgUnicodeRangeMatcher? matcher)
            {
                matcher = null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                var tokens = value!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(token => SvgUnicodeRangeToken.TryCreate(token.Trim(), out var parsed) ? parsed : null)
                    .Where(token => token is not null)
                    .Cast<SvgUnicodeRangeToken>()
                    .ToList();
                if (tokens.Count == 0)
                {
                    return false;
                }

                matcher = new SvgUnicodeRangeMatcher(tokens);
                return true;
            }
        }

        private sealed class SvgUnicodeRangeToken
        {
            private SvgUnicodeRangeToken(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start { get; }
            public int End { get; }

            public bool Matches(int codepoint)
            {
                return codepoint >= Start && codepoint <= End;
            }

            public static bool TryCreate(string token, out SvgUnicodeRangeToken? rangeToken)
            {
                rangeToken = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                if (token.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = token.Substring(2);
                    if (raw.Contains('?'))
                    {
                        var start = raw.Replace('?', '0');
                        var end = raw.Replace('?', 'F');
                        if (int.TryParse(start, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startValue) &&
                            int.TryParse(end, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var endValue))
                        {
                            rangeToken = new SvgUnicodeRangeToken(startValue, endValue);
                            return true;
                        }
                    }

                    var parts = raw.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 1 && int.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var singleValue))
                    {
                        rangeToken = new SvgUnicodeRangeToken(singleValue, singleValue);
                        return true;
                    }

                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startValue2) &&
                        int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var endValue2))
                    {
                        rangeToken = new SvgUnicodeRangeToken(startValue2, endValue2);
                        return true;
                    }
                }
                else if (token.Length > 0)
                {
                    var codepoint = char.ConvertToUtf32(token, 0);
                    rangeToken = new SvgUnicodeRangeToken(codepoint, codepoint);
                    return true;
                }

                return false;
            }
        }

        private sealed class SvgKernRule
        {
            private SvgKernRule(SvgGlyphTokenMatcher? leftGlyphMatcher, SvgGlyphTokenMatcher? rightGlyphMatcher, SvgUnicodeTokenMatcher? leftUnicodeMatcher, SvgUnicodeTokenMatcher? rightUnicodeMatcher, float kerning)
            {
                LeftGlyphMatcher = leftGlyphMatcher;
                RightGlyphMatcher = rightGlyphMatcher;
                LeftUnicodeMatcher = leftUnicodeMatcher;
                RightUnicodeMatcher = rightUnicodeMatcher;
                Kerning = kerning;
            }

            public SvgGlyphTokenMatcher? LeftGlyphMatcher { get; }
            public SvgGlyphTokenMatcher? RightGlyphMatcher { get; }
            public SvgUnicodeTokenMatcher? LeftUnicodeMatcher { get; }
            public SvgUnicodeTokenMatcher? RightUnicodeMatcher { get; }
            public float Kerning { get; }

            public bool Matches(SvgResolvedGlyph left, SvgResolvedGlyph right)
            {
                return MatchesSide(left, LeftGlyphMatcher, LeftUnicodeMatcher) && MatchesSide(right, RightGlyphMatcher, RightUnicodeMatcher);
            }

            private static bool MatchesSide(SvgResolvedGlyph glyph, SvgGlyphTokenMatcher? glyphMatcher, SvgUnicodeTokenMatcher? unicodeMatcher)
            {
                var matchesGlyph = glyphMatcher?.Matches(glyph.Definition.GlyphName) ?? false;
                var matchesUnicode = unicodeMatcher?.Matches(glyph.Text) ?? false;
                if (glyphMatcher is null && unicodeMatcher is null)
                {
                    return false;
                }

                if (glyphMatcher is not null && unicodeMatcher is not null)
                {
                    return matchesGlyph || matchesUnicode;
                }

                return matchesGlyph || matchesUnicode;
            }

            public static SvgKernRule? Create(SvgKern kern)
            {
                var leftGlyphMatcher = SvgGlyphTokenMatcher.TryCreate(kern.Glyph1, out var g1Matcher) ? g1Matcher : null;
                var rightGlyphMatcher = SvgGlyphTokenMatcher.TryCreate(kern.Glyph2, out var g2Matcher) ? g2Matcher : null;
                var leftUnicodeMatcher = SvgUnicodeTokenMatcher.TryCreate(kern.Unicode1, out var u1Matcher) ? u1Matcher : null;
                var rightUnicodeMatcher = SvgUnicodeTokenMatcher.TryCreate(kern.Unicode2, out var u2Matcher) ? u2Matcher : null;
                if (leftGlyphMatcher is null && rightGlyphMatcher is null && leftUnicodeMatcher is null && rightUnicodeMatcher is null)
                {
                    return null;
                }

                return new SvgKernRule(leftGlyphMatcher, rightGlyphMatcher, leftUnicodeMatcher, rightUnicodeMatcher, kern.Kerning);
            }
        }

        private sealed class SvgGlyphTokenMatcher
        {
            private readonly HashSet<string> _glyphNames;

            private SvgGlyphTokenMatcher(HashSet<string> glyphNames)
            {
                _glyphNames = glyphNames;
            }

            public bool Matches(string glyphName)
            {
                return _glyphNames.Contains(glyphName);
            }

            public static bool TryCreate(string? value, out SvgGlyphTokenMatcher? matcher)
            {
                matcher = null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                var glyphNames = new HashSet<string>(
                    value!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(token => token.Trim())
                        .Where(token => !string.IsNullOrWhiteSpace(token)),
                    StringComparer.Ordinal);
                if (glyphNames.Count == 0)
                {
                    return false;
                }

                matcher = new SvgGlyphTokenMatcher(glyphNames);
                return true;
            }
        }

        private sealed class SvgUnicodeTokenMatcher
        {
            private readonly IReadOnlyList<SvgUnicodeRangeToken> _ranges;
            private readonly HashSet<string> _literals;

            private SvgUnicodeTokenMatcher(IReadOnlyList<SvgUnicodeRangeToken> ranges, HashSet<string> literals)
            {
                _ranges = ranges;
                _literals = literals;
            }

            public bool Matches(string text)
            {
                if (_literals.Contains(text))
                {
                    return true;
                }

                if (CountCodepoints(text) != 1)
                {
                    return false;
                }

                var codepoint = char.ConvertToUtf32(text, 0);
                for (var i = 0; i < _ranges.Count; i++)
                {
                    if (_ranges[i].Matches(codepoint))
                    {
                        return true;
                    }
                }

                return false;
            }

            public static bool TryCreate(string? value, out SvgUnicodeTokenMatcher? matcher)
            {
                matcher = null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                var ranges = new List<SvgUnicodeRangeToken>();
                var literals = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in value!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim();
                    if (trimmed.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                    {
                        if (SvgUnicodeRangeToken.TryCreate(trimmed, out var range) && range is not null)
                        {
                            ranges.Add(range);
                        }
                    }
                    else if (!string.IsNullOrEmpty(trimmed))
                    {
                        literals.Add(trimmed);
                    }
                }

                if (ranges.Count == 0 && literals.Count == 0)
                {
                    return false;
                }

                matcher = new SvgUnicodeTokenMatcher(ranges, literals);
                return true;
            }

            private static int CountCodepoints(string text)
            {
                var count = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    count++;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }
                }

                return count;
            }
        }

        private readonly record struct CodepointInfo(int Scalar, int CharIndex, int CharLength, ArabicJoiningType JoiningType)
        {
            public static IReadOnlyList<CodepointInfo> Parse(string text)
            {
                var codepoints = new List<CodepointInfo>(text.Length);
                for (var i = 0; i < text.Length; i++)
                {
                    var start = i;
                    var scalar = char.ConvertToUtf32(text, i);
                    var charLength = 1;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        charLength = 2;
                        i++;
                    }

                    codepoints.Add(new CodepointInfo(scalar, start, charLength, GetJoiningType(text, start, scalar)));
                }

                return codepoints;
            }

            public string GetText(string source)
            {
                return source.Substring(CharIndex, CharLength);
            }

            private static ArabicJoiningType GetJoiningType(string text, int charIndex, int scalar)
            {
                if (scalar == 0x200C)
                {
                    return ArabicJoiningType.NonJoining;
                }

                if (scalar == 0x0640 || scalar == 0x0883 || scalar == 0x0884 || scalar == 0x0885 || scalar == 0x200D)
                {
                    return ArabicJoiningType.JoinCausing;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(text, charIndex);
                if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark ||
                    category == UnicodeCategory.Format)
                {
                    return ArabicJoiningType.Transparent;
                }

                if (IsDualJoiningCodepoint(scalar))
                {
                    return ArabicJoiningType.DualJoining;
                }

                if (IsRightJoiningCodepoint(scalar))
                {
                    return ArabicJoiningType.RightJoining;
                }

                return ArabicJoiningType.NonJoining;
            }

            private static bool IsRightJoiningCodepoint(int scalar)
                => scalar switch
                {
                    >= 0x0622 and <= 0x0625 => true,
                    0x0627 => true,
                    0x0629 => true,
                    >= 0x062F and <= 0x0632 => true,
                    0x0648 => true,
                    >= 0x0671 and <= 0x0673 => true,
                    >= 0x0675 and <= 0x0677 => true,
                    >= 0x0688 and <= 0x0699 => true,
                    0x06C0 => true,
                    >= 0x06C3 and <= 0x06CB => true,
                    0x06CD => true,
                    0x06CF => true,
                    >= 0x06D2 and <= 0x06D3 => true,
                    0x06D5 => true,
                    >= 0x06EE and <= 0x06EF => true,
                    >= 0x0759 and <= 0x075B => true,
                    >= 0x076B and <= 0x076C => true,
                    0x0771 => true,
                    >= 0x0773 and <= 0x0774 => true,
                    >= 0x0778 and <= 0x0779 => true,
                    >= 0x0870 and <= 0x0882 => true,
                    0x088E => true,
                    >= 0x08AA and <= 0x08AC => true,
                    0x08AE => true,
                    >= 0x08B1 and <= 0x08B2 => true,
                    0x08B9 => true,
                    _ => false
                };

            private static bool IsDualJoiningCodepoint(int scalar)
                => scalar switch
                {
                    0x0620 => true,
                    0x0626 => true,
                    0x0628 => true,
                    >= 0x062A and <= 0x062E => true,
                    >= 0x0633 and <= 0x063F => true,
                    >= 0x0641 and <= 0x0647 => true,
                    >= 0x0649 and <= 0x064A => true,
                    >= 0x066E and <= 0x066F => true,
                    >= 0x0678 and <= 0x0687 => true,
                    >= 0x069A and <= 0x06BF => true,
                    >= 0x06C1 and <= 0x06C2 => true,
                    0x06CC => true,
                    0x06CE => true,
                    >= 0x06D0 and <= 0x06D1 => true,
                    >= 0x06FA and <= 0x06FC => true,
                    0x06FF => true,
                    >= 0x074E and <= 0x0758 => true,
                    >= 0x075C and <= 0x076A => true,
                    >= 0x076D and <= 0x0770 => true,
                    0x0772 => true,
                    >= 0x0775 and <= 0x0777 => true,
                    >= 0x077A and <= 0x077F => true,
                    0x0860 => true,
                    >= 0x0862 and <= 0x0865 => true,
                    0x0868 => true,
                    0x0886 => true,
                    >= 0x0889 and <= 0x088D => true,
                    0x088F => true,
                    >= 0x08A0 and <= 0x08A9 => true,
                    >= 0x08AF and <= 0x08B0 => true,
                    >= 0x08B3 and <= 0x08B8 => true,
                    >= 0x08BA and <= 0x08C8 => true,
                    _ => false
                };
        }

        private static bool TryReadAttribute(SvgElement element, string name, out string value)
        {
            return element.TryGetAttribute(name, out value);
        }

        private static bool TryParseFloat(string? value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
