﻿using Xml;

namespace Svg.FilterEffects
{
    [Element("feMorphology")]
    public class SvgMorphology : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStylableAttributes
    {
        public override void Print(string indent)
        {
            base.Print(indent);
            // TODO:
        }
    }
}