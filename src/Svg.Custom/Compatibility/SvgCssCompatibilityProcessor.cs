#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ExCSS;
using Svg.Css;

namespace Svg;

/// <summary>
/// Chrome-compatibility CSS pipeline for Svg.Custom.
///
/// The loader's job is to build the SVG DOM and collect raw stylesheet sources. Everything below
/// is CSS policy layered on top of that DOM: selector matching, <c>:link</c> projection for text
/// containers, filtering of non-CSS <c>&lt;style&gt;</c> blocks, and the stricter import/media
/// handling needed to stay aligned with the checked Chrome W3C baselines.
/// </summary>
internal static class SvgCssCompatibilityProcessor
{
    // When the SVG does not declare a usable viewport of its own, fall back to the historical
    // standalone capture size used by the checked W3C Chrome overrides.
    private const double StaticScreenWidthPixels = 480d;
    private const double StaticScreenHeightPixels = 360d;
    private const double CentimetersPerInch = 2.54d;
    private const double MillimetersPerInch = 25.4d;
    private const double PointsPerInch = 72d;
    private const double PicasPerInch = 6d;
    private const string CssMimeType = "text/css";
    private const string ImportAtRule = "@import";
    private const string CharsetAtRule = "@charset";
    private const string UrlKeyword = "url";
    private const string ScreenMediaType = "screen";
    private const string AllMediaType = "all";
    private const string OnlyKeyword = "only";
    private const string NotKeyword = "not";
    private const string AndKeyword = "and";
    private const string LinkPseudoClass = "link";
    private const string WidthFeature = "width";
    private const string MinWidthFeature = "min-width";
    private const string MaxWidthFeature = "max-width";
    private const string HeightFeature = "height";
    private const string MinHeightFeature = "min-height";
    private const string MaxHeightFeature = "max-height";
    private const string OrientationFeature = "orientation";
    private const string LandscapeOrientation = "landscape";
    private const string PortraitOrientation = "portrait";
    private const int MaxStringBuilderCapacity = int.MaxValue - 1;

