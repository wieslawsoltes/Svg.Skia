using System;
using System.ComponentModel;
using Svg.Pathing;

namespace Svg
{
    public abstract partial class SvgAnimationElement : SvgElement
    {
        [SvgAttribute("href", SvgAttributeAttribute.XLinkNamespace)]
        public virtual Uri ReferencedElement
        {
            get { return GetAttribute<Uri>("href", false); }
            set { Attributes["href"] = value; }
        }

        public virtual SvgElement TargetElement
        {
            get
            {
                if (ReferencedElement != null)
                {
                    return OwnerDocument?.IdManager.GetElementById(ReferencedElement);
                }

                return Parent;
            }
        }

        [SvgAttribute("requiredFeatures")]
        public virtual string RequiredFeatures
        {
            get { return GetAttribute<string>("requiredFeatures", false); }
            set { Attributes["requiredFeatures"] = value; }
        }

        [SvgAttribute("requiredExtensions")]
        public virtual string RequiredExtensions
        {
            get { return GetAttribute<string>("requiredExtensions", false); }
            set { Attributes["requiredExtensions"] = value; }
        }

        [SvgAttribute("systemLanguage")]
        public virtual string SystemLanguage
        {
            get { return GetAttribute<string>("systemLanguage", false); }
            set { Attributes["systemLanguage"] = value; }
        }

        [SvgAttribute("externalResourcesRequired")]
        public virtual bool ExternalResourcesRequired
        {
            get { return GetAttribute("externalResourcesRequired", false, defaultValue: false); }
            set { Attributes["externalResourcesRequired"] = value; }
        }

        [SvgAttribute("begin")]
        public virtual string Begin
        {
            get { return GetAttribute<string>("begin", false); }
            set { Attributes["begin"] = value; }
        }

        [SvgAttribute("dur")]
        public virtual string Duration
        {
            get { return GetAttribute<string>("dur", false); }
            set { Attributes["dur"] = value; }
        }

        [SvgAttribute("end")]
        public virtual string End
        {
            get { return GetAttribute<string>("end", false); }
            set { Attributes["end"] = value; }
        }

        [SvgAttribute("min")]
        public virtual string Minimum
        {
            get { return GetAttribute<string>("min", false); }
            set { Attributes["min"] = value; }
        }

        [SvgAttribute("max")]
        public virtual string Maximum
        {
            get { return GetAttribute<string>("max", false); }
            set { Attributes["max"] = value; }
        }

        [SvgAttribute("restart")]
        public virtual SvgAnimationRestart Restart
        {
            get { return GetAttribute("restart", false, SvgAnimationRestart.Always); }
            set { Attributes["restart"] = value; }
        }

        [SvgAttribute("repeatCount")]
        public virtual string RepeatCount
        {
            get { return GetAttribute<string>("repeatCount", false); }
            set { Attributes["repeatCount"] = value; }
        }

        [SvgAttribute("repeatDur")]
        public virtual string RepeatDuration
        {
            get { return GetAttribute<string>("repeatDur", false); }
            set { Attributes["repeatDur"] = value; }
        }

        [SvgAttribute("fill")]
        public virtual SvgAnimationFill AnimationFill
        {
            get { return GetAttribute("fill", false, SvgAnimationFill.Remove); }
            set { Attributes["fill"] = value; }
        }

        [SvgAttribute("onbegin")]
        public virtual string OnBeginScript
        {
            get { return GetAttribute<string>("onbegin", false); }
            set { Attributes["onbegin"] = value; }
        }

        [SvgAttribute("onend")]
        public virtual string OnEndScript
        {
            get { return GetAttribute<string>("onend", false); }
            set { Attributes["onend"] = value; }
        }

        [SvgAttribute("onrepeat")]
        public virtual string OnRepeatScript
        {
            get { return GetAttribute<string>("onrepeat", false); }
            set { Attributes["onrepeat"] = value; }
        }

        [SvgAttribute("onload")]
        public virtual string OnLoadScript
        {
            get { return GetAttribute<string>("onload", false); }
            set { Attributes["onload"] = value; }
        }
    }

    public abstract partial class SvgAnimationAttributeElement : SvgAnimationElement
    {
        [SvgAttribute("attributeName")]
        public virtual string AnimationAttributeName
        {
            get { return GetAttribute<string>("attributeName", false); }
            set { Attributes["attributeName"] = value; }
        }

        [SvgAttribute("attributeType")]
        public virtual SvgAnimationAttributeType AttributeType
        {
            get { return GetAttribute("attributeType", false, SvgAnimationAttributeType.Auto); }
            set { Attributes["attributeType"] = value; }
        }
    }

    public abstract partial class SvgAnimationValueElement : SvgAnimationAttributeElement
    {
        [SvgAttribute("calcMode")]
        public virtual SvgAnimationCalcMode CalcMode
        {
            get { return GetAttribute("calcMode", false, SvgAnimationCalcMode.Linear); }
            set { Attributes["calcMode"] = value; }
        }

        [SvgAttribute("values")]
        public virtual string Values
        {
            get { return GetAttribute<string>("values", false); }
            set { Attributes["values"] = value; }
        }

        [TypeConverter(typeof(SvgSemicolonNumberCollectionConverter))]
        [SvgAttribute("keyTimes")]
        public virtual SvgNumberCollection KeyTimes
        {
            get { return GetAttribute<SvgNumberCollection>("keyTimes", false); }
            set { Attributes["keyTimes"] = value; }
        }

        [SvgAttribute("keySplines")]
        public virtual string KeySplines
        {
            get { return GetAttribute<string>("keySplines", false); }
            set { Attributes["keySplines"] = value; }
        }

