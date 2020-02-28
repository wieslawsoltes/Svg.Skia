using System;
using Xml;

namespace Svg
{
    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement,
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
            get => GetAttribute("path");
            set => SetAttribute("path", value);
        }

        [Attribute("keyPoints", SvgNamespace)]
        public string? KeyPoints
        {
            get => GetAttribute("keyPoints");
            set => SetAttribute("keyPoints", value);
        }

        [Attribute("rotate", SvgNamespace)]
        public string? Rotate
        {
            get => GetAttribute("rotate");
            set => SetAttribute("rotate", value);
        }

        [Attribute("origin", SvgNamespace)]
        public string? Origin
        {
            get => GetAttribute("origin");
            set => SetAttribute("origin", value);
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
