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
        [Attribute("path", SvgNamespace)]
        public string? Path
        {
            get => this.GetAttribute("path");
            set => this.SetAttribute("path", value);
        }

        [Attribute("keyPoints", SvgNamespace)]
        public string? KeyPoints
        {
            get => this.GetAttribute("keyPoints");
            set => this.SetAttribute("keyPoints", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => this.GetAttribute("rotate");
            set => this.SetAttribute("rotate", value);
        }

        [Attribute("origin", SvgNamespace)]
        public string? Origin
        {
            get => this.GetAttribute("origin");
            set => this.SetAttribute("origin", value);
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
