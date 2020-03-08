using System;
using Xml;

namespace Svg
{
    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement,
        ISvgCommonAttributes,
        ISvgTestsAttributes,
        ISvgResourcesAttributes,
        ISvgXLinkAttributes,
        ISvgAnimationEventAttributes,
        ISvgAnimationTimingAttributes,
        ISvgAnimationValueAttributes,
        ISvgAnimationAdditionAttributes
    {
        // ISvgAnimationValueAttributes

        [Attribute("calcMode", SvgNamespace)]
        public override string? CalcMode
        {
            get => this.GetAttribute("calcMode", false, "paced");
            set => this.SetAttribute("calcMode", value);
        }

        // SvgAnimateMotion

        [Attribute("path", SvgNamespace)]
        public string? Path
        {
            get => this.GetAttribute("path", false, null);
            set => this.SetAttribute("path", value);
        }

        [Attribute("keyPoints", SvgNamespace)]
        public string? KeyPoints
        {
            get => this.GetAttribute("keyPoints", false, null);
            set => this.SetAttribute("keyPoints", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate", false, "0");
            set => this.SetAttribute("rotate", value);
        }

        [Attribute("origin", SvgNamespace)]
        public string? Origin
        {
            get => this.GetAttribute("origin", false, "default"); // TODO:
            set => this.SetAttribute("origin", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                // ISvgAnimationValueAttributes
                case "calcMode":
                    CalcMode = value;
                    break;
                // SvgAnimateMotion
                case "path":
                    Path = value;
                    break;
                case "keyPoints":
                    KeyPoints = value;
                    break;
                case "rotate":
                    Rotate = value;
                    break;
                case "origin":
                    Origin = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (Path != null)
            {
                Console.WriteLine($"{indent}{nameof(Path)}='{Path}'");
            }
            if (KeyPoints != null)
            {
                Console.WriteLine($"{indent}{nameof(KeyPoints)}='{KeyPoints}'");
            }
            if (Rotate != null)
            {
                Console.WriteLine($"{indent}{nameof(Rotate)}='{Rotate}'");
            }
            if (Origin != null)
            {
                Console.WriteLine($"{indent}{nameof(Origin)}='{Origin}'");
            }
        }
    }
}