        [SvgAttribute("from")]
        public virtual string From
        {
            get { return GetAttribute<string>("from", false); }
            set { Attributes["from"] = value; }
        }

        [SvgAttribute("to")]
        public virtual string To
        {
            get { return GetAttribute<string>("to", false); }
            set { Attributes["to"] = value; }
        }

        [SvgAttribute("by")]
        public virtual string By
        {
            get { return GetAttribute<string>("by", false); }
            set { Attributes["by"] = value; }
        }

        [SvgAttribute("additive")]
        public virtual SvgAnimationAdditive Additive
        {
            get { return GetAttribute("additive", false, SvgAnimationAdditive.Replace); }
            set { Attributes["additive"] = value; }
        }

        [SvgAttribute("accumulate")]
        public virtual SvgAnimationAccumulate Accumulate
        {
            get { return GetAttribute("accumulate", false, SvgAnimationAccumulate.None); }
            set { Attributes["accumulate"] = value; }
        }
    }

    [SvgElement("animate")]
    public partial class SvgAnimate : SvgAnimationValueElement
    {
        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgAnimate>();
        }
    }

    [SvgElement("set")]
    public partial class SvgSet : SvgAnimationAttributeElement
    {
        [SvgAttribute("to")]
        public virtual string To
        {
            get { return GetAttribute<string>("to", false); }
            set { Attributes["to"] = value; }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgSet>();
        }
    }

    [SvgElement("animateMotion")]
    public partial class SvgAnimateMotion : SvgAnimationElement
    {
        [SvgAttribute("calcMode")]
        public virtual SvgAnimationCalcMode CalcMode
        {
            get { return GetAttribute("calcMode", false, SvgAnimationCalcMode.Paced); }
            set { Attributes["calcMode"] = value; }
        }

        [SvgAttribute("values")]
        public virtual string Values
        {
            get { return GetAttribute<string>("values", false); }
            set { Attributes["values"] = value; }
        }

        [TypeConverter(typeof(SvgSemicolonNumberCollectionConverter))]
        [SvgAttribute("keyTimes")]
        public virtual SvgNumberCollection KeyTimes
        {
            get { return GetAttribute<SvgNumberCollection>("keyTimes", false); }
            set { Attributes["keyTimes"] = value; }
        }

        [SvgAttribute("keySplines")]
        public virtual string KeySplines
        {
            get { return GetAttribute<string>("keySplines", false); }
            set { Attributes["keySplines"] = value; }
        }

        [SvgAttribute("from")]
        public virtual string From
        {
            get { return GetAttribute<string>("from", false); }
            set { Attributes["from"] = value; }
        }

        [SvgAttribute("to")]
        public virtual string To
        {
            get { return GetAttribute<string>("to", false); }
            set { Attributes["to"] = value; }
        }

        [SvgAttribute("by")]
        public virtual string By
        {
            get { return GetAttribute<string>("by", false); }
            set { Attributes["by"] = value; }
        }

        [SvgAttribute("additive")]
        public virtual SvgAnimationAdditive Additive
        {
            get { return GetAttribute("additive", false, SvgAnimationAdditive.Replace); }
            set { Attributes["additive"] = value; }
        }

        [SvgAttribute("accumulate")]
        public virtual SvgAnimationAccumulate Accumulate
        {
            get { return GetAttribute("accumulate", false, SvgAnimationAccumulate.None); }
            set { Attributes["accumulate"] = value; }
        }

        [SvgAttribute("path")]
        public virtual SvgPathSegmentList PathData
        {
            get { return GetAttribute<SvgPathSegmentList>("path", false); }
            set { Attributes["path"] = value; }
        }

        [TypeConverter(typeof(SvgSemicolonNumberCollectionConverter))]
        [SvgAttribute("keyPoints")]
        public virtual SvgNumberCollection KeyPoints
        {
            get { return GetAttribute<SvgNumberCollection>("keyPoints", false); }
            set { Attributes["keyPoints"] = value; }
        }

        [SvgAttribute("rotate")]
        public virtual string Rotate
        {
            get { return GetAttribute("rotate", false, "0"); }
            set { Attributes["rotate"] = value; }
        }

        [SvgAttribute("origin")]
        public virtual string Origin
        {
            get { return GetAttribute<string>("origin", false); }
            set { Attributes["origin"] = value; }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgAnimateMotion>();
        }
    }

    [SvgElement("animateColor")]
    public partial class SvgAnimateColor : SvgAnimationValueElement
    {
        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgAnimateColor>();
        }
    }

    [SvgElement("animateTransform")]
    public partial class SvgAnimateTransform : SvgAnimationValueElement
    {
        [SvgAttribute("type")]
        public virtual SvgAnimateTransformType TransformType
        {
            get { return GetAttribute("type", false, SvgAnimateTransformType.Translate); }
            set { Attributes["type"] = value; }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgAnimateTransform>();
        }
    }

    [SvgElement("mpath")]
    public partial class SvgMPath : SvgElement
    {
        [SvgAttribute("href", SvgAttributeAttribute.XLinkNamespace)]
        public virtual Uri ReferencedPath
        {
            get { return GetAttribute<Uri>("href", false); }
            set { Attributes["href"] = value; }
        }

        [SvgAttribute("externalResourcesRequired")]
        public virtual bool ExternalResourcesRequired
        {
            get { return GetAttribute("externalResourcesRequired", false, defaultValue: false); }
            set { Attributes["externalResourcesRequired"] = value; }
        }

        public virtual SvgPath TargetPath
        {
            get { return OwnerDocument?.IdManager.GetElementById(ReferencedPath) as SvgPath; }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgMPath>();
        }
    }
}
