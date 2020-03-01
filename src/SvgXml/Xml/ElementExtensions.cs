using System;
using System.Collections.Generic;
using System.Linq;

namespace Xml
{
    public static class ElementExtensions
    {
        public static string? GetAttribute(this IElement element, string key, bool inherited, string? defaultValue)
        {
            bool inherit = false;

            if (element.Attributes.TryGetValue(key, out var value))
            {
                inherit = string.Equals(value?.ToString(), "inherit", StringComparison.OrdinalIgnoreCase);
                if (!inherit)
                {
                    return value;
                }
            }

            if (inherited || inherit)
            {
                var parentValue = element.Parent?.GetAttribute(key, inherited, default);
                if (parentValue != null)
                {
                    return parentValue;
                }
            }

            return defaultValue;
        }

        public static IEnumerable<Element> Descendants<T>(this IEnumerable<T> source) where T : Element
        {
            if (source == null)
                throw new ArgumentNullException("source");

            return GetDescendants<T>(source, false);
        }

        private static IEnumerable<Element> GetAncestors<T>(IEnumerable<T> source, bool self) where T : Element
        {
            foreach (var start in source)
            {
                if (start != null)
                {
                    for (var element = (self ? start : start.Parent) as Element; element != null; element = (element.Parent as Element))
                    {
                        yield return element;
                    }
                }
            }
            yield break;
        }

        private static IEnumerable<Element> GetDescendants<T>(IEnumerable<T> source, bool self) where T : Element
        {
            foreach (var top in source)
            {
                if (top == null)
                    continue;

                if (self)
                    yield return top;

                var elements = new Stack<Element>((top.Children as IEnumerable<Element>).Reverse());
                while (elements.Count > 0)
                {
                    var element = elements.Pop();
                    yield return element;
                    foreach (var e in (element.Children as IEnumerable<Element>).Reverse())
                        elements.Push(e);
                }
            }
            yield break;
        }
    }
}