    public static bool Apply(SvgDocument svgDocument, IReadOnlyCollection<SvgCssStyleSource> styles, SvgElementFactory elementFactory)
    {
        if (styles.Count == 0)
        {
            return false;
        }

        var mediaContext = ResolveMediaContext(svgDocument);

        // Expand valid imports first so the final stylesheet matches browser evaluation order:
        // imported rules are inlined into the aggregate stylesheet before selector matching.
        var cssTotal = ExpandImportedStyles(styles, mediaContext);
        if (string.IsNullOrWhiteSpace(cssTotal))
        {
            return false;
        }

        var stylesheetParser = new StylesheetParser(true, true, tolerateInvalidValues: true);
        var stylesheet = stylesheetParser.Parse(cssTotal);
        var rootNode = new NonSvgElement();
        rootNode.Children.Add(svgDocument);
        var appliedAnyStyles = false;
        try
        {
            foreach (var rule in stylesheet.StyleRules)
            {
                try
                {
                    var projectsLinkStylesToText = ContainsLinkPseudoClass(rule.Selector);
                    var specificity = rule.Selector.GetSpecificity();
                    List<AppliedDeclaration>? declarations = null;
                    var elemsToStyle = rootNode.QuerySelectorAll(rule.Selector, elementFactory);

                    foreach (var elem in elemsToStyle)
                    {
                        declarations ??= CreateAppliedDeclarations();

                        SvgTextBase? textContainer = null;
                        var projectsToTextContainer = projectsLinkStylesToText &&
                                                      TryGetLinkTextContainer(elem, out textContainer);

                        foreach (var declaration in declarations)
                        {
                            elem.AddStyleCompatibility(declaration.Name, declaration.Value, specificity);
                            appliedAnyStyles = true;

                            if (projectsToTextContainer)
                            {
                                textContainer!.AddStyleCompatibility(declaration.Name, declaration.Value, specificity);
                                appliedAnyStyles = true;
                            }
                        }
                    }

                    List<AppliedDeclaration> CreateAppliedDeclarations()
                    {
                        var result = new List<AppliedDeclaration>();
                        foreach (var declaration in rule.Style)
                        {
                            result.Add(new AppliedDeclaration(declaration.Name, declaration.Original));
                        }

                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(ex.Message);
                }
            }
        }
        finally
        {
            // QuerySelectorAll needs a synthetic parent so selectors like :root/descendant matching
            // can traverse from a stable container, but that wrapper must not leak into the live
            // document tree. Animation bindings capture child-index addresses later, and leaving
            // svgDocument.Parent pointing at this temporary node shifts every recorded path by one
            // extra level, which makes CreateAnimatedDocument fail to resolve targets on clones.
            _ = rootNode.Children.Remove(svgDocument);
        }

        return appliedAnyStyles;
    }

    public static bool ShouldApplyStyleElement(SvgUnknownElement styleElement)
    {
        if (!styleElement.TryGetAttribute("type", out var styleType))
        {
            return true;
        }

        // Browsers ignore non-CSS <style> payloads for CSS selector matching. Restricting the
        // loader here avoids feeding script/data blocks into ExCSS and accidentally producing
        // selectors or declarations from content Chrome would never treat as CSS.
        var mediaType = styleType.AsSpan();
        var parameterSeparatorIndex = mediaType.IndexOf(';');
        if (parameterSeparatorIndex >= 0)
        {
            mediaType = mediaType.Slice(0, parameterSeparatorIndex);
        }

        return TrimWhitespace(mediaType).Equals(CssMimeType.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetLinkTextContainer(SvgElement element, out SvgTextBase textContainer)
    {
        textContainer = null!;

        if (element is not SvgAnchor anchor ||
            string.IsNullOrWhiteSpace(anchor.Href) ||
            anchor.Parent is not SvgTextBase parentTextBase)
        {
            return false;
        }

        var preservesWhitespace = parentTextBase.SpaceHandling == XmlSpaceHandling.Preserve;

        foreach (var node in parentTextBase.Nodes)
        {
            if (ReferenceEquals(node, anchor))
            {
                continue;
            }

            if (!preservesWhitespace &&
                node is SvgContentNode contentNode &&
                string.IsNullOrWhiteSpace(contentNode.Content))
            {
                continue;
            }

            // If the surrounding text container has any other meaningful content, projecting the
            // link rule onto it would leak styles onto non-link glyphs. In that case the rule must
            // stay anchored to the matched <a> only.
            return false;
        }

        // The renderer draws raw text children through the surrounding text container rather than
        // through the <a> node itself. When the anchor is the only meaningful child of that text
        // container, mirroring the fully matched rule onto the container reproduces Chrome's link
        // styling without widening selector matching.
        textContainer = parentTextBase;
        return true;
    }

    private static bool ContainsLinkPseudoClass(ISelector selector)
    {
        switch (selector)
        {
            case PseudoClassSelector pseudoClassSelector:
                return string.Equals(pseudoClassSelector.Class, LinkPseudoClass, StringComparison.OrdinalIgnoreCase);

            case CompoundSelector compoundSelector:
                foreach (var compoundPart in compoundSelector)
                {
                    if (ContainsLinkPseudoClass(compoundPart))
                    {
                        return true;
                    }
                }

                return false;

            case ComplexSelector complexSelector:
                foreach (var part in complexSelector)
                {
                    if (ContainsLinkPseudoClass(part.Selector))
                    {
                        return true;
                    }
                }

                return false;

            case ListSelector listSelector:
                foreach (var listPart in listSelector)
                {
                    if (ContainsLinkPseudoClass(listPart))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static string ExpandImportedStyles(IReadOnlyCollection<SvgCssStyleSource> sources, CssMediaContext mediaContext)
    {
        var initialCapacity = 0;
        foreach (var source in sources)
        {
            initialCapacity = SaturatingAddCapacity(initialCapacity, source.Content.Length);
            initialCapacity = SaturatingAddCapacity(initialCapacity, Environment.NewLine.Length);
        }

        var builder = initialCapacity > 0
            ? new StringBuilder(initialCapacity)
            : new StringBuilder();

        foreach (var source in sources)
        {
            // Each top-level stylesheet source gets its own active import chain. That still breaks
            // cycles, but it avoids globally deduping imports across sibling <style> blocks, which
            // would erase valid source-order effects from later imports of the same stylesheet.
            AppendExpandedStyles(builder, source.Content, source.BaseUri, mediaContext, CreateImportChain());
            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static HashSet<string> CreateImportChain()
    {
        // Import cycle detection should compare the fully resolved URI text exactly. Folding case
        // here breaks legitimate imports on case-sensitive filesystems where `A.css` and `a.css`
        // are different resources that browsers would load independently.
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static void AppendExpandedStyles(
        StringBuilder builder,
        string cssText,
        Uri? baseUri,
        CssMediaContext mediaContext,
        HashSet<string> importChain)
    {
        var index = 0;
        var isInLeadingImportSection = true;

        while (TryReadNextTopLevelStatement(cssText, ref index, out var statement))
        {
            var atRuleKind = GetAtRuleKind(cssText, statement);

            // CSS only honors @import rules while the stylesheet is still in its leading import
            // section. As soon as a normal rule or another at-rule appears, later imports are
            // invalid and must be ignored even if they are otherwise well-formed.
            if (isInLeadingImportSection && atRuleKind == CssAtRuleKind.Import)
            {
                if (TryParseKnownImportRule(cssText, statement, out var href, out var mediaCondition) &&
                    ShouldApplyImportForCurrentMedia(mediaCondition, mediaContext))
                {
                    var imported = TryLoadImportedStylesheet(href, baseUri, importChain);
                    if (imported is not null)
                    {
                        try
                        {
                            AppendExpandedStyles(builder, imported.Content, imported.BaseUri, mediaContext, importChain);
                            builder.AppendLine();
                        }
                        finally
                        {
                            importChain.Remove(imported.BaseUri!.AbsoluteUri);
                        }
                    }
                }

                continue;
            }

            if (atRuleKind != CssAtRuleKind.Charset)
            {
                isInLeadingImportSection = false;
            }

            if (atRuleKind == CssAtRuleKind.Import)
            {
                continue;
            }

            AppendStatement(builder, cssText, statement);
        }
    }

    private static bool ShouldApplyImportForCurrentMedia(ReadOnlySpan<char> mediaCondition, CssMediaContext mediaContext)
    {
        var mediaList = TrimWhitespace(mediaCondition);
        if (mediaList.IsEmpty)
        {
            return true;
        }

        var segmentStart = 0;
        var index = 0;
        var parenthesisDepth = 0;

        while (index < mediaList.Length)
        {
            if (TrySkipComment(mediaList, ref index))
            {
                continue;
            }

            var current = mediaList[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(mediaList, ref index, current);
                    continue;

                case '(':
                    parenthesisDepth++;
                    index++;
                    continue;

                case ')' when parenthesisDepth > 0:
                    parenthesisDepth--;
                    index++;
                    continue;

                case ',' when parenthesisDepth == 0:
                    if (MatchesCurrentMedia(mediaList.Slice(segmentStart, index - segmentStart), mediaContext))
                    {
                        return true;
                    }

                    index++;
                    segmentStart = index;
                    continue;

                default:
                    index++;
                    break;
            }
        }

        return MatchesCurrentMedia(mediaList.Slice(segmentStart), mediaContext);
    }

    private static bool MatchesCurrentMedia(ReadOnlySpan<char> mediaQuery, CssMediaContext mediaContext)
    {
        var normalized = TrimWhitespace(mediaQuery);
        if (normalized.IsEmpty)
        {
            return false;
        }

        ConsumeLeadingKeyword(ref normalized, OnlyKeyword);

        var isNegated = ConsumeLeadingKeyword(ref normalized, NotKeyword);
        if (!TryParseMediaQuery(normalized, mediaContext, out var mediaType, out var matchesFeatures))
        {
            return false;
        }

        // Treat Svg.Skia's document-loading CSS context as "screen". Imports scoped to other media
        // such as "print" should not leak into the static image output.
        var matchesScreen = mediaType.IsEmpty ||
            mediaType.Equals(AllMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals(ScreenMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase);

        // Once a media query adds feature predicates, matching the type alone is no longer enough.
        // Evaluate those predicates against the SVG document's declared viewport when it exists,
        // and only fall back to the historical W3C default when the document provides no usable
        // size of its own. Unsupported features stay conservative non-matches so imports are not
        // inlined on predicates we cannot validate.
        var matchesMediaQuery = matchesScreen && matchesFeatures;
        return isNegated ? !matchesMediaQuery : matchesMediaQuery;
    }

    private static bool TryParseMediaQuery(
        ReadOnlySpan<char> mediaQuery,
        CssMediaContext mediaContext,
        out ReadOnlySpan<char> mediaType,
        out bool matchesFeatures)
    {
        mediaType = default;
        matchesFeatures = true;

        var index = 0;
        SkipWhitespaceAndComments(mediaQuery, ref index);

        if (index < mediaQuery.Length && mediaQuery[index] != '(')
        {
            var typeStart = index;
            while (index < mediaQuery.Length && !char.IsWhiteSpace(mediaQuery[index]) && mediaQuery[index] != '(')
            {
                index++;
            }

            mediaType = TrimWhitespace(mediaQuery.Slice(typeStart, index - typeStart));
            SkipWhitespaceAndComments(mediaQuery, ref index);
        }

        var expectsFeatureAfterAnd = false;
        while (index < mediaQuery.Length)
        {
            if (mediaQuery[index] == '(')
            {
                if (!mediaType.IsEmpty && !expectsFeatureAfterAnd)
                {
                    return false;
                }

                if (!TryReadMediaFeature(mediaQuery, ref index, out var mediaFeature) || mediaFeature.IsEmpty)
                {
                    return false;
                }

                matchesFeatures &= EvaluateMediaFeature(mediaFeature, mediaContext);
                expectsFeatureAfterAnd = false;
                SkipWhitespaceAndComments(mediaQuery, ref index);
                continue;
            }

            if (!TryConsumeMediaAnd(mediaQuery, ref index))
            {
                return false;
            }

            expectsFeatureAfterAnd = true;
            SkipWhitespaceAndComments(mediaQuery, ref index);
        }

        return !expectsFeatureAfterAnd;
    }

    private static bool TryConsumeMediaAnd(ReadOnlySpan<char> mediaQuery, ref int index)
    {
        if (!mediaQuery.Slice(index).StartsWith(AndKeyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endIndex = index + AndKeyword.Length;
        if (endIndex < mediaQuery.Length && !char.IsWhiteSpace(mediaQuery[endIndex]) && mediaQuery[endIndex] != '(')
        {
            return false;
        }

        index = endIndex;
        return true;
    }

    private static bool TryReadMediaFeature(ReadOnlySpan<char> mediaQuery, ref int index, out ReadOnlySpan<char> mediaFeature)
    {
        mediaFeature = default;

        if (index >= mediaQuery.Length || mediaQuery[index] != '(')
        {
            return false;
        }

        index++;
        var featureStart = index;
        var depth = 1;

        while (index < mediaQuery.Length)
        {
            if (TrySkipComment(mediaQuery, ref index))
            {
                continue;
            }

            var current = mediaQuery[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(mediaQuery, ref index, current);
                    continue;

                case '(':
                    depth++;
                    index++;
                    continue;

                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        mediaFeature = TrimWhitespace(mediaQuery.Slice(featureStart, index - featureStart));
                        index++;
                        return true;
                    }

                    index++;
                    continue;

                default:
                    index++;
                    break;
            }
        }

        return false;
    }

    private static bool EvaluateMediaFeature(ReadOnlySpan<char> mediaFeature, CssMediaContext mediaContext)
    {
        var separatorIndex = mediaFeature.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        var name = TrimWhitespace(mediaFeature.Slice(0, separatorIndex));
        var value = TrimWhitespace(mediaFeature.Slice(separatorIndex + 1));
        if (name.IsEmpty || value.IsEmpty)
        {
            return false;
        }

        if (name.Equals(WidthFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesExactDimension(value, mediaContext.WidthPixels);
        }

        if (name.Equals(MinWidthFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesMinimumDimension(value, mediaContext.WidthPixels);
        }

        if (name.Equals(MaxWidthFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesMaximumDimension(value, mediaContext.WidthPixels);
        }

        if (name.Equals(HeightFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesExactDimension(value, mediaContext.HeightPixels);
        }

        if (name.Equals(MinHeightFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesMinimumDimension(value, mediaContext.HeightPixels);
        }

        if (name.Equals(MaxHeightFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesMaximumDimension(value, mediaContext.HeightPixels);
        }

        if (name.Equals(OrientationFeature.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MatchesOrientation(value, mediaContext);
        }

        return false;
    }

    private static bool MatchesExactDimension(ReadOnlySpan<char> value, double currentPixels)
    {
        return TryParsePixelLength(value, out var requestedPixels) &&
               Math.Abs(requestedPixels - currentPixels) < 0.001d;
    }

    private static bool MatchesMinimumDimension(ReadOnlySpan<char> value, double currentPixels)
    {
        return TryParsePixelLength(value, out var requestedPixels) &&
               currentPixels + 0.001d >= requestedPixels;
    }

    private static bool MatchesMaximumDimension(ReadOnlySpan<char> value, double currentPixels)
    {
        return TryParsePixelLength(value, out var requestedPixels) &&
               currentPixels - 0.001d <= requestedPixels;
    }

    private static bool MatchesOrientation(ReadOnlySpan<char> value, CssMediaContext mediaContext)
    {
        var orientation = TrimWhitespace(value);
        if (orientation.Equals(LandscapeOrientation.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return mediaContext.WidthPixels >= mediaContext.HeightPixels;
        }

        if (orientation.Equals(PortraitOrientation.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return mediaContext.HeightPixels > mediaContext.WidthPixels;
        }

        return false;
    }

    private static bool TryParsePixelLength(ReadOnlySpan<char> value, out double pixels)
    {
        pixels = 0d;

        var normalized = TrimWhitespace(value);
        if (normalized.SequenceEqual("0".AsSpan()))
        {
            return true;
        }

        if (normalized.Length < 2 ||
            !IsAsciiLetter(normalized[normalized.Length - 2], 'p') ||
            !IsAsciiLetter(normalized[normalized.Length - 1], 'x'))
        {
            return false;
        }

        var numericPart = TrimWhitespace(normalized.Slice(0, normalized.Length - 2));
        return TryParseDoubleInvariant(numericPart, out pixels);
    }

    private static bool TryParseDoubleInvariant(ReadOnlySpan<char> value, out double parsed)
    {
#if NETSTANDARD20
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
#else
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
#endif
    }

    private static bool TryReadNextTopLevelStatement(string cssText, ref int index, out CssStatement statement)
    {
        SkipWhitespaceAndComments(cssText.AsSpan(), ref index);

        if (index >= cssText.Length)
        {
            statement = default;
            return false;
        }

        var start = index;
        var parenthesisDepth = 0;
        var cssSpan = cssText.AsSpan();

        while (index < cssText.Length)
        {
            if (TrySkipComment(cssSpan, ref index))
            {
                continue;
            }

            var current = cssText[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(cssSpan, ref index, current);
                    continue;

                case '(':
                    parenthesisDepth++;
                    index++;
                    continue;

                case ')' when parenthesisDepth > 0:
                    parenthesisDepth--;
                    index++;
                    continue;

                case ';' when parenthesisDepth == 0:
                    statement = new CssStatement(start, index, index + 1, CssStatementTerminator.Semicolon);
                    index++;
                    return true;

                case '{' when parenthesisDepth == 0:
                    index++;
                    SkipBlock(cssText, ref index);
                    statement = new CssStatement(start, index, index, CssStatementTerminator.Block);
                    return true;

                default:
                    index++;
                    break;
            }
        }

        statement = new CssStatement(start, index, index, CssStatementTerminator.EndOfFile);
        return true;
    }

    private static CssAtRuleKind GetAtRuleKind(string cssText, CssStatement statement)
    {
        if (statement.Length <= 0 || cssText[statement.Start] != '@')
        {
            return CssAtRuleKind.None;
        }

        var statementSpan = cssText.AsSpan(statement.Start, statement.Length);
        if (HasAtRuleKeyword(statementSpan, ImportAtRule))
        {
            return CssAtRuleKind.Import;
        }

        if (HasAtRuleKeyword(statementSpan, CharsetAtRule))
        {
            return CssAtRuleKind.Charset;
        }

        return CssAtRuleKind.Other;
    }

    private static bool HasAtRuleKeyword(ReadOnlySpan<char> statement, string atKeyword)
    {
        if (!statement.StartsWith(atKeyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return statement.Length == atKeyword.Length || !IsCssIdentifierCharacter(statement[atKeyword.Length]);
    }

    private static bool TryParseKnownImportRule(string cssText, CssStatement statement, out string href, out ReadOnlySpan<char> mediaCondition)
    {
        href = string.Empty;
        mediaCondition = default;

        if (statement.Terminator != CssStatementTerminator.Semicolon)
        {
            return false;
        }

        var statementContent = cssText.AsSpan(0, statement.ContentEndExclusive);
        var index = statement.Start + ImportAtRule.Length;
        SkipWhitespaceAndComments(statementContent, ref index);

        if (!TryReadImportHref(cssText, ref index, statement.ContentEndExclusive, out href))
        {
            return false;
        }

        SkipWhitespaceAndComments(statementContent, ref index);
        mediaCondition = TrimWhitespace(cssText.AsSpan(index, statement.ContentEndExclusive - index));
        return !string.IsNullOrWhiteSpace(href);
    }

    private static bool TryReadImportHref(string cssText, ref int index, int endExclusive, out string href)
    {
        href = string.Empty;

        if (index >= endExclusive)
        {
            return false;
        }

        var cssSpan = cssText.AsSpan(0, endExclusive);
        var current = cssText[index];
        if (current is '\'' or '"')
        {
            return TryReadQuotedValue(cssText, ref index, endExclusive, current, out href);
        }

        if (!cssSpan.Slice(index).StartsWith(UrlKeyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var boundaryIndex = index + UrlKeyword.Length;
        if (boundaryIndex < endExclusive && IsCssIdentifierCharacter(cssText[boundaryIndex]))
        {
            return false;
        }

        index = boundaryIndex;
        SkipWhitespaceAndComments(cssSpan, ref index);

        if (index >= endExclusive || cssText[index] != '(')
        {
            return false;
        }

        index++;
        SkipWhitespaceAndComments(cssSpan, ref index);

        if (index >= endExclusive)
        {
            return false;
        }

        if (cssText[index] is '\'' or '"')
        {
            var delimiter = cssText[index];
            if (!TryReadQuotedValue(cssText, ref index, endExclusive, delimiter, out href))
            {
                return false;
            }
        }
        else
        {
            var hrefStart = index;

            while (index < endExclusive && cssText[index] != ')')
            {
                if (TrySkipComment(cssSpan, ref index))
                {
                    continue;
                }

                index++;
            }

            href = TrimWhitespace(cssText.AsSpan(hrefStart, index - hrefStart)).ToString();
        }

        SkipWhitespaceAndComments(cssSpan, ref index);

        if (index >= endExclusive || cssText[index] != ')')
        {
            return false;
        }

        index++;
        return !string.IsNullOrWhiteSpace(href);
    }

    private static bool TryReadQuotedValue(string cssText, ref int index, int endExclusive, char delimiter, out string value)
    {
        value = string.Empty;

        if (index >= endExclusive || cssText[index] != delimiter)
        {
            return false;
        }

        index++;
        var start = index;
        StringBuilder? builder = null;

        while (index < endExclusive)
        {
            var current = cssText[index];
            if (current == '\\')
            {
                builder ??= new StringBuilder(endExclusive - start);
                builder.Append(cssText, start, index - start);
                index++;

                if (index >= endExclusive)
                {
                    return false;
                }

                builder.Append(cssText[index]);
                index++;
                start = index;
                continue;
            }

            if (current == delimiter)
            {
                if (builder is null)
                {
                    value = cssText.Substring(start, index - start);
                }
                else
                {
                    builder.Append(cssText, start, index - start);
                    value = builder.ToString();
                }

                index++;
                return true;
            }

            index++;
        }

        return false;
    }

    private static void AppendStatement(StringBuilder builder, string cssText, CssStatement statement)
    {
        if (statement.Length <= 0)
        {
            return;
        }

        builder.Append(cssText, statement.Start, statement.Length);
        builder.AppendLine();
    }

    private static void SkipWhitespaceAndComments(ReadOnlySpan<char> cssText, ref int index)
    {
        while (index < cssText.Length)
        {
            if (char.IsWhiteSpace(cssText[index]))
            {
                index++;
                continue;
            }

            if (!TrySkipComment(cssText, ref index))
            {
                break;
            }
        }
    }

    private static bool TrySkipComment(ReadOnlySpan<char> cssText, ref int index)
    {
        if (index + 1 >= cssText.Length || cssText[index] != '/' || cssText[index + 1] != '*')
        {
            return false;
        }

        index += 2;
        while (index + 1 < cssText.Length)
        {
            if (cssText[index] == '*' && cssText[index + 1] == '/')
            {
                index += 2;
                return true;
            }

            index++;
        }

        index = cssText.Length;
        return true;
    }

    private static void SkipQuotedString(ReadOnlySpan<char> cssText, ref int index, char delimiter)
    {
        index++;

        while (index < cssText.Length)
        {
            if (cssText[index] == '\\')
            {
                index = Math.Min(index + 2, cssText.Length);
                continue;
            }

            if (cssText[index] == delimiter)
            {
                index++;
                return;
            }

            index++;
        }
    }

    private static void SkipBlock(string cssText, ref int index)
    {
        var cssSpan = cssText.AsSpan();
        var depth = 1;

        while (index < cssText.Length && depth > 0)
        {
            if (TrySkipComment(cssSpan, ref index))
            {
                continue;
            }

            var current = cssText[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(cssSpan, ref index, current);
                    break;

                case '{':
                    depth++;
                    index++;
                    break;

                case '}':
                    depth--;
                    index++;
                    break;

                default:
                    index++;
                    break;
            }
        }
    }

    private static bool ConsumeLeadingKeyword(ref ReadOnlySpan<char> value, string keyword)
    {
        value = TrimWhitespace(value);
        if (value.Length <= keyword.Length ||
            !value.StartsWith(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            !char.IsWhiteSpace(value[keyword.Length]))
        {
            return false;
        }

        value = TrimStartWhitespace(value.Slice(keyword.Length));
        return true;
    }

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> value)
    {
        return TrimEndWhitespace(TrimStartWhitespace(value));
    }

    private static ReadOnlySpan<char> TrimStartWhitespace(ReadOnlySpan<char> value)
    {
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        return value.Slice(start);
    }

    private static ReadOnlySpan<char> TrimEndWhitespace(ReadOnlySpan<char> value)
    {
        var end = value.Length;
        while (end > 0 && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value.Slice(0, end);
    }

    private static bool IsAsciiLetter(char value, char expectedLower)
    {
        return value == expectedLower || value == char.ToUpperInvariant(expectedLower);
    }

    private static int SaturatingAddCapacity(int currentCapacity, int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            return currentCapacity;
        }

        return currentCapacity >= MaxStringBuilderCapacity - additionalCapacity
            ? MaxStringBuilderCapacity
            : currentCapacity + additionalCapacity;
    }

    private static bool IsCssIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '-' or '_';
    }

    private static CssMediaContext ResolveMediaContext(SvgDocument svgDocument)
    {
        var viewBox = svgDocument.ViewBox;
        var viewBoxWidth = viewBox.Width > 0d ? viewBox.Width : 0d;
        var viewBoxHeight = viewBox.Height > 0d ? viewBox.Height : 0d;

        // The loader does not know the eventual host control size yet, so the best available media
        // context is the SVG's own declared viewport. Prefer explicit root width/height, then the
        // intrinsic viewBox dimensions, and only then fall back to the historical standalone W3C
        // harness size.
        var widthPixels = ResolveViewportDimension(
            svgDocument,
            svgDocument.Width,
            svgDocument.Attributes.ContainsKey("width"),
            viewBoxWidth,
            StaticScreenWidthPixels);
        var heightPixels = ResolveViewportDimension(
            svgDocument,
            svgDocument.Height,
            svgDocument.Attributes.ContainsKey("height"),
            viewBoxHeight,
            StaticScreenHeightPixels);

        return new CssMediaContext(widthPixels, heightPixels);
    }

    private static double ResolveViewportDimension(
        SvgDocument svgDocument,
        SvgUnit dimension,
        bool hasExplicitDimension,
        double intrinsicPixels,
        double fallbackPixels)
    {
        if (!hasExplicitDimension && intrinsicPixels > 0d)
        {
            return intrinsicPixels;
        }

        if (TryResolveAbsolutePixels(svgDocument, dimension, out var absolutePixels))
        {
            return absolutePixels;
        }

        if (dimension.Type == SvgUnitType.Percentage)
        {
            var basePixels = intrinsicPixels > 0d ? intrinsicPixels : fallbackPixels;
            return basePixels * dimension.Value / 100d;
        }

        return intrinsicPixels > 0d ? intrinsicPixels : fallbackPixels;
    }

    private static bool TryResolveAbsolutePixels(SvgDocument svgDocument, SvgUnit dimension, out double pixels)
    {
        pixels = dimension.Value;

        switch (dimension.Type)
        {
            case SvgUnitType.Pixel:
            case SvgUnitType.User:
                return true;

            case SvgUnitType.Inch:
                pixels = dimension.Value * svgDocument.Ppi;
                return true;

            case SvgUnitType.Centimeter:
                pixels = dimension.Value / CentimetersPerInch * svgDocument.Ppi;
                return true;

            case SvgUnitType.Millimeter:
                pixels = dimension.Value / MillimetersPerInch * svgDocument.Ppi;
                return true;

            case SvgUnitType.Point:
                pixels = dimension.Value / PointsPerInch * svgDocument.Ppi;
                return true;

            case SvgUnitType.Pica:
                pixels = dimension.Value / PicasPerInch * svgDocument.Ppi;
                return true;

            default:
                pixels = 0d;
                return false;
        }
    }

    private static SvgCssStyleSource? TryLoadImportedStylesheet(string? href, Uri? baseUri, HashSet<string> importChain)
    {
        if (string.IsNullOrWhiteSpace(href) || baseUri is null)
        {
            return null;
        }

        // Keep import resolution intentionally conservative: only file-backed resources relative to
        // the current SVG/CSS source are loaded here. That matches the scenarios exercised by the
        // W3C fixtures and avoids inventing new network/resource-loading behavior in Svg.Skia.
        if (!Uri.TryCreate(baseUri, href, out var stylesheetUri) || !stylesheetUri.IsFile)
        {
            return null;
        }

        // Cycle protection is scoped to the currently expanding import chain so repeated imports in
        // separate top-level <style> blocks still participate in cascade order like they do in a
        // browser.
        if (!importChain.Add(stylesheetUri.AbsoluteUri))
        {
            return null;
        }

        var localPath = stylesheetUri.LocalPath;
        if (!File.Exists(localPath))
        {
            importChain.Remove(stylesheetUri.AbsoluteUri);
            return null;
        }

        try
        {
            return new SvgCssStyleSource(File.ReadAllText(localPath), stylesheetUri);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            importChain.Remove(stylesheetUri.AbsoluteUri);
            return null;
        }
    }

    private readonly struct AppliedDeclaration
    {
        public AppliedDeclaration(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }

    private readonly struct CssStatement
    {
        public CssStatement(int start, int contentEndExclusive, int endExclusive, CssStatementTerminator terminator)
        {
            Start = start;
            ContentEndExclusive = contentEndExclusive;
            EndExclusive = endExclusive;
            Terminator = terminator;
        }

        public int Start { get; }

        public int ContentEndExclusive { get; }

        public int EndExclusive { get; }

        public CssStatementTerminator Terminator { get; }

        public int Length => EndExclusive - Start;
    }

    private enum CssStatementTerminator
    {
        EndOfFile,
        Semicolon,
        Block,
    }

    private enum CssAtRuleKind
    {
        None,
        Import,
        Charset,
        Other,
    }

    private readonly struct CssMediaContext
    {
        public CssMediaContext(double widthPixels, double heightPixels)
        {
            WidthPixels = widthPixels;
            HeightPixels = heightPixels;
        }

        public double WidthPixels { get; }

        public double HeightPixels { get; }
    }
}

internal sealed class SvgCssStyleSource
{
    public SvgCssStyleSource(string content, Uri? baseUri)
    {
        Content = content;
        BaseUri = baseUri;
    }

    // The raw stylesheet text collected from a <style> element, SvgOptions.Css, or an imported
    // external file.
    public string Content { get; }

    // The URI that relative URLs inside Content should resolve against.
    public Uri? BaseUri { get; }
}
