using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

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

    public interface IElementFactory
    {
        public Element Create(string name);
    }

    public abstract class Element
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public List<Element> Children { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public Element()
        {
            Name = string.Empty;
            Text = string.Empty;
            Children = new List<Element>();
            Attributes = new Dictionary<string, string>();
        }

        public static Element? Open(XmlReader reader, IElementFactory elementFactory)
        {
            var elements = new List<Element>();
            var stack = new Stack<Element>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Element:
                        {
                            var name = reader.LocalName;
                            var element = elementFactory.Create(name);

                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    element.Attributes.Add(reader.Name, reader.Value);
                                }
                                while (reader.MoveToNextAttribute());
                                reader.MoveToElement();
                            }

                            var nodes = stack.Count > 0 ? stack.Peek().Children : elements;
                            nodes.Add(element);

                            if (!reader.IsEmptyElement)
                            {
                                stack.Push(element);
                            }
                        }
                        break;
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.EndElement:
                        {
                            stack.Pop();
                        }
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        break;
                }
            }
            if (elements.Count == 1)
            {
                return elements[0];
            }
            return null;
        }

        public static Element? Open(Stream stream, IElementFactory elementFactory)
        {
            var settings = new XmlReaderSettings()
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };
            var reader = XmlReader.Create(stream, settings);
            var element = Open(reader, elementFactory);
            return element;
        }

        public static Element? Open(string path, IElementFactory elementFactory)
        {
            using var stream = File.OpenRead(path);
            return Open(stream, elementFactory);
        }
    }

    public class UnknownElement : Element
    {
    }
}
