using System;
using System.Collections.Generic;
using System.Xml;
using Svg.Transforms;

namespace Svg
{
    public abstract partial class SvgElement : ISvgElement, ISvgTransformable, ISvgNode
    {
        public SvgPaintServer Color { get; set; }
        public string Content { get; set; }
        public SvgElementCollection Children { get; set; }
        public IList<ISvgNode> Nodes { get; set; }
        public SvgElement Parent { get; set; }
        public SvgDocument OwnerDocument { get; set; }
        public SvgAttributeCollection Attributes { get; set; }
        public SvgCustomAttributeCollection CustomAttributes { get; set; }
        public SvgTransformCollection Transforms { get; set; }
        public string ID { get; set; }
        public XmlSpaceHandling SpaceHandling { get; set; }
    }

    public interface ISvgNode
    {
        string Content { get; }
    }

    public interface ISvgDescriptiveElement
    {
    }

    internal interface ISvgElement
    {
        SvgElement Parent { get; }
        SvgElementCollection Children { get; }
        IList<ISvgNode> Nodes { get; }
    }
}
