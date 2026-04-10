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
        {
            layout = null;

            if (assetLoader is { EnableSvgFonts: false })
            {
                return false;
            }

            if (string.IsNullOrEmpty(text) || paint.TextSize <= 0f)
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

            var request = SvgFontRequest.Create(svgTextBase, text, paint.TextSize);
            foreach (var family in request.Families)
            {
                if (!registry.TryGetEntries(family, out var familyEntries))
                {
                    continue;
                }

                var compatibleEntries = familyEntries
                    .Where(entry => entry.IsVariantCompatible(request) && entry.IsStyleCompatible(request))
                    .OrderBy(entry => entry.GetStyleDistance(request))
                    .ThenBy(entry => entry.GetWeightDistance(request))
                    .ThenBy(entry => entry.Order)
                    .ToList();

                for (var i = 0; i < compatibleEntries.Count; i++)
                {
                    var entry = compatibleEntries[i];
                    if (!entry.SupportsAnyText(text))
                    {
                        continue;
                    }

                    if (entry.TryCreateLayout(request, paint, assetLoader, out layout) && layout is not null)
                    {
                        return true;
                    }
                }
            }

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
                    text,
                    textSize,
                    SplitFamilies(svgTextBase.FontFamily),
                    GetLanguage(svgTextBase),
                    NormalizeFontStyle(svgTextBase.FontStyle),
                    NormalizeFontVariant(svgTextBase.FontVariant),
                    NormalizeFontWeight(svgTextBase.FontWeight),
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

            private static int NormalizeFontWeight(SvgFontWeight weight)
            {
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
            private readonly SKRect _relativeBounds;

            internal SvgFontLayout(IReadOnlyList<SvgGlyphPlacementResult> glyphs, float advance, SKRect relativeBounds)
            {
                _glyphs = glyphs;
                Advance = advance;
                _relativeBounds = relativeBounds;
            }

            public float Advance { get; }

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
            private readonly IReadOnlyList<SvgKernRule> _kernRules;

            public SvgFontEntry(int order, SvgFont font, SvgFontFace metricsFace, SvgFontDescriptor descriptor)
            {
                Order = order;
                Font = font;
                MetricsFace = metricsFace;
                Descriptor = descriptor;
                _glyphs = CreateGlyphs(font);
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

            public bool TryCreateLayout(SvgFontRequest request, SKPaint paint, ISvgAssetLoader? assetLoader, out SvgFontLayout? layout)
            {
                layout = null;

                var codepoints = CodepointInfo.Parse(request.Text);
                if (codepoints.Count == 0)
                {
                    return false;
                }

                var logicalItems = new List<SvgResolvedItem>();
                var hasSvgGlyph = false;
                for (var codepointIndex = 0; codepointIndex < codepoints.Count;)
                {
                    var start = codepoints[codepointIndex];
                    if (!SupportsText(start.Value))
                    {
                        logicalItems.Add(new SvgFallbackTextItem(start.Value));
                        codepointIndex++;
                        continue;
                    }

                    var remaining = request.Text.Substring(start.CharIndex);
                    if (!TryResolveGlyph(remaining, codepoints, codepointIndex, request.Language, out var glyph, out var consumedCodepoints, out var requiresFontFallback))
                    {
                        if (requiresFontFallback || MissingGlyph is null)
                        {
                            logicalItems.Add(new SvgFallbackTextItem(start.Value));
                            codepointIndex++;
                            continue;
                        }

                        glyph = MissingGlyph;
                        consumedCodepoints = 1;
                    }

                    var endCharIndex = codepointIndex + consumedCodepoints < codepoints.Count
                        ? codepoints[codepointIndex + consumedCodepoints].CharIndex
                        : request.Text.Length;
                    var consumedText = request.Text.Substring(start.CharIndex, endCharIndex - start.CharIndex);
                    logicalItems.Add(new SvgResolvedGlyphItem(glyph, consumedText));
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
                layout = CreateLayout(visualItems, request.TextSize, paint, assetLoader);
                return true;
            }

            private SvgFontLayout CreateLayout(IReadOnlyList<SvgResolvedItem> resolvedItems, float textSize, SKPaint paint, ISvgAssetLoader? assetLoader)
            {
                var scale = textSize / UnitsPerEm;
                var ascent = Ascent * scale;
                var descent = Descent * scale;
                var alphabetic = Alphabetic * scale;
                var placements = new List<SvgGlyphPlacementResult>(resolvedItems.Count);
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
                                TransY = alphabetic,
                                Persp0 = 0f,
                                Persp1 = 0f,
                                Persp2 = 1f
                            };
                            relativePath.Transform(transform);
                        }

                        glyphBounds = relativePath is { IsEmpty: false }
                            ? relativePath.Bounds
                            : new SKRect(glyphOriginX, alphabetic - ascent, glyphOriginX + advance, alphabetic + descent);
                        previousSvgGlyph = svgGlyphItem;
                    }
                    else
                    {
                        if (assetLoader is null)
                        {
                            if (placements.Count > 0)
                            {
                                var lastPlacement = placements[placements.Count - 1];
                                return new SvgFontLayout(placements, lastPlacement.RelativeX + lastPlacement.Advance, bounds);
                            }

                            return new SvgFontLayout(placements, 0f, bounds);
                        }

                        var fallbackPlacement = CreateFallbackPlacement(resolvedItem.Text, currentX, paint, assetLoader);
                        relativePath = fallbackPlacement.RelativePath;
                        glyphBounds = fallbackPlacement.RelativeBounds;
                        advance = fallbackPlacement.Advance;
                        previousSvgGlyph = null;
                    }

                    bounds = bounds.IsEmpty ? glyphBounds : SKRect.Union(bounds, glyphBounds);
                    placements.Add(new SvgGlyphPlacementResult(glyphDefinition, currentX, advance, relativePath, glyphBounds));
                    previousAdvance = advance;
                    hasPrevious = true;
                }

                var totalAdvance = 0f;
                if (placements.Count > 0)
                {
                    var lastPlacement = placements[placements.Count - 1];
                    totalAdvance = lastPlacement.RelativeX + lastPlacement.Advance;
                }

                return new SvgFontLayout(placements, totalAdvance, bounds);
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

            private bool TryResolveGlyph(
                string remainingText,
                IReadOnlyList<CodepointInfo> codepoints,
                int currentCodepointIndex,
                string? language,
                out SvgGlyphDefinition glyph,
                out int consumedCodepoints,
                out bool requiresFontFallback)
            {
                var candidates = new List<SvgGlyphDefinition>();
                for (var i = 0; i < _glyphs.Count; i++)
                {
                    var candidate = _glyphs[i];
                    if (!string.IsNullOrEmpty(candidate.Unicode) &&
                        remainingText.StartsWith(candidate.Unicode, StringComparison.Ordinal) &&
                        SupportsText(candidate.Unicode))
                    {
                        candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = false;
                    return false;
                }

                var languageCandidates = FilterByLanguage(candidates, language);
                if (languageCandidates.Count == 0)
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = true;
                    return false;
                }

                var formCandidates = FilterByArabicForm(languageCandidates, codepoints, currentCodepointIndex);
                if (formCandidates.Count == 0)
                {
                    glyph = null!;
                    consumedCodepoints = 1;
                    requiresFontFallback = true;
                    return false;
                }

                glyph = formCandidates[0];
                consumedCodepoints = CountCodepoints(glyph.Unicode);
                for (var i = 1; i < formCandidates.Count; i++)
                {
                    var candidate = formCandidates[i];
                    var candidateCodepoints = CountCodepoints(candidate.Unicode);
                    if (candidateCodepoints > consumedCodepoints)
                    {
                        glyph = candidate;
                        consumedCodepoints = candidateCodepoints;
                    }
                }

                requiresFontFallback = false;
                return true;
            }

            private static List<SvgGlyphDefinition> FilterByLanguage(IReadOnlyList<SvgGlyphDefinition> candidates, string? language)
            {
                var matchingSpecific = new List<SvgGlyphDefinition>();
                var generic = new List<SvgGlyphDefinition>();
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (string.IsNullOrWhiteSpace(candidate.Language))
                    {
                        generic.Add(candidate);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(language) && LanguageMatches(language!, candidate.Language!))
                    {
                        matchingSpecific.Add(candidate);
                    }
                }

                if (matchingSpecific.Count > 0)
                {
                    return matchingSpecific;
                }

                return generic;
            }

            private static List<SvgGlyphDefinition> FilterByArabicForm(IReadOnlyList<SvgGlyphDefinition> candidates, IReadOnlyList<CodepointInfo> codepoints, int currentCodepointIndex)
            {
                var hasSpecificForms = false;
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].ArabicForm != SvgArabicForm.None)
                    {
                        hasSpecificForms = true;
                        break;
                    }
                }

                if (!hasSpecificForms)
                {
                    return candidates.ToList();
                }

                var requiredForm = GetArabicForm(codepoints, currentCodepointIndex);
                var matching = new List<SvgGlyphDefinition>();
                var generic = new List<SvgGlyphDefinition>();
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate.ArabicForm == SvgArabicForm.None)
                    {
                        generic.Add(candidate);
                    }
                    else if (candidate.ArabicForm == requiredForm)
                    {
                        matching.Add(candidate);
                    }
                }

                if (matching.Count > 0)
                {
                    return matching;
                }

                return generic;
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
        }

        private abstract record SvgResolvedItem(string Text);
        private sealed record SvgResolvedGlyphItem(SvgGlyphDefinition Definition, string Text) : SvgResolvedItem(Text);
        private sealed record SvgFallbackTextItem(string Text) : SvgResolvedItem(Text);
        private sealed record SvgResolvedGlyph(SvgGlyphDefinition Definition, string Text);

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
                SKPath? pathTemplate)
            {
                Unicode = unicode ?? string.Empty;
                GlyphName = glyphName;
                HorizAdvX = horizAdvX;
                HorizOriginX = horizOriginX;
                Language = language;
                ArabicForm = arabicForm;
                PathTemplate = pathTemplate;
            }

            public string Unicode { get; }
            public string GlyphName { get; }
            public float HorizAdvX { get; }
            public float HorizOriginX { get; }
            public string? Language { get; }
            public SvgArabicForm ArabicForm { get; }
            public SKPath? PathTemplate { get; }

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
                    glyph.PathData?.ToPath(glyph.FillRule));
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

                value = value.Trim();
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericWeight))
                {
                    return numericWeight;
                }

                return value.Equals("bold", StringComparison.OrdinalIgnoreCase) ? 700 : 400;
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

                var tokens = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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
                    value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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
                foreach (var token in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
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

        private readonly record struct CodepointInfo(string Value, int CharIndex, ArabicJoiningType JoiningType)
        {
            public static IReadOnlyList<CodepointInfo> Parse(string text)
            {
                var codepoints = new List<CodepointInfo>();
                for (var i = 0; i < text.Length; i++)
                {
                    var start = i;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }

                    var value = text.Substring(start, i - start + 1);
                    codepoints.Add(new CodepointInfo(value, start, GetJoiningType(value)));
                }

                return codepoints;
            }

            private static ArabicJoiningType GetJoiningType(string value)
            {
                var scalar = char.ConvertToUtf32(value, 0);
                if (scalar == 0x200C)
                {
                    return ArabicJoiningType.NonJoining;
                }

                if (scalar == 0x0640 || scalar == 0x0883 || scalar == 0x0884 || scalar == 0x0885 || scalar == 0x200D)
                {
                    return ArabicJoiningType.JoinCausing;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(value, 0);
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
