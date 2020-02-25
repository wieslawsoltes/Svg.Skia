using System.Collections.Generic;

namespace Xml
{
    public abstract class Element : IElement
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public List<Element> Children { get; set; }
        public Dictionary<string, string?> Attributes { get; set; }
        public IElement? Parent { get; set; }

        public Element()
        {
            Name = string.Empty;
            Text = string.Empty;
            Children = new List<Element>();
            Attributes = new Dictionary<string, string?>();
            Parent = null;
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
    }
}
