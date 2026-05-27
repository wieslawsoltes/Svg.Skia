#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private const string MediaAtRule = "@media";
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

    public static void Apply(
        SvgDocument svgDocument,
        IReadOnlyCollection<SvgCssStyleSource> styles,
        SvgElementFactory elementFactory,
        SvgDocumentLoadOptions? loadOptions = null)
    {
        ApplyCore(svgDocument, svgDocument, styles, elementFactory, loadOptions, svgDocument);
    }

    internal static void ApplyScoped(
        SvgElement scopeRoot,
        SvgDocument svgDocument,
        IReadOnlyCollection<SvgCssStyleSource> styles,
        SvgElementFactory elementFactory,
        SvgDocumentLoadOptions? loadOptions = null)
    {
        ApplyCore(scopeRoot, svgDocument, styles, elementFactory, loadOptions, documentRootSelectorTarget: null);
    }

    private static void ApplyCore(
        SvgElement scopeRoot,
        SvgDocument svgDocument,
        IReadOnlyCollection<SvgCssStyleSource> styles,
        SvgElementFactory elementFactory,
        SvgDocumentLoadOptions? loadOptions,
        SvgDocument? documentRootSelectorTarget)
    {
        if (styles.Count == 0)
        {
            return;
        }

        var mediaContext = ResolveMediaContext(svgDocument);

        // Expand valid imports first so the final stylesheet matches browser evaluation order:
        // imported rules are inlined into the aggregate stylesheet before selector matching.
        var cssTotal = ExpandImportedStyles(styles, mediaContext, loadOptions);
        if (string.IsNullOrWhiteSpace(cssTotal))
        {
            return;
        }

        var stylesheetParser = new StylesheetParser(true, true, tolerateInvalidValues: true);
        var rootNode = new NonSvgElement();
        rootNode.Children.Add(scopeRoot);
        try
        {
            ApplyCustomPropertyRules(cssTotal, svgDocument, rootNode, elementFactory, stylesheetParser, mediaContext, documentRootSelectorTarget);
            ApplyRawSvgStaticPropertyRules(cssTotal, svgDocument, rootNode, elementFactory, stylesheetParser, mediaContext, documentRootSelectorTarget);
            ApplyStyleRules(cssTotal, svgDocument, rootNode, elementFactory, stylesheetParser, mediaContext, documentRootSelectorTarget);
        }
        finally
        {
            // QuerySelectorAll needs a synthetic parent so selectors like :root/descendant matching
            // can traverse from a stable container, but that wrapper must not leak into the live
            // document tree. Animation bindings capture child-index addresses later, and leaving
            // svgDocument.Parent pointing at this temporary node shifts every recorded path by one
            // extra level, which makes CreateAnimatedDocument fail to resolve targets on clones.
            _ = rootNode.Children.Remove(scopeRoot);
        }
    }

    internal static bool ShouldApplyMediaForCurrentContext(string? mediaCondition, SvgDocument? svgDocument)
    {
        var mediaContext = svgDocument is null
            ? new CssMediaContext(StaticScreenWidthPixels, StaticScreenHeightPixels)
            : ResolveMediaContext(svgDocument);

        return ShouldApplyMediaForCurrentContext(mediaCondition.AsSpan(), mediaContext);
    }

    private static void ApplyCustomPropertyRules(
        string cssText,
        SvgDocument svgDocument,
        SvgElement rootNode,
        SvgElementFactory elementFactory,
        StylesheetParser stylesheetParser,
        CssMediaContext mediaContext,
        SvgDocument? documentRootSelectorTarget)
    {
        var index = 0;
        while (TryReadNextTopLevelStatement(cssText, ref index, out var statement))
        {
            if (statement.Terminator != CssStatementTerminator.Block)
            {
                continue;
            }

            var atRuleKind = GetAtRuleKind(cssText, statement);
            if (atRuleKind == CssAtRuleKind.Media)
            {
                if (TryGetMediaRuleParts(cssText, statement, out var mediaCondition, out var nestedCssText) &&
                    ShouldApplyMediaForCurrentContext(mediaCondition, mediaContext))
                {
                    ApplyCustomPropertyRules(
                        nestedCssText,
                        svgDocument,
                        rootNode,
                        elementFactory,
                        stylesheetParser,
                        mediaContext,
                        documentRootSelectorTarget);
                }

                continue;
            }

            if (atRuleKind != CssAtRuleKind.None ||
                !TryGetStyleRuleParts(cssText, statement, out var selectorText, out var declarationsText))
            {
                continue;
            }

            var declarations = CreateCustomPropertyDeclarations(declarationsText);
            if (declarations.Count == 0)
            {
                continue;
            }

            try
            {
                var selectorSheet = stylesheetParser.Parse(selectorText + "{fill:inherit}");
                foreach (var rule in selectorSheet.StyleRules)
                {
                    foreach (var selector in EnumerateSelectorBranches(rule.Selector))
                    {
                        var specificity = selector.GetSpecificity();
                        var elemsToStyle = QuerySelectorAllIncludingSvgRoot(
                            rootNode,
                            selector,
                            GetSelectorText(selector, selectorText),
                            documentRootSelectorTarget,
                            elementFactory);

                        foreach (var elem in elemsToStyle.Distinct())
                        {
                            foreach (var declaration in declarations)
                            {
                                SvgCssVariableResolver.AddCustomProperty(elem, declaration.Name, declaration.Value, specificity);
                            }

                            if (elementFactory.PreserveJavaScriptDomState)
                            {
                                svgDocument.TrackCompatibilityStyleApplication(elem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.Message);
            }
        }
    }

    private static void ApplyRawSvgStaticPropertyRules(
        string cssText,
        SvgDocument svgDocument,
        SvgElement rootNode,
        SvgElementFactory elementFactory,
        StylesheetParser stylesheetParser,
        CssMediaContext mediaContext,
        SvgDocument? documentRootSelectorTarget)
    {
        var index = 0;
        while (TryReadNextTopLevelStatement(cssText, ref index, out var statement))
        {
            if (statement.Terminator != CssStatementTerminator.Block)
            {
                continue;
            }

            var atRuleKind = GetAtRuleKind(cssText, statement);
            if (atRuleKind == CssAtRuleKind.Media)
            {
                if (TryGetMediaRuleParts(cssText, statement, out var mediaCondition, out var nestedCssText) &&
                    ShouldApplyMediaForCurrentContext(mediaCondition, mediaContext))
                {
                    ApplyRawSvgStaticPropertyRules(
                        nestedCssText,
                        svgDocument,
                        rootNode,
                        elementFactory,
                        stylesheetParser,
                        mediaContext,
                        documentRootSelectorTarget);
                }

                continue;
            }

            if (atRuleKind != CssAtRuleKind.None ||
                !TryGetStyleRuleParts(cssText, statement, out var selectorText, out var declarationsText))
            {
                continue;
            }

            var declarations = CreateRawSvgStaticPropertyDeclarations(declarationsText);
            if (declarations.Count == 0)
            {
                continue;
            }

            try
            {
                var selectorSheet = stylesheetParser.Parse(selectorText + "{fill:inherit}");
                foreach (var rule in selectorSheet.StyleRules)
                {
                    foreach (var selector in EnumerateSelectorBranches(rule.Selector))
                    {
                        var specificity = selector.GetSpecificity();
                        var elemsToStyle = QuerySelectorAllIncludingSvgRoot(
                            rootNode,
                            selector,
                            GetSelectorText(selector, selectorText),
                            documentRootSelectorTarget,
                            elementFactory);

                        foreach (var elem in elemsToStyle.Distinct())
                        {
                            foreach (var declaration in declarations)
                            {
                                ApplyDeclaration(elem, declaration, specificity);
                            }

                            if (elementFactory.PreserveJavaScriptDomState)
                            {
                                svgDocument.TrackCompatibilityStyleApplication(elem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.Message);
            }
        }
    }

    private static void ApplyStyleRules(
        string cssText,
        SvgDocument svgDocument,
        SvgElement rootNode,
        SvgElementFactory elementFactory,
        StylesheetParser stylesheetParser,
        CssMediaContext mediaContext,
        SvgDocument? documentRootSelectorTarget)
    {
        var index = 0;
        while (TryReadNextTopLevelStatement(cssText, ref index, out var statement))
        {
            if (statement.Terminator != CssStatementTerminator.Block)
            {
                continue;
            }

            var atRuleKind = GetAtRuleKind(cssText, statement);
            if (atRuleKind == CssAtRuleKind.Media)
            {
                if (TryGetMediaRuleParts(cssText, statement, out var mediaCondition, out var nestedCssText) &&
                    ShouldApplyMediaForCurrentContext(mediaCondition, mediaContext))
                {
                    ApplyStyleRules(
                        nestedCssText,
                        svgDocument,
                        rootNode,
                        elementFactory,
                        stylesheetParser,
                        mediaContext,
                        documentRootSelectorTarget);
                }

                continue;
            }

            if (atRuleKind != CssAtRuleKind.None ||
                !TryGetStyleRuleParts(cssText, statement, out var selectorText, out var declarationsText))
            {
                continue;
            }

            try
            {
                var selectorSheet = stylesheetParser.Parse(selectorText + "{" + declarationsText + "}");
                foreach (var rule in selectorSheet.StyleRules)
                {
                    var declarations = CreateAppliedDeclarations(rule);
                    if (declarations.Count == 0)
                    {
                        continue;
                    }

                    foreach (var selector in EnumerateSelectorBranches(rule.Selector))
                    {
                        var specificity = selector.GetSpecificity();
                        var projectsLinkStylesToText = ContainsLinkPseudoClass(selector);
                        var elemsToStyle = QuerySelectorAllIncludingSvgRoot(
                            rootNode,
                            selector,
                            GetSelectorText(selector, rule.SelectorText),
                            documentRootSelectorTarget,
                            elementFactory);

                        foreach (var elem in elemsToStyle.Distinct())
                        {
                            SvgTextBase? textContainer = null;
                            var projectsToTextContainer = projectsLinkStylesToText &&
                                                          TryGetLinkTextContainer(elem, out textContainer);

                            foreach (var declaration in declarations)
                            {
                                ApplyDeclaration(elem, declaration, specificity);

                                if (projectsToTextContainer)
                                {
                                    ApplyDeclaration(textContainer!, declaration, specificity);
                                }
                            }

                            if (elementFactory.PreserveJavaScriptDomState)
                            {
                                svgDocument.TrackCompatibilityStyleApplication(elem);
                                if (projectsToTextContainer)
                                {
                                    svgDocument.TrackCompatibilityStyleApplication(textContainer!);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.Message);
            }
        }
    }

    private static List<AppliedDeclaration> CreateAppliedDeclarations(IStyleRule rule)
    {
        var result = new List<AppliedDeclaration>();
        foreach (var declaration in rule.Style)
        {
            if (string.IsNullOrWhiteSpace(declaration.Original))
            {
                continue;
            }

            result.Add(new AppliedDeclaration(declaration.Name, declaration.Original, declaration.IsImportant));
        }

        return result;
    }

    private static IEnumerable<ISelector> EnumerateSelectorBranches(ISelector selector)
    {
        if (selector is ListSelector listSelector)
        {
            foreach (var branch in listSelector)
            {
                yield return branch;
            }

            yield break;
        }

        yield return selector;
    }

    private static IEnumerable<SvgElement> QuerySelectorAllIncludingSvgRoot(
        SvgElement rootNode,
        ISelector selector,
        string selectorText,
        SvgDocument? svgDocument,
        SvgElementFactory elementFactory)
    {
        var elemsToStyle = rootNode.QuerySelectorAll(selector, elementFactory);
        if (svgDocument is not null && SelectorListMatchesSvgRoot(selectorText, svgDocument))
        {
            elemsToStyle = elemsToStyle.Concat(new[] { svgDocument });
        }

        return elemsToStyle;
    }

    private static string GetSelectorText(ISelector selector, string fallback)
    {
        return !string.IsNullOrWhiteSpace(selector.Text)
            ? selector.Text
            : fallback;
    }

    private static bool SelectorListMatchesSvgRoot(string selectorText, SvgDocument svgDocument)
    {
        var segmentStart = 0;
        var index = 0;
        var parenthesisDepth = 0;

        while (index < selectorText.Length)
        {
            var current = selectorText[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(selectorText.AsSpan(), ref index, current);
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
                    if (SelectorSegmentMatchesSvgRoot(
                            selectorText.AsSpan(segmentStart, index - segmentStart),
                            svgDocument))
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

        return SelectorSegmentMatchesSvgRoot(
            selectorText.AsSpan(segmentStart),
            svgDocument);
    }

    private static bool SelectorSegmentMatchesSvgRoot(
        ReadOnlySpan<char> selector,
        SvgDocument svgDocument)
    {
        var trimmed = TrimWhitespace(selector);
        if (trimmed.Equals("svg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(":root".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (TryGetSvgRootSelectorSuffix(trimmed, "svg", out var suffix) ||
                TryGetSvgRootSelectorSuffix(trimmed, ":root", out suffix)) &&
               SvgRootSelectorSuffixMatches(svgDocument, suffix);
    }

    private static bool TryGetSvgRootSelectorSuffix(
        ReadOnlySpan<char> selector,
        string rootSelector,
        out ReadOnlySpan<char> suffix)
    {
        suffix = default;
        if (!selector.StartsWith(rootSelector.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        suffix = selector.Slice(rootSelector.Length);
        if (suffix.Length > 0 &&
            suffix[0] is not ('.' or '#' or '['))
        {
            return false;
        }

        return IsSimpleSelectorSuffix(suffix);
    }

    private static bool IsSimpleSelectorSuffix(ReadOnlySpan<char> selector)
    {
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var index = 0;
        while (index < selector.Length)
        {
            var current = selector[index];
            switch (current)
            {
                case '\'':
                case '"':
                    SkipQuotedString(selector, ref index, current);
                    continue;

                case '[':
                    bracketDepth++;
                    index++;
                    continue;

                case ']' when bracketDepth > 0:
                    bracketDepth--;
                    index++;
                    continue;

                case '(':
                    parenthesisDepth++;
                    index++;
                    continue;

                case ')' when parenthesisDepth > 0:
                    parenthesisDepth--;
                    index++;
                    continue;

                case '>' or '+' or '~' or ',' when parenthesisDepth == 0 && bracketDepth == 0:
                    return false;

                default:
                    if (char.IsWhiteSpace(current) && parenthesisDepth == 0 && bracketDepth == 0)
                    {
                        return false;
                    }

                    index++;
                    break;
            }
        }

        return parenthesisDepth == 0 && bracketDepth == 0;
    }

    private static bool SvgRootSelectorSuffixMatches(SvgDocument svgDocument, ReadOnlySpan<char> selector)
    {
        var index = 0;
        while (index < selector.Length)
        {
            switch (selector[index])
            {
                case '.':
                    index++;
                    if (!TryReadSimpleSelectorIdentifier(selector, ref index, out var className) ||
                        !SvgRootHasClass(svgDocument, className))
                    {
                        return false;
                    }

                    break;

                case '#':
                    index++;
                    if (!TryReadSimpleSelectorIdentifier(selector, ref index, out var id) ||
                        !string.Equals(svgDocument.ID, id, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    break;

                case '[':
                    if (!TryReadAttributeSelector(selector, ref index, out var attributeName, out var attributeValue) ||
                        !svgDocument.TryGetAttribute(attributeName, out var actualValue))
                    {
                        return false;
                    }

                    if (attributeValue is not null &&
                        !string.Equals(actualValue, attributeValue, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool SvgRootHasClass(SvgDocument svgDocument, string className)
    {
        if (!svgDocument.TryGetAttribute("class", out var classAttribute) ||
            string.IsNullOrWhiteSpace(classAttribute))
        {
            return false;
        }

        foreach (var token in classAttribute.Split(new[] { ' ', '\t', '\r', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(token, className, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadSimpleSelectorIdentifier(ReadOnlySpan<char> selector, ref int index, out string identifier)
    {
        var start = index;
        while (index < selector.Length && IsSimpleSelectorIdentifierCharacter(selector[index]))
        {
            index++;
        }

        if (index == start)
        {
            identifier = string.Empty;
            return false;
        }

        identifier = selector.Slice(start, index - start).ToString();
        return true;
    }

    private static bool IsSimpleSelectorIdentifierCharacter(char character)
    {
        return char.IsLetterOrDigit(character) ||
               character is '_' or '-';
    }

    private static bool TryReadAttributeSelector(
        ReadOnlySpan<char> selector,
        ref int index,
        out string name,
        out string? value)
    {
        name = string.Empty;
        value = null;
        var start = index;
        var current = index + 1;
        var quote = '\0';
        while (current < selector.Length)
        {
            var character = selector[current];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                current++;
                continue;
            }

            if (character == ']')
            {
                var content = TrimWhitespace(selector.Slice(start + 1, current - start - 1));
                if (!TryReadAttributeSelectorContent(content, out name, out value))
                {
                    return false;
                }

                index = current + 1;
                return true;
            }

            current++;
        }

        return false;
    }

    private static bool TryReadAttributeSelectorContent(ReadOnlySpan<char> content, out string name, out string? value)
    {
        name = string.Empty;
        value = null;
        var separatorIndex = content.IndexOf('=');
        if (separatorIndex < 0)
        {
            name = content.ToString();
            return name.Length > 0;
        }

        if (separatorIndex > 0 &&
            content[separatorIndex - 1] is '~' or '|' or '^' or '$' or '*')
        {
            return false;
        }

        name = TrimWhitespace(content.Slice(0, separatorIndex)).ToString();
        value = UnquoteCssAttributeValue(TrimWhitespace(content.Slice(separatorIndex + 1)));
        return name.Length > 0;
    }

    private static string UnquoteCssAttributeValue(ReadOnlySpan<char> value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[value.Length - 1] == '"') ||
             (value[0] == '\'' && value[value.Length - 1] == '\'')))
        {
            return value.Slice(1, value.Length - 2).ToString();
        }

        return value.ToString();
    }

    private static void ApplyDeclaration(SvgElement element, AppliedDeclaration declaration, int specificity)
    {
        if (SvgCssVariableResolver.IsCustomPropertyName(declaration.Name))
        {
            // Custom properties are already applied from raw CSS text so ExCSS does not
            // duplicate partial parser output with a newer source order.
            return;
        }

        var value = declaration.Value;
        var effectiveSpecificity = SvgCssDeclarationPriority.NormalizePriority(ref value, specificity, declaration.Important);

        if (SvgCssPaintDeclarationValidator.ShouldIgnoreInvalidPaintDeclaration(
                element,
                declaration.Name,
                value))
        {
            return;
        }

        if (SvgComputedStyleMetadata.ShouldIgnoreInvalidDeclaration(declaration.Name, value))
        {
            return;
        }

        element.AddCompatibilityStyle(declaration.Name, NormalizeReferenceUrl(declaration.Name, value), effectiveSpecificity);
    }

    private static string NormalizeReferenceUrl(string name, string value)
    {
        if (!IsReferenceStyleProperty(name))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 8 &&
            trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            var inner = trimmed.Substring(4, trimmed.Length - 5).Trim();
            if (inner.Length >= 2 &&
                ((inner[0] == '"' && inner[inner.Length - 1] == '"') ||
                 (inner[0] == '\'' && inner[inner.Length - 1] == '\'')))
            {
                return "url(" + inner.Substring(1, inner.Length - 2) + ")";
            }
        }

        return value;
    }

    private static bool IsReferenceStyleProperty(string name)
    {
        return name.Equals("clip-path", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("filter", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-end", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-mid", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-start", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mask", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStyleRuleParts(
        string cssText,
        CssStatement statement,
        out string selectorText,
        out string declarationsText)
    {
        selectorText = string.Empty;
        declarationsText = string.Empty;

        if (!TryFindTopLevelBlockOpen(cssText, statement.Start, statement.EndExclusive, out var openBraceIndex))
        {
            return false;
        }

        var closeBraceIndex = statement.EndExclusive - 1;
        if (closeBraceIndex <= openBraceIndex || cssText[closeBraceIndex] != '}')
        {
            return false;
        }

        selectorText = TrimWhitespace(cssText.AsSpan(statement.Start, openBraceIndex - statement.Start)).ToString();
        declarationsText = cssText.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
        return !string.IsNullOrWhiteSpace(selectorText);
    }

    private static bool TryGetMediaRuleParts(
        string cssText,
        CssStatement statement,
        out ReadOnlySpan<char> mediaCondition,
        out string nestedCssText)
    {
        mediaCondition = default;
        nestedCssText = string.Empty;

        if (!TryFindTopLevelBlockOpen(cssText, statement.Start, statement.EndExclusive, out var openBraceIndex))
        {
            return false;
        }

        var closeBraceIndex = statement.EndExclusive - 1;
        if (closeBraceIndex <= openBraceIndex || cssText[closeBraceIndex] != '}')
        {
            return false;
        }

        var conditionStart = statement.Start + MediaAtRule.Length;
        mediaCondition = TrimWhitespace(cssText.AsSpan(conditionStart, openBraceIndex - conditionStart));
        nestedCssText = cssText.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
        return true;
    }

    private static bool TryFindTopLevelBlockOpen(string cssText, int startIndex, int endExclusive, out int openBraceIndex)
    {
        openBraceIndex = -1;
        var cssSpan = cssText.AsSpan(0, endExclusive);
        var parenthesisDepth = 0;
        var index = startIndex;

        while (index < endExclusive)
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

                case '{' when parenthesisDepth == 0:
                    openBraceIndex = index;
                    return true;

                default:
                    index++;
                    break;
            }
        }

        return false;
    }

    private static List<AppliedDeclaration> CreateCustomPropertyDeclarations(string declarationsText)
    {
        var result = new List<AppliedDeclaration>();
        var index = 0;

        while (true)
        {
            if (!SkipIgnorableDeclarationContent(declarationsText, ref index))
            {
                return result;
            }

            if (index >= declarationsText.Length)
            {
                return result;
            }

            var declarationStart = index;
            if (!TryReadDeclaration(declarationsText, ref index, out var name, out var value))
            {
                if (index <= declarationStart)
                {
                    return result;
                }

                continue;
            }

            if (SvgCssVariableResolver.IsCustomPropertyName(name))
            {
                result.Add(new AppliedDeclaration(name, value));
            }
        }
    }

    private static List<AppliedDeclaration> CreateRawSvgStaticPropertyDeclarations(string declarationsText)
    {
        var result = new List<AppliedDeclaration>();
        var index = 0;

        while (true)
        {
            if (!SkipIgnorableDeclarationContent(declarationsText, ref index))
            {
                return result;
            }

            if (index >= declarationsText.Length)
            {
                return result;
            }

            var declarationStart = index;
            if (!TryReadDeclaration(declarationsText, ref index, out var name, out var value))
            {
                if (index <= declarationStart)
                {
                    return result;
                }

                continue;
            }

            if (IsRawSvgStaticPropertyName(name))
            {
                result.Add(new AppliedDeclaration(name, value));
            }
        }
    }

    private static bool IsRawSvgStaticPropertyName(string name)
    {
        return name.Equals("cx", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("cy", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("d", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("height", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("r", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("rx", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("ry", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("transform-box", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("transform-origin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("width", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x2", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadDeclaration(string declarationsText, ref int index, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        var declarationStart = index;

        if (!TryFindDeclarationEnd(declarationsText, ref index, out var declarationEnd) ||
            !TryFindDeclarationSeparator(declarationsText, declarationStart, declarationEnd, out var separatorIndex))
        {
            return false;
        }

        name = NormalizeDeclarationSegment(declarationsText, declarationStart, separatorIndex - declarationStart);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        value = NormalizeDeclarationSegment(declarationsText, separatorIndex + 1, declarationEnd - separatorIndex - 1);
        return true;
    }

    private static bool TryFindDeclarationEnd(string declarationsText, ref int index, out int declarationEnd)
    {
        var quote = '\0';
        var escape = false;
        var parentheses = 0;
        var current = index;

        while (current < declarationsText.Length)
        {
            var character = declarationsText[current];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (character == '/' && current + 1 < declarationsText.Length && declarationsText[current + 1] == '*')
            {
                current += 2;
                if (!TrySkipDeclarationComment(declarationsText, ref current))
                {
                    declarationEnd = 0;
                    return false;
                }

                continue;
            }

            switch (character)
            {
                case '\'':
                case '"':
                    quote = character;
                    break;
                case '(':
                    parentheses++;
                    break;
                case ')':
                    if (parentheses > 0)
                    {
                        parentheses--;
                    }

                    break;
                case ';' when parentheses == 0:
                    declarationEnd = current;
                    current++;
                    index = current;
                    return true;
            }

            current++;
        }

        declarationEnd = current;
        index = current;
        return quote == '\0' && parentheses == 0;
    }

    private static bool TryFindDeclarationSeparator(string declarationsText, int startIndex, int endIndex, out int separatorIndex)
    {
        var quote = '\0';
        var escape = false;
        var parentheses = 0;

        for (var i = startIndex; i < endIndex; i++)
        {
            var character = declarationsText[i];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character == '/' && i + 1 < endIndex && declarationsText[i + 1] == '*')
            {
                i += 2;
                if (!TrySkipDeclarationComment(declarationsText, ref i, endIndex))
                {
                    separatorIndex = -1;
                    return false;
                }

                i--;
                continue;
            }

            switch (character)
            {
                case '\'':
                case '"':
                    quote = character;
                    break;
                case '(':
                    parentheses++;
                    break;
                case ')':
                    if (parentheses > 0)
                    {
                        parentheses--;
                    }

                    break;
                case ':' when parentheses == 0:
                    separatorIndex = i;
                    return true;
            }
        }

        separatorIndex = -1;
        return false;
    }

    private static bool SkipIgnorableDeclarationContent(string declarationsText, ref int index)
    {
        while (index < declarationsText.Length)
        {
            var character = declarationsText[index];
            if (char.IsWhiteSpace(character) || character == ';')
            {
                index++;
                continue;
            }

            if (character == '/' && index + 1 < declarationsText.Length && declarationsText[index + 1] == '*')
            {
                index += 2;
                if (!TrySkipDeclarationComment(declarationsText, ref index))
                {
                    return false;
                }

                continue;
            }

            break;
        }

        return true;
    }

    private static bool TrySkipDeclarationComment(string declarationsText, ref int index)
    {
        return TrySkipDeclarationComment(declarationsText, ref index, declarationsText.Length);
    }

    private static bool TrySkipDeclarationComment(string declarationsText, ref int index, int endIndex)
    {
        while (index + 1 < endIndex)
        {
            if (declarationsText[index] == '*' && declarationsText[index + 1] == '/')
            {
                index += 2;
                return true;
            }

            index++;
        }

        return false;
    }

    private static string NormalizeDeclarationSegment(string declarationsText, int startIndex, int length)
    {
        if (!TryTrimDeclarationSegment(declarationsText, startIndex, length, out var trimmedStart, out var trimmedLength))
        {
            return string.Empty;
        }

        StringBuilder? builder = null;
        var quote = '\0';
        var escape = false;
        var segmentEnd = trimmedStart + trimmedLength;

        for (var i = trimmedStart; i < segmentEnd; i++)
        {
            var character = declarationsText[i];
            if (quote != '\0')
            {
                builder?.Append(character);
                if (escape)
                {
                    escape = false;
                }
                else if (character == '\\')
                {
                    escape = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character == '/' && i + 1 < segmentEnd && declarationsText[i + 1] == '*')
            {
                builder ??= new StringBuilder(trimmedLength);
                if (builder.Length == 0 && i > trimmedStart)
                {
                    builder.Append(declarationsText, trimmedStart, i - trimmedStart);
                }

                i += 2;
                if (!TrySkipDeclarationComment(declarationsText, ref i, segmentEnd))
                {
                    return declarationsText.Substring(trimmedStart, trimmedLength);
                }

                i--;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }

            builder?.Append(character);
        }

        return builder is null
            ? declarationsText.Substring(trimmedStart, trimmedLength)
            : builder.ToString().Trim();
    }

    private static bool TryTrimDeclarationSegment(
        string declarationsText,
        int startIndex,
        int length,
        out int trimmedStart,
        out int trimmedLength)
    {
        if (length <= 0)
        {
            trimmedStart = 0;
            trimmedLength = 0;
            return false;
        }

        var endIndex = startIndex + length - 1;
        while (startIndex <= endIndex && char.IsWhiteSpace(declarationsText[startIndex]))
        {
            startIndex++;
        }

        while (endIndex >= startIndex && char.IsWhiteSpace(declarationsText[endIndex]))
        {
            endIndex--;
        }

        if (startIndex > endIndex)
        {
            trimmedStart = 0;
            trimmedLength = 0;
            return false;
        }

        trimmedStart = startIndex;
        trimmedLength = endIndex - startIndex + 1;
        return true;
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

    private static string ExpandImportedStyles(IReadOnlyCollection<SvgCssStyleSource> sources, CssMediaContext mediaContext, SvgDocumentLoadOptions? loadOptions)
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
            AppendExpandedStyles(builder, source.Content, source.BaseUri, source.BaseUri, mediaContext, loadOptions, CreateImportChain());
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
        Uri? policyBaseUri,
        CssMediaContext mediaContext,
        SvgDocumentLoadOptions? loadOptions,
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
                    ShouldApplyMediaForCurrentContext(mediaCondition, mediaContext))
                {
                    var imported = TryLoadImportedStylesheet(href, baseUri, policyBaseUri, loadOptions, importChain);
                    if (imported is not null)
                    {
                        try
                        {
                            AppendExpandedStyles(builder, imported.Content, imported.BaseUri, policyBaseUri, mediaContext, loadOptions, importChain);
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

    private static bool ShouldApplyMediaForCurrentContext(ReadOnlySpan<char> mediaCondition, CssMediaContext mediaContext)
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

        if (HasAtRuleKeyword(statementSpan, MediaAtRule))
        {
            return CssAtRuleKind.Media;
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

        if (statement.Terminator != CssStatementTerminator.Semicolon &&
            statement.Terminator != CssStatementTerminator.EndOfFile)
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

    internal static SvgCssStyleSource? TryLoadLinkedStylesheet(string? href, Uri? baseUri, SvgDocumentLoadOptions? loadOptions)
    {
        return TryLoadStylesheetResource(href, baseUri, baseUri, loadOptions, SvgExternalResourcePolicy.SameDocumentAndDataOnly, importChain: null);
    }

    private static SvgCssStyleSource? TryLoadImportedStylesheet(string? href, Uri? baseUri, Uri? policyBaseUri, SvgDocumentLoadOptions? loadOptions, HashSet<string> importChain)
    {
        return TryLoadStylesheetResource(href, baseUri, policyBaseUri, loadOptions, SvgExternalResourcePolicy.SameDocumentAndDataOnly, importChain);
    }

    private static SvgCssStyleSource? TryLoadStylesheetResource(
        string? href,
        Uri? baseUri,
        Uri? policyBaseUri,
        SvgDocumentLoadOptions? loadOptions,
        SvgExternalResourcePolicy minimumPolicyForData,
        HashSet<string>? importChain)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        Uri stylesheetUri;
        if (baseUri is { })
        {
            if (!Uri.TryCreate(baseUri, href, out stylesheetUri!))
            {
                return null;
            }
        }
        else if (!Uri.TryCreate(href, UriKind.Absolute, out stylesheetUri!))
        {
            return null;
        }

        if (!AllowsStylesheetResource(stylesheetUri, policyBaseUri, loadOptions, minimumPolicyForData))
        {
            return null;
        }

        if (stylesheetUri.IsAbsoluteUri && stylesheetUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return TryLoadDataStylesheet(stylesheetUri, importChain);
        }

        // Keep non-data stylesheet resolution intentionally conservative: only file-backed resources
        // relative to the current SVG/CSS source are loaded here. That matches the scenarios
        // exercised by the W3C fixtures and avoids inventing new network/resource-loading behavior
        // in Svg.Skia.
        if (!stylesheetUri.IsFile)
        {
            return null;
        }

        // Cycle protection is scoped to the currently expanding import chain so repeated imports in
        // separate top-level <style> blocks still participate in cascade order like they do in a
        // browser.
        if (importChain is { } && !importChain.Add(stylesheetUri.AbsoluteUri))
        {
            return null;
        }

        var localPath = stylesheetUri.LocalPath;
        if (!File.Exists(localPath))
        {
            importChain?.Remove(stylesheetUri.AbsoluteUri);
            return null;
        }

        try
        {
            return new SvgCssStyleSource(File.ReadAllText(localPath), stylesheetUri);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            importChain?.Remove(stylesheetUri.AbsoluteUri);
            return null;
        }
    }

    private static SvgCssStyleSource? TryLoadDataStylesheet(Uri stylesheetUri, HashSet<string>? importChain)
    {
        if (importChain is { } && !importChain.Add(stylesheetUri.AbsoluteUri))
        {
            return null;
        }

        var loaded = false;
        try
        {
            loaded = TryReadDataStylesheet(stylesheetUri.OriginalString, out var css);
            return loaded ? new SvgCssStyleSource(css, stylesheetUri) : null;
        }
        finally
        {
            if (!loaded)
            {
                importChain?.Remove(stylesheetUri.AbsoluteUri);
            }
        }
    }

    private static bool TryReadDataStylesheet(string dataUri, out string css)
    {
        css = string.Empty;

        var headerStartIndex = 5;
        var headerEndIndex = dataUri.IndexOf(",", headerStartIndex, StringComparison.Ordinal);
        if (headerEndIndex < 0 || headerEndIndex + 1 > dataUri.Length)
        {
            return false;
        }

        var mimeType = "text/plain";
        var charset = "US-ASCII";
        var base64 = false;
        var headers = dataUri.Substring(headerStartIndex, headerEndIndex - headerStartIndex).Split(';');
        var headerIndex = 0;
        if (headers.Length > 0 && headers[0].Contains("/"))
        {
            mimeType = headers[0].Trim();
            charset = string.Empty;
            headerIndex = 1;
        }

        if (!mimeType.Equals(CssMimeType, StringComparison.OrdinalIgnoreCase) &&
            !mimeType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (; headerIndex < headers.Length; headerIndex++)
        {
            var header = headers[headerIndex].Trim();
            if (header.Equals("base64", StringComparison.OrdinalIgnoreCase))
            {
                base64 = true;
                continue;
            }

            var separatorIndex = header.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var attribute = header.Substring(0, separatorIndex).Trim();
            if (attribute.Equals("charset", StringComparison.OrdinalIgnoreCase))
            {
                charset = header.Substring(separatorIndex + 1).Trim();
            }
        }

        var data = dataUri.Substring(headerEndIndex + 1);
        if (base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(data);
                var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                css = encoding.GetString(bytes);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                return false;
            }
        }

        try
        {
            css = Uri.UnescapeDataString(data);
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool AllowsStylesheetResource(
        Uri stylesheetUri,
        Uri? policyBaseUri,
        SvgDocumentLoadOptions? loadOptions,
        SvgExternalResourcePolicy minimumPolicyForData)
    {
        var effectivePolicy = SvgExternalResourceResolver.GetEffectiveExternalResourcePolicy(
            loadOptions ?? new SvgDocumentLoadOptions());
        if (policyBaseUri is not null)
        {
            return SvgExternalResourceResolver.AllowsStylesheetResource(
                stylesheetUri,
                policyBaseUri,
                loadOptions ?? new SvgDocumentLoadOptions(),
                minimumPolicyForData);
        }

        return effectivePolicy switch
        {
            SvgExternalResourcePolicy.Disabled => false,
            SvgExternalResourcePolicy.SameDocumentAndDataOnly => IsDataUri(stylesheetUri) &&
                                                                 minimumPolicyForData == SvgExternalResourcePolicy.SameDocumentAndDataOnly,
            SvgExternalResourcePolicy.SameOrigin => IsDataUri(stylesheetUri) ||
                                                    IsSameOriginStylesheet(stylesheetUri, policyBaseUri),
            _ => true
        };
    }

    private static bool IsDataUri(Uri uri)
    {
        return uri.IsAbsoluteUri &&
               uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOriginStylesheet(Uri resourceUri, Uri? baseUri)
    {
        if (baseUri is not { IsAbsoluteUri: true } || !resourceUri.IsAbsoluteUri)
        {
            return false;
        }

        if (resourceUri.IsFile || baseUri.IsFile)
        {
            return IsFileResourceUnderBaseDirectory(resourceUri, baseUri);
        }

        return resourceUri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               resourceUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
               resourceUri.Port == baseUri.Port;
    }

    private static bool IsFileResourceUnderBaseDirectory(Uri resourceUri, Uri? baseUri)
    {
        if (baseUri is not { IsFile: true } || !resourceUri.IsFile)
        {
            return false;
        }

        var basePath = Path.GetFullPath(baseUri.LocalPath);
        var baseDirectory = Directory.Exists(basePath)
            ? basePath
            : Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            return false;
        }

        var resourcePath = Path.GetFullPath(resourceUri.LocalPath);
        var normalizedBaseDirectory = EnsureTrailingDirectorySeparator(baseDirectory);
        return resourcePath.StartsWith(normalizedBaseDirectory, GetPathComparison());
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
            path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private readonly struct AppliedDeclaration
    {
        public AppliedDeclaration(string name, string value, bool important = false)
        {
            Name = name;
            Value = value;
            Important = important;
        }

        public string Name { get; }

        public string Value { get; }

        public bool Important { get; }
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
        Media,
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
