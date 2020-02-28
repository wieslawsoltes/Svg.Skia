using System.Collections.Generic;

namespace Xml
{
    public interface IElementFactory
    {
        ISet<string> Namespaces { get; }
        Element Create(string name, IElement? parent);
    }
}
