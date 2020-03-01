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

    public abstract class Element : IElement
    {
        internal const int StyleSpecificity_PresAttribute = 0;
        internal const int StyleSpecificity_InlineStyle = 1 << 16;

        public string Tag { get; set; }
        public string Content { get; set; }
        public List<Element> Children { get; set; }
        public Dictionary<string, string?> Attributes { get; set; }
        public Dictionary<string, SortedDictionary<int, string>> Styles { get; set; }
        public IElement? Parent { get; set; }

        public Element()
        {
            Tag = string.Empty;
            Content = string.Empty;
            Children = new List<Element>();
            Attributes = new Dictionary<string, string?>();
            Styles = new Dictionary<string, SortedDictionary<int, string>>();
            Parent = null;
        }

        //public abstract void SetPropertyValue(string key, string? value);

        public void SetPropertyValue(string key, string? value)
        {
            // TODO: make abstract method
        }

        public string? GetAttribute(string key)
        {
            if (Attributes.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        public void SetAttribute(string key, string? value)
        {
            Attributes[key] = value;
        }

        public bool ContainsAttribute(string key)
        {
            if (Attributes.ContainsKey(key))
            {
                return true;
            }

            if (Styles.TryGetValue(key, out var rules) && (rules.ContainsKey(StyleSpecificity_InlineStyle) || rules.ContainsKey(StyleSpecificity_PresAttribute)))
            {
                return true;
            }

            return false;
        }

        public bool TryGetAttribute(string key, out string? value)
        {
            if (Attributes.TryGetValue(key, out value))
            {
                return true;
            }

            if (Styles.TryGetValue(key, out var rules))
            {
                if (rules.TryGetValue(StyleSpecificity_InlineStyle, out value))
                {
                    return true;
                }

                if (rules.TryGetValue(StyleSpecificity_PresAttribute, out value))
                {
                    return true;
                }
            }

            return false;
        }

        public void AddStyle(string name, string value, int specificity)
        {
            if (!Styles.TryGetValue(name, out var rules))
            {
                rules = new SortedDictionary<int, string>();
                Styles[name] = rules;
            }

            while (rules.ContainsKey(specificity))
            {
                ++specificity;
            }

            rules[specificity] = value;
        }

        public IEnumerable<Element> Descendants()
        {
            return this.AsEnumerable().Descendants();
        }

        private IEnumerable<Element> AsEnumerable()
        {
            yield return this;
        }
    }
}
