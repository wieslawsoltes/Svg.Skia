using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Xml
{
    public static class XmlLoader
    {
        public static XmlReaderSettings s_settings = new XmlReaderSettings()
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

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
                            string elementName = reader.LocalName;
                            Element element;

                            if (string.IsNullOrEmpty(reader.NamespaceURI) || elementFactory.Namespaces.Contains(reader.NamespaceURI))
                            {
                                var parent = stack.Count > 0 ? stack.Peek() : null;

                                element = elementFactory.Create(elementName, parent);
                                element.Parent = parent;

                                if (reader.MoveToFirstAttribute())
                                {
                                    do
                                    {
                                        if (string.IsNullOrEmpty(reader.NamespaceURI) || elementFactory.Namespaces.Contains(reader.NamespaceURI))
                                        {
                                            string attributeName = reader.LocalName;
                                            element.Attributes.Add(attributeName, reader.Value);
                                        }
                                    }
                                    while (reader.MoveToNextAttribute());
                                    reader.MoveToElement();
                                }

                                var nodes = parent != null ? parent.Children : elements;
                                nodes.Add(element);
                            }
                            else
                            {
                                element = new UnknownElement() { Name = elementName, Parent = null };
                            }

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
                    case XmlNodeType.EndElement:
                        {
                            stack.Pop();
                        }
                        break;
                    case XmlNodeType.EndEntity:
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
            var reader = XmlReader.Create(stream, s_settings);
            var element = Open(reader, elementFactory);
            return element;
        }

        public static Element? Open(string path, IElementFactory elementFactory)
        {
            using var stream = File.OpenRead(path);
            return Open(stream, elementFactory);
        }
    }
}
