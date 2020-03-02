using System.Collections.Generic;

namespace Xml
{
    public interface IElement
    {
        string Tag { get; set; }
        string Content { get; set; }
        List<Element> Children { get; set; }
        Dictionary<string, string?> Attributes { get; set; }
        Dictionary<string, SortedDictionary<int, string>> Styles { get; set; }
        IElement? Parent { get; set; }
        void SetPropertyValue(string key, string? value);
        string? GetAttribute(string key);
        void SetAttribute(string key, string? value);
        bool ContainsAttribute(string key);
        bool TryGetAttribute(string key, out string? value);
        void AddStyle(string name, string value, int specificity);
    }
}
