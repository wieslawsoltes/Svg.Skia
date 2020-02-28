using System;

namespace Xml
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
    public class AttributeAttribute : Attribute
    {
        public string Name { get; private set; }
        public string NameSpace { get; private set; }

        public AttributeAttribute(string name, string nameSpace)
        {
            Name = name;
            NameSpace = nameSpace;
        }
    }
}
