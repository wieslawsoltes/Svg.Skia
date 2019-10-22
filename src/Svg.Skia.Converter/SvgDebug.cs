// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Svg;
using Svg.DataTypes;
using Svg.Document_Structure;
using Svg.FilterEffects;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Skia.Converter
{
    public class SvgDebug
    {
        public StringBuilder Builder { get; set; }

        public string IndentTab { get; set; }

        public bool PrintSvgElementAttributesEnabled { get; set; }

        public bool PrintSvgElementCustomAttributesEnabled { get; set; }

        public bool PrintSvgElementChildrenEnabled { get; set; }

        public bool PrintSvgElementNodesEnabled { get; set; }

        private string GetElementName(SvgElement svgElement)
        {
            var attr = TypeDescriptor.GetAttributes(svgElement).OfType<SvgElementAttribute>().SingleOrDefault();
            if (attr != null)
            {
                return attr.ElementName;
            }
            return "unknown";
        }

        private string Format(float value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", value);
        }

        public void WriteLine(string value)
        {
            Builder?.AppendLine(value);
        }

        public void PrintAttributes(SvgClipPath svgClipPath, string indentLine, string indentAttribute)
        {
            if (svgClipPath.ClipPathUnits != SvgCoordinateUnits.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}clipPathUnits: {svgClipPath.ClipPathUnits}");
            }
        }

        public void PrintAttributes(SvgFragment svgFragment, string indentLine, string indentAttribute)
        {
            if (svgFragment.X != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}x: {Format(svgFragment.X)}");
            }

            if (svgFragment.Y != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}y: {Format(svgFragment.Y)}");
            }

            if (svgFragment.Width != new SvgUnit(SvgUnitType.Percentage, 100f))
            {
                WriteLine($"{indentLine}{indentAttribute}width: {Format(svgFragment.Width)}");
            }

            if (svgFragment.Height != new SvgUnit(SvgUnitType.Percentage, 100f))
            {
                WriteLine($"{indentLine}{indentAttribute}height: {Format(svgFragment.Height)}");
            }

            if (svgFragment.Overflow != SvgOverflow.Inherit && svgFragment.Overflow != SvgOverflow.Hidden)
            {
                WriteLine($"{indentLine}{indentAttribute}overflow: {svgFragment.Overflow}");
            }

            if (svgFragment.ViewBox != SvgViewBox.Empty)
            {
                var viewBox = svgFragment.ViewBox;
                WriteLine($"{indentLine}{indentAttribute}viewBox: {Format(viewBox.MinX)} {Format(viewBox.MinY)} {Format(viewBox.Width)} {Format(viewBox.Height)}");
            }

            if (svgFragment.AspectRatio != null)
            {
                var @default = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
                if (svgFragment.AspectRatio.Align != @default.Align
                 || svgFragment.AspectRatio.Slice != @default.Slice
                 || svgFragment.AspectRatio.Defer != @default.Defer)
                {
                    WriteLine($"{indentLine}{indentAttribute}preserveAspectRatio: {svgFragment.AspectRatio}");
                }
            }

            if (svgFragment.FontSize != SvgUnit.Empty)
            {
                WriteLine($"{indentLine}{indentAttribute}font-size: {svgFragment.FontSize}");
            }

            if (!string.IsNullOrEmpty(svgFragment.FontFamily))
            {
                WriteLine($"{indentLine}{indentAttribute}font-family: {svgFragment.FontFamily}");
            }

            if (svgFragment.SpaceHandling != XmlSpaceHandling.@default && svgFragment.SpaceHandling != XmlSpaceHandling.inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}space: {svgFragment.SpaceHandling}");
            }
        }

        public void PrintAttributes(SvgMask svgMask, string indentLine, string indentAttribute)
        {
        }

        public void PrintAttributes(SvgDefinitionList svgDefinitionList, string indentLine, string indentAttribute)
        {
        }

        public void PrintAttributes(SvgDescription svgDescription, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgDescription.Content))
            {
                if (svgDescription.Children.Count == 0)
                {
                    WriteLine($"{indentLine}{indentAttribute}Content: |");
                    WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgDescription.Content}");
                }
            }
        }

        public void PrintAttributes(SvgDocumentMetadata svgDocumentMetadata, string indentLine, string indentAttribute)
        {
        }

        public void PrintAttributes(SvgTitle svgTitle, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgTitle.Content))
            {
                if (svgTitle.Children.Count == 0)
                {
                    WriteLine($"{indentLine}{indentAttribute}Content: |");
                    WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgTitle.Content}");
                }
            }
        }

        public void PrintAttributes(SvgMergeNode svgMergeNode, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgMergeNode.Input))
            {
                WriteLine($"{indentLine}{indentAttribute}in: {svgMergeNode.Input}");
            }
        }

        public void PrintAttributes(SvgFilter svgFilter, string indentLine, string indentAttribute)
        {
            if (svgFilter.X != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}x: {Format(svgFilter.X)}");
            }

            if (svgFilter.Y != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}y: {Format(svgFilter.Y)}");
            }

            if (svgFilter.Width != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}width: {Format(svgFilter.Width)}");
            }

            if (svgFilter.Height != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}height: {Format(svgFilter.Height)}");
            }

            if (svgFilter.ColorInterpolationFilters != SvgColourInterpolation.Inherit && svgFilter.ColorInterpolationFilters != SvgColourInterpolation.Auto)
            {
                WriteLine($"{indentLine}{indentAttribute}color-interpolation-filters: {svgFilter.ColorInterpolationFilters}");
            }
        }

        public void PrintAttributes(NonSvgElement nonSvgElement, string indentLine, string indentAttribute)
        {
            WriteLine($"{indentLine}{indentAttribute}Name: {nonSvgElement.Name}");
        }

        public void PrintAttributes(SvgGradientStop svgGradientStop, string indentLine, string indentAttribute)
        {
            if (svgGradientStop.Offset != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}offset: {svgGradientStop.Offset}");
            }

            if (svgGradientStop.StopColor != null && svgGradientStop.StopColor != SvgColourServer.NotSet)
            {
                WriteLine($"{indentLine}{indentAttribute}stop-color: {svgGradientStop.StopColor.ToString()}");
            }

            if (svgGradientStop.Opacity != 1f)
            {
                WriteLine($"{indentLine}{indentAttribute}stop-opacity: {Format(svgGradientStop.Opacity)}");
            }
        }

        public void PrintAttributes(SvgUnknownElement svgUnknownElement, string indentLine, string indentAttribute)
        {
        }

        public void PrintAttributes(SvgFont svgFont, string indentLine, string indentAttribute)
        {
            if (svgFont.HorizAdvX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}horiz-adv-x: {Format(svgFont.HorizAdvX)}");
            }

            if (svgFont.HorizOriginX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}horiz-origin-x: {Format(svgFont.HorizOriginX)}");
            }

            if (svgFont.HorizOriginY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}horiz-origin-y: {Format(svgFont.HorizOriginY)}");
            }

            if (svgFont.VertAdvY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-adv-y: {Format(svgFont.VertAdvY)}");
            }

            if (svgFont.VertOriginX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-origin-x: {Format(svgFont.VertOriginX)}");
            }

            if (svgFont.VertOriginY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-origin-y: {Format(svgFont.VertOriginY)}");
            }
        }

        public void PrintAttributes(SvgFontFace svgFontFace, string indentLine, string indentAttribute)
        {
            if (svgFontFace.Alphabetic != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}alphabetic: {Format(svgFontFace.Alphabetic)}");
            }

            if (svgFontFace.Ascent != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}ascent: {Format(svgFontFace.Ascent)}");
            }

            if (svgFontFace.AscentHeight != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}ascent-height: {Format(svgFontFace.AscentHeight)}");
            }

            if (svgFontFace.Descent != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}descent: {Format(svgFontFace.Descent)}");
            }

            if (!string.IsNullOrEmpty(svgFontFace.FontFamily))
            {
                WriteLine($"{indentLine}{indentAttribute}font-family: {svgFontFace.FontFamily}");
            }

            if (svgFontFace.FontSize != SvgUnit.Empty)
            {
                WriteLine($"{indentLine}{indentAttribute}font-size: {svgFontFace.FontSize}");
            }

            if (svgFontFace.FontStyle != SvgFontStyle.All)
            {
                WriteLine($"{indentLine}{indentAttribute}font-style: {svgFontFace.FontStyle}");
            }

            if (svgFontFace.FontVariant != SvgFontVariant.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}font-variant: {svgFontFace.FontVariant}");
            }

            if (svgFontFace.FontWeight != SvgFontWeight.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}font-weight: {svgFontFace.FontWeight}");
            }

            if (!string.IsNullOrEmpty(svgFontFace.Panose1))
            {
                WriteLine($"{indentLine}{indentAttribute}panose-1: {svgFontFace.Panose1}");
            }

            if (svgFontFace.UnitsPerEm != 1000f)
            {
                WriteLine($"{indentLine}{indentAttribute}units-per-em: {Format(svgFontFace.UnitsPerEm)}");
            }

            if (svgFontFace.XHeight != float.MinValue)
            {
                WriteLine($"{indentLine}{indentAttribute}x-height: {Format(svgFontFace.XHeight)}");
            }
        }

        public void PrintAttributes(SvgFontFaceSrc svgFontFaceSrc, string indentLine, string indentAttribute)
        {
        }

        public void PrintAttributes(SvgFontFaceUri svgFontFaceUri, string indentLine, string indentAttribute)
        {
            if (svgFontFaceUri.ReferencedElement != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgFontFaceUri.ReferencedElement}");
            }
        }

        public void PrintSvgVisualElementAttributes(SvgVisualElement svgVisualElement, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgVisualElement.Clip))
            {
                WriteLine($"{indentLine}{indentAttribute}clip: {svgVisualElement.Clip}");
            }

            if (svgVisualElement.ClipPath != null)
            {
                WriteLine($"{indentLine}{indentAttribute}clip-path: {svgVisualElement.ClipPath}");
            }

            if (svgVisualElement.ClipRule != SvgClipRule.NonZero)
            {
                WriteLine($"{indentLine}{indentAttribute}clip-rule: {svgVisualElement.ClipRule}");
            }

            if (svgVisualElement.Filter != null)
            {
                WriteLine($"{indentLine}{indentAttribute}filter: {svgVisualElement.Filter}");
            }

            // Style

            if (svgVisualElement.Visible != true)
            {
                WriteLine($"{indentLine}{indentAttribute}visibility: {svgVisualElement.Visible}");
            }

            if (!string.IsNullOrEmpty(svgVisualElement.Display))
            {
                WriteLine($"{indentLine}{indentAttribute}display: {svgVisualElement.Display}");
            }

            if (!string.IsNullOrEmpty(svgVisualElement.EnableBackground))
            {
                WriteLine($"{indentLine}{indentAttribute}enable-background: {svgVisualElement.EnableBackground}");
            }
        }

        public void PrintAttributes(SvgImage svgImage, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgImage, indentLine, indentAttribute);

            if (svgImage.AspectRatio != null)
            {
                var @default = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
                if (svgImage.AspectRatio.Align != @default.Align
                 || svgImage.AspectRatio.Slice != @default.Slice
                 || svgImage.AspectRatio.Defer != @default.Defer)
                {
                    WriteLine($"{indentLine}{indentAttribute}preserveAspectRatio: {svgImage.AspectRatio}");
                }
            }

            WriteLine($"{indentLine}{indentAttribute}x: {Format(svgImage.X)}");
            WriteLine($"{indentLine}{indentAttribute}y: {Format(svgImage.Y)}");
            WriteLine($"{indentLine}{indentAttribute}width: {Format(svgImage.Width)}");
            WriteLine($"{indentLine}{indentAttribute}height: {Format(svgImage.Height)}");

            if (svgImage.Href != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgImage.Href}");
            }
        }

        public void PrintAttributes(SvgSwitch svgSwitch, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgSwitch, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgSymbol svgSymbol, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgSymbol, indentLine, indentAttribute);

            if (svgSymbol.ViewBox != SvgViewBox.Empty)
            {
                var viewBox = svgSymbol.ViewBox;
                WriteLine($"{indentLine}{indentAttribute}viewBox: {viewBox.MinX} {viewBox.MinY} {viewBox.Width} {viewBox.Height}");
            }

            if (svgSymbol.AspectRatio != null)
            {
                var @default = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
                if (svgSymbol.AspectRatio.Align != @default.Align
                 || svgSymbol.AspectRatio.Slice != @default.Slice
                 || svgSymbol.AspectRatio.Defer != @default.Defer)
                {
                    WriteLine($"{indentLine}{indentAttribute}preserveAspectRatio: {svgSymbol.AspectRatio}");
                }
            }
        }

        public void PrintAttributes(SvgUse svgUse, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgUse, indentLine, indentAttribute);

            if (svgUse.ReferencedElement != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgUse.ReferencedElement}");
            }

            WriteLine($"{indentLine}{indentAttribute}x: {Format(svgUse.X)}");
            WriteLine($"{indentLine}{indentAttribute}y: {Format(svgUse.Y)}");
            WriteLine($"{indentLine}{indentAttribute}width: {Format(svgUse.Width)}");
            WriteLine($"{indentLine}{indentAttribute}height: {Format(svgUse.Height)}");
        }

        public void PrintAttributes(SvgForeignObject svgForeignObject, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgForeignObject, indentLine, indentAttribute);
        }

        public void PrintSvgPathBasedElementAttributes(SvgPathBasedElement svgPathBasedElement, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgPathBasedElement, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgCircle svgCircle, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgCircle, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}cx: {Format(svgCircle.CenterX)}");
            WriteLine($"{indentLine}{indentAttribute}cy: {Format(svgCircle.CenterY)}");
            WriteLine($"{indentLine}{indentAttribute}r: {Format(svgCircle.Radius)}");
        }

        public void PrintAttributes(SvgEllipse svgEllipse, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgEllipse, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}cx: {Format(svgEllipse.CenterX)}");
            WriteLine($"{indentLine}{indentAttribute}cy: {Format(svgEllipse.CenterY)}");
            WriteLine($"{indentLine}{indentAttribute}rx: {Format(svgEllipse.RadiusX)}");
            WriteLine($"{indentLine}{indentAttribute}ry: {Format(svgEllipse.RadiusY)}");
        }

        public void PrintAttributes(SvgRectangle svgRectangle, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgRectangle, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}x: {Format(svgRectangle.X)}");
            WriteLine($"{indentLine}{indentAttribute}y: {Format(svgRectangle.Y)}");
            WriteLine($"{indentLine}{indentAttribute}width: {Format(svgRectangle.Width)}");
            WriteLine($"{indentLine}{indentAttribute}height: {Format(svgRectangle.Height)}");

            if (svgRectangle.CornerRadiusX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}rx: {Format(svgRectangle.CornerRadiusX)}");
            }

            if (svgRectangle.CornerRadiusY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}ry: {Format(svgRectangle.CornerRadiusY)}");
            }
        }

        public void PrintAttributes(SvgMarker svgMarker, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgMarker, indentLine, indentAttribute);

            if (svgMarker.RefX != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}refX: {svgMarker.RefX}");
            }

            if (svgMarker.RefY != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}refY: {svgMarker.RefY}");
            }

            if (svgMarker.Orient != null)
            {
                var orient = svgMarker.Orient;
                if (orient.IsAuto == false)
                {
                    if (orient.Angle != 0f)
                    {
                        WriteLine($"{indentLine}{indentAttribute}orient: {Format(orient.Angle)}");
                    }
                }
                else
                {
                    WriteLine($"{indentLine}{indentAttribute}orient: {(orient.IsAutoStartReverse ? "auto-start-reverse" : "auto")}");
                }
            }

            if (svgMarker.Overflow != SvgOverflow.Hidden)
            {
                WriteLine($"{indentLine}{indentAttribute}overflow: {svgMarker.Overflow}");
            }

            if (svgMarker.ViewBox != SvgViewBox.Empty)
            {
                var viewBox = svgMarker.ViewBox;
                WriteLine($"{indentLine}{indentAttribute}viewBox: {viewBox.MinX} {viewBox.MinY} {viewBox.Width} {viewBox.Height}");
            }

            if (svgMarker.AspectRatio != null)
            {
                var @default = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
                if (svgMarker.AspectRatio.Align != @default.Align
                 || svgMarker.AspectRatio.Slice != @default.Slice
                 || svgMarker.AspectRatio.Defer != @default.Defer)
                {
                    WriteLine($"{indentLine}{indentAttribute}preserveAspectRatio: {svgMarker.AspectRatio}");
                }
            }

            if (svgMarker.MarkerWidth != 3f)
            {
                WriteLine($"{indentLine}{indentAttribute}markerWidth: {svgMarker.MarkerWidth}");
            }

            if (svgMarker.MarkerHeight != 3f)
            {
                WriteLine($"{indentLine}{indentAttribute}markerHeight: {svgMarker.MarkerHeight}");
            }

            if (svgMarker.MarkerUnits != SvgMarkerUnits.StrokeWidth)
            {
                WriteLine($"{indentLine}{indentAttribute}markerUnits: {svgMarker.MarkerUnits}");
            }
        }

        public void PrintAttributes(SvgGlyph svgGlyph, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgGlyph, indentLine, indentAttribute);

            if (svgGlyph.PathData != null)
            {
                PrintAttributes(svgGlyph.PathData, indentLine, indentAttribute);
            }

            if (!string.IsNullOrEmpty(svgGlyph.GlyphName))
            {
                WriteLine($"{indentLine}{indentAttribute}glyph-name: {svgGlyph.GlyphName}");
            }

            if (svgGlyph.HorizAdvX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}horiz-adv-x: {Format(svgGlyph.HorizAdvX)}");
            }

            if (!string.IsNullOrEmpty(svgGlyph.Unicode))
            {
                WriteLine($"{indentLine}{indentAttribute}unicode: {svgGlyph.Unicode}");
            }

            if (svgGlyph.VertAdvY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-adv-y: {Format(svgGlyph.VertAdvY)}");
            }

            if (svgGlyph.VertOriginX != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-origin-x: {Format(svgGlyph.VertOriginX)}");
            }

            if (svgGlyph.VertOriginY != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}vert-origin-y: {Format(svgGlyph.VertOriginY)}");
            }
        }

        public void PrintSvgMarkerElementAttributes(SvgMarkerElement svgMarkerElement, string indentLine, string indentAttribute)
        {
            PrintSvgPathBasedElementAttributes(svgMarkerElement, indentLine, indentAttribute);

            if (svgMarkerElement.MarkerEnd != null)
            {
                WriteLine($"{indentLine}{indentAttribute}marker-end: {svgMarkerElement.MarkerEnd}");
            }

            if (svgMarkerElement.MarkerMid != null)
            {
                WriteLine($"{indentLine}{indentAttribute}marker-mid: {svgMarkerElement.MarkerMid}");
            }

            if (svgMarkerElement.MarkerStart != null)
            {
                WriteLine($"{indentLine}{indentAttribute}marker-start: {svgMarkerElement.MarkerStart}");
            }
        }

        public void PrintAttributes(SvgGroup svgGroup, string indentLine, string indentAttribute)
        {
            PrintSvgMarkerElementAttributes(svgGroup, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgLine svgLine, string indentLine, string indentAttribute)
        {
            PrintSvgMarkerElementAttributes(svgLine, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}x1: {svgLine.StartX}");
            WriteLine($"{indentLine}{indentAttribute}y1: {svgLine.StartY}");
            WriteLine($"{indentLine}{indentAttribute}x2: {svgLine.EndX}");
            WriteLine($"{indentLine}{indentAttribute}y2: {svgLine.EndY}");
        }

        public void PrintAttributes(SvgPathSegmentList svgPathSegmentList, string indentLine, string indentAttribute)
        {
            if (svgPathSegmentList != null)
            {
                WriteLine($"{indentLine}{indentAttribute}d: |");

                foreach (var svgSegment in svgPathSegmentList)
                {
                    switch (svgSegment)
                    {
                        case SvgArcSegment svgArcSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgArcSegment}");
                            break;
                        case SvgClosePathSegment svgClosePathSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgClosePathSegment}");
                            break;
                        case SvgCubicCurveSegment svgCubicCurveSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgCubicCurveSegment}");
                            break;
                        case SvgLineSegment svgLineSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgLineSegment}");
                            break;
                        case SvgMoveToSegment svgMoveToSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgMoveToSegment}");
                            break;
                        case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                            WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgQuadraticCurveSegment}");
                            break;
                        default:
                            WriteLine($"ERROR: Unknown path segment type: {svgSegment.GetType()}");
                            break;
                    }
                }
            }
        }

        public void PrintAttributes(SvgPath svgPath, string indentLine, string indentAttribute)
        {
            PrintSvgMarkerElementAttributes(svgPath, indentLine, indentAttribute);

            if (svgPath.PathData != null)
            {
                PrintAttributes(svgPath.PathData, indentLine, indentAttribute);
            }

            if (svgPath.PathLength != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}pathLength: {Format(svgPath.PathLength)}");
            }
        }

        public void PrintAttributes(SvgPolygon svgPolygon, string indentLine, string indentAttribute)
        {
            PrintSvgMarkerElementAttributes(svgPolygon, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}points: {svgPolygon.Points}");
        }

        public void PrintAttributes(SvgPolyline svgPolyline, string indentLine, string indentAttribute)
        {
            PrintSvgMarkerElementAttributes(svgPolyline, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}points: {svgPolyline.Points}");
        }

        public void PrintSvgTextBaseAttributes(SvgTextBase svgTextBase, string indentLine, string indentAttribute)
        {
            PrintSvgVisualElementAttributes(svgTextBase, indentLine, indentAttribute);

            if (!string.IsNullOrEmpty(svgTextBase.Text))
            {
                if (svgTextBase.Children.Count == 0)
                {
                    WriteLine($"{indentLine}{indentAttribute}Content: |");
                    WriteLine($"{indentLine}{indentAttribute}{IndentTab}{svgTextBase.Text}");
                }
            }

            if (svgTextBase.X != null && svgTextBase.X.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}x: {svgTextBase.X}");
            }

            if (svgTextBase.Dx != null && svgTextBase.Dx.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}dx: {svgTextBase.Dx}");
            }

            if (svgTextBase.Y != null && svgTextBase.Y.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}y: {svgTextBase.Y}");
            }

            if (svgTextBase.Dy != null && svgTextBase.Dy.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}dy: {svgTextBase.Dy}");
            }

            if (!string.IsNullOrEmpty(svgTextBase.Rotate))
            {
                WriteLine($"{indentLine}{indentAttribute}rotate: {svgTextBase.Rotate}");
            }

            if (svgTextBase.TextLength != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}textLength: {svgTextBase.TextLength}");
            }

            if (svgTextBase.LengthAdjust != SvgTextLengthAdjust.Spacing)
            {
                WriteLine($"{indentLine}{indentAttribute}lengthAdjust: {svgTextBase.LengthAdjust}");
            }

            if (svgTextBase.LetterSpacing != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}letter-spacing: {svgTextBase.LetterSpacing}");
            }

            if (svgTextBase.WordSpacing != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}word-spacing: {svgTextBase.WordSpacing}");
            }
        }

        public void PrintAttributes(SvgText svgText, string indentLine, string indentAttribute)
        {
            PrintSvgTextBaseAttributes(svgText, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgTextPath svgTextPath, string indentLine, string indentAttribute)
        {
            PrintSvgTextBaseAttributes(svgTextPath, indentLine, indentAttribute);

            if (svgTextPath.StartOffset != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}startOffset: {svgTextPath.StartOffset}");
            }

            if (svgTextPath.Method != SvgTextPathMethod.Align)
            {
                WriteLine($"{indentLine}{indentAttribute}method: {svgTextPath.Method}");
            }

            if (svgTextPath.Spacing != SvgTextPathSpacing.Exact)
            {
                WriteLine($"{indentLine}{indentAttribute}spacing: {svgTextPath.Spacing}");
            }

            if (svgTextPath.ReferencedPath != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgTextPath.ReferencedPath}");
            }
        }

        public void PrintAttributes(SvgTextRef svgTextRef, string indentLine, string indentAttribute)
        {
            PrintSvgTextBaseAttributes(svgTextRef, indentLine, indentAttribute);

            if (svgTextRef.ReferencedElement != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgTextRef.ReferencedElement}");
            }
        }

        public void PrintAttributes(SvgTextSpan svgTextSpan, string indentLine, string indentAttribute)
        {
            PrintSvgTextBaseAttributes(svgTextSpan, indentLine, indentAttribute);
        }

        public void PrintSvgFilterPrimitiveAttributes(SvgFilterPrimitive svgFilterPrimitive, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgFilterPrimitive.Input))
            {
                WriteLine($"{indentLine}{indentAttribute}in: {svgFilterPrimitive.Input}");
            }

            if (!string.IsNullOrEmpty(svgFilterPrimitive.Result))
            {
                WriteLine($"{indentLine}{indentAttribute}result: {svgFilterPrimitive.Result}");
            }
        }

        public void PrintAttributes(SvgColourMatrix svgColourMatrix, string indentLine, string indentAttribute)
        {
            PrintSvgFilterPrimitiveAttributes(svgColourMatrix, indentLine, indentAttribute);

            WriteLine($"{indentLine}{indentAttribute}type: {svgColourMatrix.Type}");

            if (!string.IsNullOrEmpty(svgColourMatrix.Values))
            {
                WriteLine($"{indentLine}{indentAttribute}values: {svgColourMatrix.Values}");
            }
        }

        public void PrintAttributes(SvgGaussianBlur svgGaussianBlur, string indentLine, string indentAttribute)
        {
            PrintSvgFilterPrimitiveAttributes(svgGaussianBlur, indentLine, indentAttribute);

            if (svgGaussianBlur.StdDeviation > 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}stdDeviation: {Format(svgGaussianBlur.StdDeviation)}");
            }
        }

        public void PrintAttributes(SvgMerge svgMerge, string indentLine, string indentAttribute)
        {
            PrintSvgFilterPrimitiveAttributes(svgMerge, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgOffset svgOffset, string indentLine, string indentAttribute)
        {
            PrintSvgFilterPrimitiveAttributes(svgOffset, indentLine, indentAttribute);

            if (svgOffset.Dx != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}dx: {svgOffset.Dx}");
            }

            if (svgOffset.Dy != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}dy: {svgOffset.Dy}");
            }
        }

        public void PrintSvgPaintServerServer(SvgPaintServer svgPaintServer, string indentLine, string indentAttribute)
        {
            switch (svgPaintServer)
            {
                case SvgColourServer svgColourServer:
                    PrintAttributes(svgColourServer, indentLine, indentAttribute);
                    break;
                case SvgDeferredPaintServer svgDeferredPaintServer:
                    PrintAttributes(svgDeferredPaintServer, indentLine, indentAttribute);
                    break;
                case SvgFallbackPaintServer svgFallbackPaintServer:
                    PrintAttributes(svgFallbackPaintServer, indentLine, indentAttribute);
                    break;
                case SvgPatternServer svgPatternServer:
                    PrintAttributes(svgPatternServer, indentLine, indentAttribute);
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    PrintAttributes(svgLinearGradientServer, indentLine, indentAttribute);
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    PrintAttributes(svgRadialGradientServer, indentLine, indentAttribute);
                    break;
                default:
                    WriteLine($"ERROR: Unknown paint server type: {svgPaintServer.GetType()}");
                    break;
            }
        }

        public void PrintAttributes(SvgColourServer svgColourServer, string indentLine, string indentAttribute)
        {
            WriteLine($"{indentLine}{indentAttribute}: {svgColourServer.ToString()}");
        }

        public void PrintAttributes(SvgDeferredPaintServer svgDeferredPaintServer, string indentLine, string indentAttribute)
        {
            WriteLine($"{indentLine}{indentAttribute}: {svgDeferredPaintServer.GetType()}");
        }

        public void PrintAttributes(SvgFallbackPaintServer svgFallbackPaintServer, string indentLine, string indentAttribute)
        {
            WriteLine($"{indentLine}{indentAttribute}: {svgFallbackPaintServer.GetType()}");
        }

        public void PrintAttributes(SvgPatternServer svgPatternServer, string indentLine, string indentAttribute)
        {
            if (svgPatternServer.Overflow != SvgOverflow.Inherit && svgPatternServer.Overflow != SvgOverflow.Hidden)
            {
                WriteLine($"{indentLine}{indentAttribute}overflow: {svgPatternServer.Overflow}");
            }

            if (svgPatternServer.ViewBox != SvgViewBox.Empty)
            {
                var viewBox = svgPatternServer.ViewBox;
                WriteLine($"{indentLine}{indentAttribute}viewBox: {Format(viewBox.MinX)} {Format(viewBox.MinY)} {Format(viewBox.Width)} {Format(viewBox.Height)}");
            }

            if (svgPatternServer.AspectRatio != null)
            {
                var @default = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
                if (svgPatternServer.AspectRatio.Align != @default.Align
                 || svgPatternServer.AspectRatio.Slice != @default.Slice
                 || svgPatternServer.AspectRatio.Defer != @default.Defer)
                {
                    WriteLine($"{indentLine}{indentAttribute}preserveAspectRatio: {svgPatternServer.AspectRatio}");
                }
            }

            WriteLine($"{indentLine}{indentAttribute}width: {Format(svgPatternServer.Width)}");

            if (svgPatternServer.PatternUnits != SvgCoordinateUnits.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}patternUnits: {svgPatternServer.PatternUnits}");
            }

            if (svgPatternServer.PatternContentUnits != SvgCoordinateUnits.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}patternContentUnits: {svgPatternServer.PatternContentUnits}");
            }

            WriteLine($"{indentLine}{indentAttribute}height: {Format(svgPatternServer.Height)}");

            WriteLine($"{indentLine}{indentAttribute}x: {Format(svgPatternServer.X)}");
            WriteLine($"{indentLine}{indentAttribute}y: {Format(svgPatternServer.Y)}");

            if (svgPatternServer.InheritGradient != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgPatternServer.InheritGradient}");
            }

            PrintSvgTransformCollection(svgPatternServer.PatternTransform, indentLine, indentAttribute, "patternTransform");
        }

        public void PrintSvgGradientServerAttributes(SvgGradientServer svgGradientServer, string indentLine, string indentAttribute)
        {
            if (svgGradientServer.SpreadMethod != SvgGradientSpreadMethod.Pad)
            {
                WriteLine($"{indentLine}{indentAttribute}spreadMethod: {svgGradientServer.SpreadMethod}");
            }

            if (svgGradientServer.GradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
            {
                WriteLine($"{indentLine}{indentAttribute}gradientUnits: {svgGradientServer.GradientUnits}");
            }

            if (svgGradientServer.InheritGradient != null)
            {
                WriteLine($"{indentLine}{indentAttribute}href: {svgGradientServer.InheritGradient}");
            }

            PrintSvgTransformCollection(svgGradientServer.GradientTransform, indentLine, indentAttribute, "gradientTransform");
        }

        public void PrintAttributes(SvgLinearGradientServer svgLinearGradientServer, string indentLine, string indentAttribute)
        {
            PrintSvgGradientServerAttributes(svgLinearGradientServer, indentLine, indentAttribute);

            if (svgLinearGradientServer.X1 != new SvgUnit(SvgUnitType.Percentage, 0f))
            {
                WriteLine($"{indentLine}{indentAttribute}x1: {svgLinearGradientServer.X1}");
            }

            if (svgLinearGradientServer.Y1 != new SvgUnit(SvgUnitType.Percentage, 0f))
            {
                WriteLine($"{indentLine}{indentAttribute}y1: {svgLinearGradientServer.Y1}");
            }

            if (svgLinearGradientServer.X2 != new SvgUnit(SvgUnitType.Percentage, 100f))
            {
                WriteLine($"{indentLine}{indentAttribute}x2: {svgLinearGradientServer.X2}");
            }

            if (svgLinearGradientServer.Y2 != new SvgUnit(SvgUnitType.Percentage, 0f))
            {
                WriteLine($"{indentLine}{indentAttribute}y2: {svgLinearGradientServer.Y2}");
            }
        }

        public void PrintAttributes(SvgRadialGradientServer svgRadialGradientServer, string indentLine, string indentAttribute)
        {
            PrintSvgGradientServerAttributes(svgRadialGradientServer, indentLine, indentAttribute);

            if (svgRadialGradientServer.CenterX != new SvgUnit(SvgUnitType.Percentage, 50f))
            {
                WriteLine($"{indentLine}{indentAttribute}cx: {svgRadialGradientServer.CenterX}");
            }

            if (svgRadialGradientServer.CenterY != new SvgUnit(SvgUnitType.Percentage, 50f))
            {
                WriteLine($"{indentLine}{indentAttribute}cy: {svgRadialGradientServer.CenterY}");
            }

            if (svgRadialGradientServer.Radius != new SvgUnit(SvgUnitType.Percentage, 50f))
            {
                WriteLine($"{indentLine}{indentAttribute}r: {svgRadialGradientServer.Radius}");
            }

            if (svgRadialGradientServer.FocalX != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}fx: {svgRadialGradientServer.FocalX}");
            }

            if (svgRadialGradientServer.FocalY != SvgUnit.None)
            {
                WriteLine($"{indentLine}{indentAttribute}fy: {svgRadialGradientServer.FocalY}");
            }
        }

        public void PrintSvgKernAttributes(SvgKern svgKern, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgKern.Glyph1))
            {
                WriteLine($"{indentLine}{indentAttribute}g1: {svgKern.Glyph1}");
            }

            if (!string.IsNullOrEmpty(svgKern.Glyph2))
            {
                WriteLine($"{indentLine}{indentAttribute}g2: {svgKern.Glyph2}");
            }

            if (!string.IsNullOrEmpty(svgKern.Unicode1))
            {
                WriteLine($"{indentLine}{indentAttribute}u1: {svgKern.Unicode1}");
            }

            if (!string.IsNullOrEmpty(svgKern.Unicode2))
            {
                WriteLine($"{indentLine}{indentAttribute}u2: {svgKern.Unicode2}");
            }

            if (svgKern.Kerning != 0f)
            {
                WriteLine($"{indentLine}{indentAttribute}k: {Format(svgKern.Kerning)}");
            }
        }

        public void PrintAttributes(SvgVerticalKern svgVerticalKern, string indentLine, string indentAttribute)
        {
            PrintSvgKernAttributes(svgVerticalKern, indentLine, indentAttribute);
        }

        public void PrintAttributes(SvgHorizontalKern svgHorizontalKern, string indentLine, string indentAttribute)
        {
            PrintSvgKernAttributes(svgHorizontalKern, indentLine, indentAttribute);
        }

        public void PrintSvgTransformAttributes(SvgTransform svgTransform, string indentLine, string indentAttribute)
        {
            switch (svgTransform)
            {
                case SvgMatrix svgMatrix:
                    {
                        WriteLine($"{indentLine}matrix:");
                        WriteLine($"{indentLine}{indentAttribute}type: {svgMatrix.GetType().Name}");
                        var points = string.Format(
                                        CultureInfo.InvariantCulture,
                                        "{0}, {1}, {2}, {3}, {4}, {5}",
                                        svgMatrix.Points[0],
                                        svgMatrix.Points[1],
                                        svgMatrix.Points[2],
                                        svgMatrix.Points[3],
                                        svgMatrix.Points[4],
                                        svgMatrix.Points[5]);
                        WriteLine($"{indentLine}{indentAttribute}points: {points}");
                    }
                    break;
                case SvgRotate svgRotate:
                    {
                        WriteLine($"{indentLine}rotate:");
                        WriteLine($"{indentLine}{indentAttribute}type: {svgRotate.GetType().Name}");
                        WriteLine($"{indentLine}{indentAttribute}Angle: {Format(svgRotate.Angle)}");
                        WriteLine($"{indentLine}{indentAttribute}CenterX: {Format(svgRotate.CenterX)}");
                        WriteLine($"{indentLine}{indentAttribute}CenterY: {Format(svgRotate.CenterY)}");
                    }
                    break;
                case SvgScale svgScale:
                    {
                        WriteLine($"{indentLine}scale:");
                        WriteLine($"{indentLine}{indentAttribute}type: {svgScale.GetType().Name}");
                        WriteLine($"{indentLine}{indentAttribute}X: {Format(svgScale.X)}");
                        WriteLine($"{indentLine}{indentAttribute}Y: {Format(svgScale.Y)}");
                    }
                    break;
                case SvgShear svgShear:
                    {
                        WriteLine($"{indentLine}shear:");
                        WriteLine($"{indentLine}{indentAttribute}type: {svgShear.GetType().Name}");
                        WriteLine($"{indentLine}{indentAttribute}X: {Format(svgShear.X)}");
                        WriteLine($"{indentLine}{indentAttribute}Y: {Format(svgShear.Y)}");
                    }
                    break;
                case SvgSkew svgSkew:
                    {
                        if (svgSkew.AngleY == 0)
                        {
                            WriteLine($"{indentLine}skewX:");
                            WriteLine($"{indentLine}{indentAttribute}type: {svgSkew.GetType().Name}");
                            WriteLine($"{indentLine}{indentAttribute}AngleX: {Format(svgSkew.AngleX)}");
                        }
                        else
                        {
                            WriteLine($"{indentLine}skewY:");
                            WriteLine($"{indentLine}{indentAttribute}type: {svgSkew.GetType().Name}");
                            WriteLine($"{indentLine}{indentAttribute}AngleY: {Format(svgSkew.AngleY)}");
                        }
                    }
                    break;
                case SvgTranslate svgTranslate:
                    {
                        WriteLine($"{indentLine}translate:");
                        WriteLine($"{indentLine}{indentAttribute}type: {svgTranslate.GetType().Name}");
                        WriteLine($"{indentLine}{indentAttribute}X: {Format(svgTranslate.X)}");
                        WriteLine($"{indentLine}{indentAttribute}Y: {Format(svgTranslate.Y)}");
                    }
                    break;
                default:
                    WriteLine($"ERROR: Unknown transform type: {svgTransform.GetType()}");
                    break;
            }
        }

        public void PrintSvgTransformCollection(SvgTransformCollection svgTransformCollection, string indentLine, string indentAttribute, string attribute)
        {
            if (svgTransformCollection != null && svgTransformCollection.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}{attribute}:");

                foreach (var svgTransform in svgTransformCollection)
                {
                    PrintSvgTransformAttributes(svgTransform, indentLine + indentAttribute + IndentTab, indentAttribute);
                }
            }
        }

        public void PrintSvgElementAttributes(SvgElement svgElement, string indentLine, string indentAttribute)
        {
            if (!string.IsNullOrEmpty(svgElement.ID))
            {
                WriteLine($"{indentLine}{indentAttribute}id: {svgElement.ID}");
            }

            if (svgElement.SpaceHandling != XmlSpaceHandling.@default && svgElement.SpaceHandling != XmlSpaceHandling.inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}space: {svgElement.SpaceHandling}");
            }

            if (svgElement.Color != null && svgElement.Color != SvgColourServer.NotSet)
            {
                WriteLine($"{indentLine}{indentAttribute}color: {svgElement.Color.ToString()}");
            }

            // Style Attributes

            if (svgElement.Fill != null && svgElement.Fill != SvgColourServer.NotSet)
            {
                WriteLine($"{indentLine}{indentAttribute}fill: {svgElement.Fill.ToString()}");
            }

            if (svgElement.Stroke != null && svgElement.Fill != SvgColourServer.NotSet)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke: {svgElement.Stroke.ToString()}");
            }

            if (svgElement.FillRule != SvgFillRule.NonZero)
            {
                WriteLine($"{indentLine}{indentAttribute}fill-rule: {svgElement.FillRule}");
            }

            if (svgElement.FillOpacity != 1f)
            {
                WriteLine($"{indentLine}{indentAttribute}fill-opacity: {Format(svgElement.FillOpacity)}");
            }

            if (svgElement.StrokeWidth != 1f)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-width: {Format(svgElement.StrokeWidth)}");
            }

            if (svgElement.StrokeLineCap != SvgStrokeLineCap.Butt)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-linecap: {svgElement.StrokeLineCap}");
            }

            if (svgElement.StrokeLineJoin != SvgStrokeLineJoin.Miter)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-linejoin: {svgElement.StrokeLineJoin}");
            }

            if (svgElement.StrokeMiterLimit != 4f)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-miterlimit: {Format(svgElement.StrokeMiterLimit)}");
            }

            if (svgElement.StrokeDashArray != null && svgElement.StrokeDashArray.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-dasharray: {svgElement.StrokeDashArray}");
            }

            if (svgElement.StrokeDashOffset != SvgUnit.Empty)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-dashoffset: {svgElement.StrokeDashOffset}");
            }

            if (svgElement.StrokeOpacity != 1f)
            {
                WriteLine($"{indentLine}{indentAttribute}stroke-opacity: {Format(svgElement.StrokeOpacity)}");
            }

            if (svgElement.StopColor != null)
            {
                WriteLine($"{indentLine}{indentAttribute}stop-color: {svgElement.StopColor.ToString()}");
            }

            if (svgElement.Opacity != 1f) // TODO: check attribute name, confilit with `stop-opacity` Svg.SvgGradientStop. Use always SvgAttribute name for validation.
            {
                WriteLine($"{indentLine}{indentAttribute}opacity: {Format(svgElement.Opacity)}");
            }

            if (svgElement.ShapeRendering != SvgShapeRendering.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}shape-rendering: {svgElement.ShapeRendering}");
            }

            if (svgElement.TextAnchor != SvgTextAnchor.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}text-anchor: {svgElement.TextAnchor}");
            }

            if (!string.IsNullOrEmpty(svgElement.BaselineShift))
            {
                WriteLine($"{indentLine}{indentAttribute}baseline-shift: {svgElement.BaselineShift}");
            }

            if (!string.IsNullOrEmpty(svgElement.FontFamily))
            {
                WriteLine($"{indentLine}{indentAttribute}font-family: {svgElement.FontFamily}");
            }

            if (svgElement.FontSize != SvgUnit.Empty)
            {
                WriteLine($"{indentLine}{indentAttribute}font-size: {svgElement.FontSize}");
            }

            if (svgElement.FontStyle != SvgFontStyle.All)
            {
                WriteLine($"{indentLine}{indentAttribute}font-style: {svgElement.FontStyle}");
            }

            if (svgElement.FontVariant != SvgFontVariant.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}font-variant: {svgElement.FontVariant}");
            }

            if (svgElement.TextDecoration != SvgTextDecoration.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}text-decoration: {svgElement.TextDecoration}");
            }

            if (svgElement.FontWeight != SvgFontWeight.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}font-weight: {svgElement.FontWeight}");
            }

            if (svgElement.TextTransformation != SvgTextTransformation.Inherit)
            {
                WriteLine($"{indentLine}{indentAttribute}text-transform: {svgElement.TextTransformation}");
            }

            if (!string.IsNullOrEmpty(svgElement.Font))
            {
                WriteLine($"{indentLine}{indentAttribute}font: {svgElement.Font}");
            }
        }

        public void PrintSvgElement(SvgElement svgElement, string indentLine, string indentAttribute)
        {
            var name = GetElementName(svgElement);
            var type = svgElement.GetType();

            WriteLine($"{indentLine}-");

            indentLine += IndentTab;

            WriteLine($"{indentLine}{indentAttribute}name: {name}");
            WriteLine($"{indentLine}{indentAttribute}type: {type}");

            if (PrintSvgElementAttributesEnabled)
            {
                PrintSvgTransformCollection(svgElement.Transforms, indentLine, indentAttribute, "transforms");
                PrintSvgElementAttributes(svgElement, indentLine, indentAttribute);
            }

            switch (svgElement)
            {
                case SvgClipPath svgClipPath:
                    PrintAttributes(svgClipPath, indentLine, indentAttribute);
                    break;
                case SvgDocument svgDocument:
                    PrintAttributes(svgDocument, indentLine, indentAttribute);
                    break;
                case SvgFragment svgFragment:
                    PrintAttributes(svgFragment, indentLine, indentAttribute);
                    break;
                case SvgMask svgMask:
                    PrintAttributes(svgMask, indentLine, indentAttribute);
                    break;
                case SvgDefinitionList svgDefinitionList:
                    PrintAttributes(svgDefinitionList, indentLine, indentAttribute);
                    break;
                case SvgDescription svgDescription:
                    PrintAttributes(svgDescription, indentLine, indentAttribute);
                    break;
                case SvgDocumentMetadata svgDocumentMetadata:
                    PrintAttributes(svgDocumentMetadata, indentLine, indentAttribute);
                    break;
                case SvgTitle svgTitle:
                    PrintAttributes(svgTitle, indentLine, indentAttribute);
                    break;
                case SvgMergeNode svgMergeNode:
                    PrintAttributes(svgMergeNode, indentLine, indentAttribute);
                    break;
                case SvgFilter svgFilter:
                    PrintAttributes(svgFilter, indentLine, indentAttribute);
                    break;
                case NonSvgElement nonSvgElement:
                    PrintAttributes(nonSvgElement, indentLine, indentAttribute);
                    break;
                case SvgGradientStop svgGradientStop:
                    PrintAttributes(svgGradientStop, indentLine, indentAttribute);
                    break;
                case SvgUnknownElement svgUnknownElement:
                    PrintAttributes(svgUnknownElement, indentLine, indentAttribute);
                    break;
                case SvgFont svgFont:
                    PrintAttributes(svgFont, indentLine, indentAttribute);
                    break;
                case SvgFontFace svgFontFace:
                    PrintAttributes(svgFontFace, indentLine, indentAttribute);
                    break;
                case SvgFontFaceSrc svgFontFaceSrc:
                    PrintAttributes(svgFontFaceSrc, indentLine, indentAttribute);
                    break;
                case SvgFontFaceUri svgFontFaceUri:
                    PrintAttributes(svgFontFaceUri, indentLine, indentAttribute);
                    break;
                case SvgImage svgImage:
                    PrintAttributes(svgImage, indentLine, indentAttribute);
                    break;
                case SvgSwitch svgSwitch:
                    PrintAttributes(svgSwitch, indentLine, indentAttribute);
                    break;
                case SvgSymbol svgSymbol:
                    PrintAttributes(svgSymbol, indentLine, indentAttribute);
                    break;
                case SvgUse svgUse:
                    PrintAttributes(svgUse, indentLine, indentAttribute);
                    break;
                case SvgForeignObject svgForeignObject:
                    PrintAttributes(svgForeignObject, indentLine, indentAttribute);
                    break;
                case SvgCircle svgCircle:
                    PrintAttributes(svgCircle, indentLine, indentAttribute);
                    break;
                case SvgEllipse svgEllipse:
                    PrintAttributes(svgEllipse, indentLine, indentAttribute);
                    break;
                case SvgRectangle svgRectangle:
                    PrintAttributes(svgRectangle, indentLine, indentAttribute);
                    break;
                case SvgMarker svgMarker:
                    PrintAttributes(svgMarker, indentLine, indentAttribute);
                    break;
                case SvgGlyph svgGlyph:
                    PrintAttributes(svgGlyph, indentLine, indentAttribute);
                    break;
                case SvgGroup svgGroup:
                    PrintAttributes(svgGroup, indentLine, indentAttribute);
                    break;
                case SvgLine svgLine:
                    PrintAttributes(svgLine, indentLine, indentAttribute);
                    break;
                case SvgPath svgPath:
                    PrintAttributes(svgPath, indentLine, indentAttribute);
                    break;
                case SvgPolyline svgPolyline:
                    PrintAttributes(svgPolyline, indentLine, indentAttribute);
                    break;
                case SvgPolygon svgPolygon:
                    PrintAttributes(svgPolygon, indentLine, indentAttribute);
                    break;
                case SvgText svgText:
                    PrintAttributes(svgText, indentLine, indentAttribute);
                    break;
                case SvgTextPath svgTextPath:
                    PrintAttributes(svgTextPath, indentLine, indentAttribute);
                    break;
                case SvgTextRef svgTextRef:
                    PrintAttributes(svgTextRef, indentLine, indentAttribute);
                    break;
                case SvgTextSpan svgTextSpan:
                    PrintAttributes(svgTextSpan, indentLine, indentAttribute);
                    break;
                case SvgColourMatrix svgColourMatrix:
                    PrintAttributes(svgColourMatrix, indentLine, indentAttribute);
                    break;
                case SvgGaussianBlur svgGaussianBlur:
                    PrintAttributes(svgGaussianBlur, indentLine, indentAttribute);
                    break;
                case SvgMerge svgMerge:
                    PrintAttributes(svgMerge, indentLine, indentAttribute);
                    break;
                case SvgOffset svgOffset:
                    PrintAttributes(svgOffset, indentLine, indentAttribute);
                    break;
                case SvgColourServer svgColourServer:
                    PrintAttributes(svgColourServer, indentLine, indentAttribute);
                    break;
                case SvgDeferredPaintServer svgDeferredPaintServer:
                    PrintAttributes(svgDeferredPaintServer, indentLine, indentAttribute);
                    break;
                case SvgFallbackPaintServer svgFallbackPaintServer:
                    PrintAttributes(svgFallbackPaintServer, indentLine, indentAttribute);
                    break;
                case SvgPatternServer svgPatternServer:
                    PrintAttributes(svgPatternServer, indentLine, indentAttribute);
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    PrintAttributes(svgLinearGradientServer, indentLine, indentAttribute);
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    PrintAttributes(svgRadialGradientServer, indentLine, indentAttribute);
                    break;
                case SvgVerticalKern svgVerticalKern:
                    PrintAttributes(svgVerticalKern, indentLine, indentAttribute);
                    break;
                case SvgHorizontalKern svgHorizontalKern:
                    PrintAttributes(svgHorizontalKern, indentLine, indentAttribute);
                    break;
                default:
                    WriteLine($"ERROR: Unknown elemen type: {svgElement.GetType()}");
                    break;
            }

            if (PrintSvgElementCustomAttributesEnabled && svgElement.CustomAttributes.Count > 0)
            {
                foreach (var attribute in svgElement.CustomAttributes)
                {
                    WriteLine($"{indentLine}{indentAttribute}{attribute.Key}: {attribute.Value}");
                }
            }

            if (PrintSvgElementNodesEnabled && svgElement.Nodes.Count > 0)
            {
                WriteLine($"{indentLine}nodes: |");

                foreach (var node in svgElement.Nodes)
                {
                    WriteLine($"{indentLine}{indentAttribute}{IndentTab}{node.Content}");
                }
            }

            if (PrintSvgElementChildrenEnabled && svgElement.Children.Count > 0)
            {
                WriteLine($"{indentLine}{indentAttribute}children:");

                foreach (var child in svgElement.Children)
                {
                    PrintSvgElement(child, indentLine + IndentTab, indentAttribute);
                }
            }
        }

        public static void Print(SvgElement svgElement, string path)
        {
            var svgDebug = new SvgDebug()
            {
                Builder = new StringBuilder(),
                IndentTab = "  ",
                PrintSvgElementAttributesEnabled = true,
                PrintSvgElementCustomAttributesEnabled = true,
                PrintSvgElementChildrenEnabled = true,
                PrintSvgElementNodesEnabled = false
            };
            svgDebug.PrintSvgElement(svgElement, "", "");
            if (svgDebug.Builder != null)
            {
                var yaml = svgDebug.Builder.ToString();
                if (!string.IsNullOrEmpty(yaml))
                {
                    File.WriteAllText(path, yaml);
                }
            }
        }
    }
}
