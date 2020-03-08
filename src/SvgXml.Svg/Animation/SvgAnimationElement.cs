using System;
using Xml;

namespace Svg
{
    public abstract class SvgAnimationElement : SvgStylableElement
    {
        // ISvgAnimationAdditionAttributes

        [Attribute("additive", SvgNamespace)]
        public virtual string? Additive
        {
            get => this.GetAttribute("additive", false, "replace");
            set => this.SetAttribute("additive", value);
        }

        [Attribute("accumulate", SvgNamespace)]
        public virtual string? Accumulate
        {
            get => this.GetAttribute("accumulate", false, "none");
            set => this.SetAttribute("accumulate", value);
        }

        // ISvgAnimationAttributeTargetAttributes

        [Attribute("attributeType", SvgNamespace)]
        public virtual string? AttributeType
        {
            get => this.GetAttribute("attributeType", false, "auto");
            set => this.SetAttribute("attributeType", value);
        }

        [Attribute("attributeName", SvgNamespace)]
        public virtual string? AttributeName
        {
            get => this.GetAttribute("attributeName", false, null);
            set => this.SetAttribute("attributeName", value);
        }

        // ISvgAnimationEventAttributes

        [Attribute("onbegin", SvgNamespace)]
        public virtual string? OnBegin
        {
            get => this.GetAttribute("onbegin", false, null);
            set => this.SetAttribute("onbegin", value);
        }

        [Attribute("onend", SvgNamespace)]
        public virtual string? OnEnd
        {
            get => this.GetAttribute("onend", false, null);
            set => this.SetAttribute("onend", value);
        }

        [Attribute("onrepeat", SvgNamespace)]
        public virtual string? OnRepeat
        {
            get => this.GetAttribute("onrepeat", false, null);
            set => this.SetAttribute("onrepeat", value);
        }

        [Attribute("onload", SvgNamespace)]
        public virtual string? OnLoad
        {
            get => this.GetAttribute("onload", false, null);
            set => this.SetAttribute("onload", value);
        }

        // ISvgAnimationTimingAttributes

        [Attribute("begin", SvgNamespace)]
        public virtual string? Begin
        {
            get => this.GetAttribute("begin", false, null); // TODO:
            set => this.SetAttribute("begin", value);
        }

        [Attribute("dur", SvgNamespace)]
        public virtual string? Dur
        {
            get => this.GetAttribute("dur", false, "indefinite");
            set => this.SetAttribute("dur", value);
        }

        [Attribute("end", SvgNamespace)]
        public virtual string? End
        {
            get => this.GetAttribute("end", false, null); // TODO:
            set => this.SetAttribute("end", value);
        }

        [Attribute("min", SvgNamespace)]
        public virtual string? Min
        {
            get => this.GetAttribute("min", false, "0");
            set => this.SetAttribute("min", value);
        }

        [Attribute("max", SvgNamespace)]
        public virtual string? Max
        {
            get => this.GetAttribute("max", false, null);
            set => this.SetAttribute("max", value);
        }

        [Attribute("restart", SvgNamespace)]
        public virtual string? Restart
        {
            get => this.GetAttribute("restart", false, "always");
            set => this.SetAttribute("restart", value);
        }

        [Attribute("repeatCount", SvgNamespace)]
        public virtual string? RepeatCount
        {
            get => this.GetAttribute("repeatCount", false, null);
            set => this.SetAttribute("repeatCount", value);
        }

        [Attribute("repeatDur", SvgNamespace)]
        public virtual string? RepeatDur
        {
            get => this.GetAttribute("repeatDur", false, null);
            set => this.SetAttribute("repeatDur", value);
        }

        [Attribute("fill", SvgNamespace)]
        public override string? Fill
        {
            get => this.GetAttribute("fill", false, "remove");
            set => this.SetAttribute("fill", value);
        }

        // ISvgAnimationValueAttributes

        [Attribute("calcMode", SvgNamespace)]
        public virtual string? CalcMode
        {
            get => this.GetAttribute("calcMode", false, "linear");
            set => this.SetAttribute("calcMode", value);
        }

