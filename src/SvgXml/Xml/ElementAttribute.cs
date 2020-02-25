using System;

namespace Xml
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ElementAttribute : Attribute
    {
        public string Name { get; private set; }

        public ElementAttribute(string name)
        {
            Name = name;
        }
    }
}
