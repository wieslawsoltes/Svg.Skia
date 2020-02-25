using System;

namespace Xml
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Event)]
    public class AttributeAttribute : Attribute
    {
        public string Name { get; private set; }

        public AttributeAttribute(string name)
        {
            Name = name;
        }
    }
}
