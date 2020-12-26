using SvgXml.Xml;
using SvgXml.Xml.Attributes;

namespace SvgXml.Svg
{
    public abstract class SvgElement : Element, IId
    {
        public const string SvgNamespace = "http://www.w3.org/2000/svg";

        public const string XLinkNamespace = "http://www.w3.org/1999/xlink";

        public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

        // ISvgCommonAttributes

        [Attribute("id", SvgNamespace)]
        public virtual string? Id
        {
            get => this.GetAttribute("id", false, null);
            set => this.SetAttribute("id", value);
        }

        [Attribute("base", XmlNamespace)]
        public virtual string? Base
        {
            get => this.GetAttribute("base", false, null);
            set => this.SetAttribute("base", value);
        }

        [Attribute("lang", XmlNamespace)]
        public virtual string? Lang
        {
            get => this.GetAttribute("lang", false, null);
            set => this.SetAttribute("lang", value);
        }

        [Attribute("space", XmlNamespace)]
        public virtual string? Space
        {
            get => this.GetAttribute("space", false, "default");
            set => this.SetAttribute("space", value);
        }

        // ISvgResourcesAttributes

        [Attribute("externalResourcesRequired", SvgNamespace)]
        public virtual string? ExternalResourcesRequired
        {
            get => this.GetAttribute("externalResourcesRequired", false, "false");
            set => this.SetAttribute("externalResourcesRequired", value);
        }

        // ISvgTestsAttributes

        [Attribute("requiredFeatures", SvgNamespace)]
        public virtual string? RequiredFeatures
        {
            get => this.GetAttribute("requiredFeatures", false, null);
            set => this.SetAttribute("requiredFeatures", value);
        }

        [Attribute("requiredExtensions", SvgNamespace)]
        public virtual string? RequiredExtensions
        {
            get => this.GetAttribute("requiredExtensions", false, null);
            set => this.SetAttribute("requiredExtensions", value);
        }

        [Attribute("systemLanguage", SvgNamespace)]
        public virtual string? SystemLanguage
        {
            get => this.GetAttribute("systemLanguage", false, null);
            set => this.SetAttribute("systemLanguage", value);
        }

        // ISvgXLinkAttributes

        [Attribute("href", XLinkNamespace)]
        public virtual string? Href
        {
            get => this.GetAttribute("href", false, null);
            set => this.SetAttribute("href", value);
        }

        [Attribute("show", XLinkNamespace)]
        public virtual string? Show
        {
            get => this.GetAttribute("show", false, null);
            set => this.SetAttribute("show", value);
        }

        [Attribute("actuate", XLinkNamespace)]
        public virtual string? Actuate
        {
            get => this.GetAttribute("actuate", false, null);
            set => this.SetAttribute("actuate", value);
        }

        [Attribute("type", XLinkNamespace)]
        public virtual string? Type
        {
            get => this.GetAttribute("type", false, null);
            set => this.SetAttribute("type", value);
        }

        [Attribute("role", XLinkNamespace)]
        public virtual string? Role
        {
            get => this.GetAttribute("role", false, null);
            set => this.SetAttribute("role", value);
        }

        [Attribute("arcrole", XLinkNamespace)]
        public virtual string? Arcrole
        {
            get => this.GetAttribute("arcrole", false, null);
            set => this.SetAttribute("arcrole", value);
        }

        [Attribute("title", XLinkNamespace)]
        public virtual string? Title
        {
            get => this.GetAttribute("title", false, null);
            set => this.SetAttribute("title", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            switch (key)
            {
                // ISvgCommonAttributes
                case "id":
                    Id = value;
                    break;
                case "base":
                    Base = value;
                    break;
                case "lang":
                    Lang = value;
                    break;
                case "space":
                    Space = value;
                    break;
                // ISvgResourcesAttributes
                case "externalResourcesRequired":
                    ExternalResourcesRequired = value;
                    break;
                // ISvgTestsAttributes
                case "requiredFeatures":
                    RequiredFeatures = value;
                    break;
                case "requiredExtensions":
                    RequiredExtensions = value;
                    break;
                case "systemLanguage":
                    SystemLanguage = value;
                    break;
                // ISvgXLinkAttributes
                case "href":
                    Href = value;
                    break;
                case "show":
                    Show = value;
                    break;
                case "actuate":
                    Actuate = value;
                    break;
                case "type":
                    Type = value;
                    break;
                case "role":
                    Role = value;
                    break;
                case "arcrole":
                    Arcrole = value;
                    break;
                case "title":
                    Title = value;
                    break;
            }
        }
    }
}
