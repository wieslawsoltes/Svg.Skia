using System;
using Svg.DataTypes;

namespace Svg
{
    public partial class SvgElement
    {
        public SvgPaintServer Fill { get; set; }
        public SvgPaintServer Stroke { get; set; }
        public SvgFillRule FillRule { get; set; }
        public float FillOpacity { get; set; }
        public SvgUnit StrokeWidth { get; set; }
        public SvgStrokeLineCap StrokeLineCap { get; set; }
        public SvgStrokeLineJoin StrokeLineJoin { get; set; }
        public float StrokeMiterLimit { get; set; }
        public SvgUnitCollection StrokeDashArray { get; set; }
        public SvgUnit StrokeDashOffset { get; set; }
        public float StrokeOpacity { get; set; }
        public float Opacity { get; set; }
        public SvgShapeRendering ShapeRendering { get; set; }
        public SvgColourInterpolation ColorInterpolation { get; set; }
        public SvgColourInterpolation ColorInterpolationFilters { get; set; }
        public string Visibility { get; set; }
        public string Display { get; set; }
        public SvgTextAnchor TextAnchor { get; set; }
        public string BaselineShift { get; set; }
        public string FontFamily { get; set; }
        public SvgUnit FontSize { get; set; }
        public SvgFontStyle FontStyle { get; set; }
        public SvgFontVariant FontVariant { get; set; }
        public SvgTextDecoration TextDecoration { get; set; }
        public SvgFontWeight FontWeight { get; set; }
        public SvgFontStretch FontStretch { get; set; }
        public SvgTextTransformation TextTransformation { get; set; }
    }
}