        [Attribute("values", SvgNamespace)]
        public virtual string? Values
        {
            get => this.GetAttribute("values", false, null);
            set => this.SetAttribute("values", value);
        }

        [Attribute("keyTimes", SvgNamespace)]
        public virtual string? KeyTimes
        {
            get => this.GetAttribute("keyTimes", false, null);
            set => this.SetAttribute("keyTimes", value);
        }

        [Attribute("keySplines", SvgNamespace)]
        public virtual string? KeySplines
        {
            get => this.GetAttribute("keySplines", false, null);
            set => this.SetAttribute("keySplines", value);
        }

        [Attribute("from", SvgNamespace)]
        public virtual string? From
        {
            get => this.GetAttribute("from", false, null);
            set => this.SetAttribute("from", value);
        }

        [Attribute("to", SvgNamespace)]
        public virtual string? To
        {
            get => this.GetAttribute("to", false, null);
            set => this.SetAttribute("to", value);
        }

        [Attribute("by", SvgNamespace)]
        public virtual string? By
        {
            get => this.GetAttribute("by", false, null);
            set => this.SetAttribute("by", value);
        }

        public override void SetPropertyValue(string key, string? value)
        {
            base.SetPropertyValue(key, value);
            switch (key)
            {
                // ISvgAnimationAdditionAttributes
                case "additive":
                    Additive = value;
                    break;
                case "accumulate":
                    Accumulate = value;
                    break;
                // ISvgAnimationAttributeTargetAttributes
                case "attributeType":
                    AttributeType = value;
                    break;
                case "attributeName":
                    AttributeName = value;
                    break;
                // ISvgAnimationEventAttributes
                case "onbegin":
                    OnBegin = value;
                    break;
                case "onend":
                    OnEnd = value;
                    break;
                case "onrepeat":
                    OnRepeat = value;
                    break;
                case "onload":
                    OnLoad = value;
                    break;
                // ISvgAnimationTimingAttributes
                case "begin":
                    Begin = value;
                    break;
                case "dur":
                    Dur = value;
                    break;
                case "end":
                    End = value;
                    break;
                case "min":
                    Min = value;
                    break;
                case "max":
                    Max = value;
                    break;
                case "restart":
                    Restart = value;
                    break;
                case "repeatCount":
                    RepeatCount = value;
                    break;
                case "repeatDur":
                    RepeatDur = value;
                    break;
                case "fill":
                    Fill = value;
                    break;
                // ISvgAnimationValueAttributes
                case "calcMode":
                    CalcMode = value;
                    break;
                case "values":
                    Values = value;
                    break;
                case "keyTimes":
                    KeyTimes = value;
                    break;
                case "keySplines":
                    KeySplines = value;
                    break;
                case "from":
                    From = value;
                    break;
                case "to":
                    To = value;
                    break;
                case "by":
                    By = value;
                    break;
            }
        }

        public override void Print(Action<string> write, string indent)
        {
            base.Print(write, indent);

            if (this is ISvgAnimationAdditionAttributes svgAnimationAdditionAttributes)
            {
                PrintAnimationAdditionAttributes(svgAnimationAdditionAttributes, write, indent);
            }
            if (this is ISvgAnimationAttributeTargetAttributes svgAnimationAttributeTargetAttributes)
            {
                PrintAnimationAttributeTargetAttributes(svgAnimationAttributeTargetAttributes, write, indent);
            }
            if (this is ISvgAnimationEventAttributes svgAnimationEventAttributes)
            {
                PrintAnimationEventAttributes(svgAnimationEventAttributes, write, indent);
            }
            if (this is ISvgAnimationTimingAttributes svgAnimationTimingAttributes)
            {
                PrintAnimationTimingAttributes(svgAnimationTimingAttributes, write, indent);
            }
            if (this is ISvgAnimationValueAttributes svgAnimationValueAttributes)
            {
                PrintAnimationValueAttributes(svgAnimationValueAttributes, write, indent);
            }
        }
    }
}
