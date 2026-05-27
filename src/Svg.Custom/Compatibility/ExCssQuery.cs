using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExCSS;

namespace Svg.Css
{
    /// <summary>
    /// Svg.Custom override of the upstream ExCSS selector adapter.
    ///
    /// The base project only needs enough selector support to apply stylesheet rules onto an SVG
    /// DOM. The browser-backed W3C rows exposed a few gaps where the generic ExCSS behavior is too
    /// optimistic for a static renderer:
    ///
    /// 1. Interactive pseudo-classes such as <c>:hover</c> and <c>:active</c> should never match,
    ///    because Svg.Skia does not execute an interaction runtime while loading a document.
    /// 2. <c>:link</c> in SVG needs to style both the anchor node and, for text content, the
    ///    surrounding text container that actually owns the rendered glyph paint.
    /// 3. <c>:lang(...)</c> must honor inherited <c>xml:lang</c>/<c>lang</c> values the same way a
    ///    browser does, even though those attributes can be stored in a few different forms inside
    ///    the Svg DOM model.
    ///
    /// Everything else stays on the upstream selector pipeline so that we only diverge where the
    /// browser-compatibility tests proved we needed to.
    /// </summary>
    internal static class ExCssQuery
    {
        private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

        public static IEnumerable<SvgElement> QuerySelectorAll(this SvgElement elem, ISelector selector, SvgElementFactory elementFactory)
        {
            var input = Enumerable.Repeat(elem, 1);
            var ops = new ExSvgElementOps(elementFactory);

            var func = GetFunc(selector, ops, ops.Universal());
            var descendants = ops.Descendant();
            var func1 = func;
            func = f => func1(descendants(f));
            return func(input).Distinct();
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            CompoundSelector selector,
            ExSvgElementOps ops,
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> inFunc)
        {
            foreach (var it in selector)
            {
                inFunc = GetFunc(it, ops, inFunc);
            }

            return inFunc;
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            FirstChildSelector selector,
            ExSvgElementOps ops)
        {
            var step = selector.Step;
            var offset = selector.Offset;

            if (offset == 0)
            {
                return ops.FirstChild();
            }

            return ops.NthChild(step, offset);
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            FirstTypeSelector selector,
            ExSvgElementOps ops)
        {
            var step = selector.Step;
            var offset = selector.Offset;

            return ops.NthType(step, offset);
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            LastTypeSelector selector,
            ExSvgElementOps ops)
        {
            var step = selector.Step;
            var offset = selector.Offset;

            return ops.NthLastType(step, offset);
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            LastChildSelector selector,
            ExSvgElementOps ops)
        {
            var step = selector.Step;
            var offset = selector.Offset;

            if (offset == 0)
            {
                return ops.LastChild();
            }

            return ops.NthLastChild(step, offset);
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            ListSelector listSelector,
            ExSvgElementOps ops,
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> inFunc)
        {
            List<Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>>> results = new();

            foreach (var selector in listSelector)
            {
                results.Add(GetFunc(selector, ops, null));
            }

            return f =>
            {
                var svgElements = inFunc(f);
                var nodes = results[0](svgElements);
                for (var i = 1; i < results.Count; i++)
                {
                    nodes = nodes.Union(results[i](svgElements));
                }

                return nodes;
            };
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            PseudoClassSelector selector,
            ExSvgElementOps ops,
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> inFunc)
        {
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> pseudoFunc;
            if (selector.Class == PseudoClassNames.FirstChild)
            {
                pseudoFunc = ops.FirstChild();
            }
            else if (selector.Class == PseudoClassNames.LastChild)
            {
                pseudoFunc = ops.LastChild();
            }
            else if (selector.Class == PseudoClassNames.Empty)
            {
                pseudoFunc = ops.Empty();
            }
            else if (selector.Class == PseudoClassNames.OnlyChild)
            {
                pseudoFunc = ops.OnlyChild();
            }
            else if (string.Equals(selector.Class, "first-of-type", StringComparison.OrdinalIgnoreCase))
            {
                pseudoFunc = ops.NthType(0, 1);
            }
            else if (string.Equals(selector.Class, "last-of-type", StringComparison.OrdinalIgnoreCase))
            {
                pseudoFunc = ops.NthLastType(0, 1);
            }
            else if (string.Equals(selector.Class, "only-of-type", StringComparison.OrdinalIgnoreCase))
            {
                var firstOfType = ops.NthType(0, 1);
                var lastOfType = ops.NthLastType(0, 1);
                pseudoFunc = nodes => lastOfType(firstOfType(nodes));
            }
            else if (string.Equals(selector.Class, "hover", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(selector.Class, "active", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(selector.Class, "focus", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(selector.Class, "visited", StringComparison.OrdinalIgnoreCase))
            {
                // Svg.Skia applies CSS during document load without a live DOM/event loop. Treating
                // these as matches would permanently style content as if the user were interacting
                // with it, so the browser-compatible behavior for this renderer is "never matches".
                pseudoFunc = NoMatches();
            }
            else if (string.Equals(selector.Class, "link", StringComparison.OrdinalIgnoreCase))
            {
                // :link is still a pseudo-class filter, so it must narrow the current candidate set
                // instead of widening it to ancestor text containers. Styling then flows through the
                // normal SVG inheritance path from the matched anchor to its text content.
                pseudoFunc = nodes => nodes.Where(IsLinkAnchor);
            }
            else
            {
                if (selector.Class.StartsWith(PseudoClassNames.Not, StringComparison.Ordinal))
                {
                    var sel = selector.Class.Substring(PseudoClassNames.Not.Length + 1, selector.Class.Length - 2 - PseudoClassNames.Not.Length);
                    var parser = new StylesheetParser(true, true, tolerateInvalidValues: true);
                    var styleSheet = parser.Parse(sel);
                    var newSelector = styleSheet.StyleRules.First().Selector;
                    var func = GetFunc(newSelector, ops, ops.Universal());
                    var descendants = ops.Descendant();
                    var func1 = func;
                    func = f => func1(descendants(f));
                    HashSet<SvgElement> notElements = null;

                    pseudoFunc = f => f.Where(e =>
                    {
                        notElements ??= func(f).ToHashSet();
                        return !notElements.Contains(e);
                    });
                }
                else if (TryGetPseudoArgument(selector.Class, "lang", out var languageRange))
                {
                    // :lang() participates in the normal selector pipeline, but the language value
                    // itself comes from inherited xml:lang/lang attributes. Resolve that explicitly
                    // so selectors see the effective language, not just attributes declared on the
                    // leaf element.
                    pseudoFunc = nodes => nodes.Where(element => MatchesLanguage(element, languageRange));
                }
                else if (selector.Class.StartsWith(PseudoClassNames.Root, StringComparison.Ordinal))
                {
                    pseudoFunc = ops.Root();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (inFunc == null)
            {
                return pseudoFunc;
            }

            return f => pseudoFunc(inFunc(f));
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            ComplexSelector selector,
            ExSvgElementOps ops,
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> inFunc)
        {
            List<Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>>> results = new();

            foreach (var it in selector)
            {
                results.Add(GetFunc(it.Selector, ops, null));

                Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> combinatorFunc;
                if (it.Delimiter == Combinator.Child.Delimiter)
                {
                    combinatorFunc = ops.Child();
                }
                else if (it.Delimiter == Combinators.Descendent)
                {
                    combinatorFunc = ops.Descendant();
                }
                else if (it.Delimiter == Combinator.Deep.Delimiter)
                {
                    throw new NotImplementedException();
                }
                else if (it.Delimiter == Combinators.Adjacent)
                {
                    combinatorFunc = ops.Adjacent();
                }
                else if (it.Delimiter == Combinators.Sibling)
                {
                    combinatorFunc = ops.GeneralSibling();
                }
                else if (it.Delimiter == Combinators.Pipe)
                {
                    throw new NotImplementedException();
                }
                else if (it.Delimiter == Combinators.Column)
                {
                    throw new NotImplementedException();
                }
                else if (it.Delimiter == null)
                {
                    combinatorFunc = null;
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (combinatorFunc != null)
                {
                    results.Add(combinatorFunc);
                }
            }

            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> result = inFunc;
            foreach (var it in results)
            {
                if (result == null)
                {
                    result = it;
                }
                else
                {
                    var temp = result;
                    result = f => it(temp(f));
                }
            }

            return result;
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> GetFunc(
            ISelector selector,
            ExSvgElementOps ops,
            Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> inFunc)
        {
            var func = selector switch
            {
                AllSelector allSelector => ops.Universal(),
                AttrAvailableSelector attrAvailableSelector => ops.AttributeExists(attrAvailableSelector.Attribute),
                AttrBeginsSelector attrBeginsSelector => ops.AttributePrefixMatch(attrBeginsSelector.Attribute, attrBeginsSelector.Value),
                AttrContainsSelector attrContainsSelector => ops.AttributeSubstring(attrContainsSelector.Attribute, attrContainsSelector.Value),
                AttrEndsSelector attrEndsSelector => ops.AttributeSuffixMatch(attrEndsSelector.Attribute, attrEndsSelector.Value),
                AttrHyphenSelector attrHyphenSelector => ops.AttributeDashMatch(attrHyphenSelector.Attribute, attrHyphenSelector.Value),
                AttrListSelector attrListSelector => ops.AttributeIncludes(attrListSelector.Attribute, attrListSelector.Value),
                AttrMatchSelector attrMatchSelector => ops.AttributeExact(attrMatchSelector.Attribute, attrMatchSelector.Value),
                AttrNotMatchSelector attrNotMatchSelector => ops.AttributeNotMatch(attrNotMatchSelector.Attribute, attrNotMatchSelector.Value),
                ClassSelector classSelector => ops.Class(classSelector.Class),
                ComplexSelector complexSelector => GetFunc(complexSelector, ops, inFunc),
                CompoundSelector compoundSelector => GetFunc(compoundSelector, ops, inFunc),
                FirstChildSelector firstChildSelector => GetFunc(firstChildSelector, ops),
                LastChildSelector lastChildSelector => GetFunc(lastChildSelector, ops),
                FirstColumnSelector => throw new NotImplementedException(),
                LastColumnSelector => throw new NotImplementedException(),
                FirstTypeSelector firstTypeSelector => GetFunc(firstTypeSelector, ops),
                LastTypeSelector lastTypeSelector => GetFunc(lastTypeSelector, ops),
                ChildSelector => ops.Child(),
                ListSelector listSelector => GetFunc(listSelector, ops, inFunc),
                NamespaceSelector => throw new NotImplementedException(),
                PseudoClassSelector pseudoClassSelector => GetFunc(pseudoClassSelector, ops, inFunc),
                PseudoElementSelector => throw new NotImplementedException(),
                TypeSelector typeSelector => ops.Type(typeSelector.Name),
                UnknownSelector => throw new NotImplementedException(),
                IdSelector idSelector => ops.Id(idSelector.Id),
                PageSelector => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };

            if (inFunc == null)
            {
                return func;
            }

            return f => func(inFunc(f));
        }

        private static Func<IEnumerable<SvgElement>, IEnumerable<SvgElement>> NoMatches()
        {
            return static _ => Enumerable.Empty<SvgElement>();
        }

        private static bool IsLinkAnchor(SvgElement element)
        {
            return element is SvgAnchor anchor && !string.IsNullOrWhiteSpace(anchor.Href);
        }

        private static bool TryGetPseudoArgument(string selectorClass, string pseudoName, out string argument)
        {
            argument = string.Empty;
            var prefix = pseudoName + "(";
            if (!selectorClass.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !selectorClass.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            argument = selectorClass.Substring(prefix.Length, selectorClass.Length - prefix.Length - 1);
            return !string.IsNullOrWhiteSpace(argument);
        }

        private static bool MatchesLanguage(SvgElement element, string languageRange)
        {
            if (string.IsNullOrWhiteSpace(languageRange))
            {
                return false;
            }

            var language = GetEffectiveLanguage(element);
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            // CSS :lang() matches either the exact language tag or one of its subtags, so "en"
            // should match both "en" and "en-US".
            var normalizedRange = languageRange.Trim().ToLowerInvariant();
            var normalizedLanguage = language.Trim().ToLowerInvariant();
            return normalizedLanguage == normalizedRange ||
                   normalizedLanguage.StartsWith(normalizedRange + "-", StringComparison.Ordinal);
        }

        private static string GetEffectiveLanguage(SvgElement element)
        {
            for (var current = element; current != null; current = current.Parent)
            {
                if (TryGetLanguage(current, out var language))
                {
                    return language;
                }
            }

            return string.Empty;
        }

        private static bool TryGetLanguage(SvgElement element, out string language)
        {
            if (element.TryGetAttribute("xml:lang", out language) && !string.IsNullOrWhiteSpace(language))
            {
                return true;
            }

            if (element.TryGetAttribute("lang", out language) && !string.IsNullOrWhiteSpace(language))
            {
                return true;
            }

            if (element.CustomAttributes.TryGetValue($"{XmlNamespace}:lang", out language) &&
                !string.IsNullOrWhiteSpace(language))
            {
                return true;
            }

            // Older parser paths and foreign-namespace handling can surface xml:lang under a raw
            // qualified-name key instead of the normalized lookup above. Probe those fallbacks so
            // :lang() still sees the same effective language a browser would inherit.
            foreach (var attribute in element.CustomAttributes)
            {
                if (attribute.Key.EndsWith(":lang", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    language = attribute.Value;
                    return true;
                }
            }

            language = string.Empty;
            return false;
        }

#if NETSTANDARD2_0 || NET462_OR_GREATER
        private static HashSet<T> ToHashSet<T>(this IEnumerable<T> enumerable)
        {
            var result = new HashSet<T>();
            foreach (var it in enumerable)
            {
                result.Add(it);
            }

            return result;
        }
#endif

        public static int GetSpecificity(this ISelector selector)
        {
            var specificity = 0x0;
            specificity |= (1 << 12) * selector.Specificity.Ids;
            specificity |= (1 << 8) * selector.Specificity.Classes;
            specificity |= (1 << 4) * selector.Specificity.Tags;
            return specificity;
        }
    }
}
