using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Svg;

public abstract partial class SvgAnimationElement
{
    protected override void WriteAttributes(XmlWriter writer)
    {
        if (!Attributes.ContainsKey("fill"))
        {
            base.WriteAttributes(writer);
            return;
        }

        // The inherited writer treats every attribute named fill as a CSS paint.
        // SMIL fill controls timing and must be written as an XML attribute. Buffer
        // only this element's attributes so serialization never mutates the live DOM.
        var text = new StringBuilder();
        using (var attributes = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            attributes.WriteStartElement("animation", SvgNamespaces.SvgNamespace);
            base.WriteAttributes(attributes);
            attributes.WriteEndElement();
        }
        foreach (var attribute in XElement.Parse(text.ToString()).Attributes())
        {
            if (attribute.Name.LocalName == "fill" && attribute.Name.NamespaceName.Length == 0)
                continue;
            if (attribute.Name.LocalName == "style" && attribute.Name.NamespaceName.Length == 0)
            {
                var style = Regex.Replace(attribute.Value, @"(^|;)\s*fill\s*:\s*(?:freeze|remove)\s*(?=;|$)", "$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim(';', ' ', '\t', '\r', '\n');
                if (style.Length > 0)
                    writer.WriteAttributeString("style", style);
            }
            else if (attribute.IsNamespaceDeclaration)
            {
                if (attribute.Name.LocalName != "xmlns" || attribute.Value != SvgNamespaces.SvgNamespace || writer.LookupPrefix(SvgNamespaces.SvgNamespace) is null)
                    writer.WriteAttributeString(attribute.Name.LocalName == "xmlns" ? null : "xmlns", attribute.Name.LocalName, "http://www.w3.org/2000/xmlns/", attribute.Value);
            }
            else
                writer.WriteAttributeString(attribute.Name.LocalName, attribute.Name.NamespaceName, attribute.Value);
        }
        writer.WriteAttributeString("fill", AnimationFill == SvgAnimationFill.Freeze ? "freeze" : "remove");
    }
}
