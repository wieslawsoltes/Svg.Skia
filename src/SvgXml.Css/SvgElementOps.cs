using System;
using System.Collections.Generic;
using System.Linq;
using Fizzler;
using Svg;
using Xml;

namespace SvgXml.Css
{
    public class ElementOps : IElementOps<Element>
    {
        private readonly IElementFactory _elementFactory;

        public ElementOps(IElementFactory elementFactory)
        {
            _elementFactory = elementFactory;
        }

        public Selector<Element> Type(NamespacePrefix prefix, string name)
        {
            if (_elementFactory.Types.TryGetValue(name, out var type))
            {
                return nodes => nodes.Where(n => n.GetType() == type);
            }
            return nodes => Enumerable.Empty<Element>();
        }

        public Selector<Element> Universal(NamespacePrefix prefix)
        {
            return nodes => nodes;
        }

        public Selector<Element> Id(string id)
        {
            return nodes => nodes.Where(n =>
            {
                if (n is IId iid)
                {
                    return iid.Id == id;
                }
                return false;
            });
        }

        public Selector<Element> Class(string clazz)
        {
            return AttributeIncludes(NamespacePrefix.None, "class", clazz);
        }

        public Selector<Element> AttributeExists(NamespacePrefix prefix, string name)
        {
            return nodes => nodes.Where(n => n.ContainsAttribute(name));
        }

        public Selector<Element> AttributeExact(NamespacePrefix prefix, string name, string value)
        {
            return nodes => nodes.Where(n =>
            {
                return (n.TryGetAttribute(name, out var val) && val == value);
            });
        }

        public Selector<Element> AttributeIncludes(NamespacePrefix prefix, string name, string value)
        {
            return nodes => nodes.Where(n =>
            {
                return (n.TryGetAttribute(name, out var val) && val?.Split(' ').Contains(value) == true);
            });
        }

        public Selector<Element> AttributeDashMatch(NamespacePrefix prefix, string name, string value)
        {
            return string.IsNullOrEmpty(value)
                 ? (Selector<Element>)(nodes => Enumerable.Empty<Element>())
                 : (nodes => nodes.Where(n =>
                    {
                        return (n.TryGetAttribute(name, out var val) && val?.Split('-').Contains(value) == true);
                    }));
        }

        public Selector<Element> AttributePrefixMatch(NamespacePrefix prefix, string name, string value)
        {
            return string.IsNullOrEmpty(value)
                 ? (Selector<Element>)(nodes => Enumerable.Empty<Element>())
                 : (nodes => nodes.Where(n =>
                     {
                         return (n.TryGetAttribute(name, out var val) && val?.StartsWith(value) == true);
                     }));
        }

        public Selector<Element> AttributeSuffixMatch(NamespacePrefix prefix, string name, string value)
        {
            return string.IsNullOrEmpty(value)
                 ? (Selector<Element>)(nodes => Enumerable.Empty<Element>())
                 : (nodes => nodes.Where(n =>
                 {
                     return (n.TryGetAttribute(name, out var val) && val?.EndsWith(value) == true);
                 }));
        }

        public Selector<Element> AttributeSubstring(NamespacePrefix prefix, string name, string value)
        {
            return string.IsNullOrEmpty(value)
                 ? (Selector<Element>)(nodes => Enumerable.Empty<Element>())
                 : (nodes => nodes.Where(n =>
                 {
                     return (n.TryGetAttribute(name, out var val) && val?.Contains(value) == true);
                 }));
        }

        public Selector<Element> FirstChild()
        {
            return nodes => nodes.Where(n => n.Parent == null || n.Parent.Children.First() == n);
        }

        public Selector<Element> LastChild()
        {
            return nodes => nodes.Where(n => n.Parent == null || n.Parent.Children.Last() == n);
        }

        private IEnumerable<T> GetByIds<T>(IList<T> items, IEnumerable<int> indices)
        {
            foreach (var i in indices)
            {
                if (i >= 0 && i < items.Count)
                    yield return items[i];
            }
        }

        public Selector<Element> NthChild(int a, int b)
        {
            return nodes => nodes.Where(n => n.Parent != null && GetByIds(n.Parent.Children, (from i in Enumerable.Range(0, n.Parent.Children.Count / a) select a * i + b)).Contains(n));
        }

        public Selector<Element> OnlyChild()
        {
            return nodes => nodes.Where(n => n.Parent == null || n.Parent.Children.Count == 1);
        }

        public Selector<Element> Empty()
        {
            return nodes => nodes.Where(n => n.Children.Count == 0);
        }

        public Selector<Element> Child()
        {
            return nodes => nodes.SelectMany(n => n.Children);
        }

        public Selector<Element> Descendant()
        {
            return nodes => nodes.SelectMany(n => Descendants(n));
        }

        private IEnumerable<Element> Descendants(Element elememnt)
        {
            foreach (var child in elememnt.Children)
            {
                yield return child;
                foreach (var descendant in child.Descendants())
                {
                    yield return descendant;
                }
            }
        }

        public Selector<Element> Adjacent()
        {
            return nodes => nodes.SelectMany(n => ElementsAfterSelf(n).Take(1));
        }

        public Selector<Element> GeneralSibling()
        {
            return nodes => nodes.SelectMany(n => ElementsAfterSelf(n));
        }

        private IEnumerable<Element> ElementsAfterSelf(Element self)
        {
            return (self.Parent == null ? Enumerable.Empty<Element>() : self.Parent.Children.Skip(self.Parent.Children.IndexOf(self) + 1));
        }

        public Selector<Element> NthLastChild(int a, int b)
        {
            throw new NotImplementedException();
        }
    }
}
