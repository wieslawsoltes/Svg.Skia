using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace Xml
{
    public static class XmlLoader
    {
        public static Element? Read(XmlReader reader, IElementFactory elementFactory)
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
                            string localName = reader.LocalName;
                            Element element;

                            if (string.IsNullOrEmpty(reader.NamespaceURI) || elementFactory.Namespaces.Contains(reader.NamespaceURI))
                            {
                                var parent = stack.Count > 0 ? stack.Peek() : null;

                                element = elementFactory.Create(localName, parent);
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

                                var children = parent != null ? parent.Children : elements;
                                children.Add(element);
                            }
                            else
                            {
                                element = new UnknownElement() { Tag = localName, Parent = null };
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
                        {
                            var element = stack.Peek();
                            var content = new ContentElement() { Content = reader.Value };
                            element.Children.Add(content);
                        }
                        break;
                    case XmlNodeType.CDATA:
                        {
                            var element = stack.Peek();
                            var content = new ContentElement() { Content = reader.Value };
                            element.Children.Add(content);
                        }
                        break;
                    case XmlNodeType.EntityReference:
                        {
                            reader.ResolveEntity();
                            var element = stack.Peek();
                            var content = new ContentElement() { Content = reader.Value };
                            element.Children.Add(content);
                        }
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
                            var element = stack.Pop();

                            foreach (var child in element.Children)
                            {
                                if (child is ContentElement content)
                                {
                                    element.Content += content.Content;
                                }
                            }
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
    }
}
