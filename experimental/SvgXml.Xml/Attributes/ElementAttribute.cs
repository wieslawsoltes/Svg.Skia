using System;

namespace SvgXml.Xml.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ElementAttribute : Attribute
    {
        public string Name { get; private set; }

        public ElementAttribute(string name)
        {
            Name = name;
        }
    }
}
