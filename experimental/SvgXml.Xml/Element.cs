using System.Collections.Generic;
using System.Linq;

namespace Xml
{
    public abstract class Element : IElement
    {
        public const int StyleSpecificity_PresAttribute = 0;
        public const int StyleSpecificity_InlineStyle = 1 << 16;

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

        public abstract void SetPropertyValue(string key, string? value);

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

        public void FlushStyles(bool children = false)
        {
            foreach (var style in Styles)
            {
                SetPropertyValue(style.Key, style.Value.Last().Value);
            }

            if (children)
            {
                foreach (var child in Children)
                {
                    child.FlushStyles(children);
                }
            }
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
