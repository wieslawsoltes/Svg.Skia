using System.Collections.Generic;

namespace Xml
{
    public interface IElement
    {
        string Tag { get; set; }
        string Content { get; set; }
        List<Element> Children { get; set; }
        Dictionary<string, string?> Attributes { get; set; }
        IElement? Parent { get; set; }
        string? GetAttribute(string key);
        void SetAttribute(string key, string? value);
    }
}
