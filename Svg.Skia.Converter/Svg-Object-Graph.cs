// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Svg;
using Svg.Css;
using Svg.DataTypes;
using Svg.ExCSS;
using Svg.ExCSS.Model;
using Svg.ExCSS.Model.Extensions;
using Svg.Exceptions;
using Svg.ExtensionMethods;
using Svg.Document_Structure;
using Svg.FilterEffects;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Skia.Converter
{
    /// The <see cref="SvgElement"/> object graph.
    /// +---abstract class <see cref="SvgElement"/>
    /// |   +---class <see cref="SvgClipPath"/>
    /// |   +---class <see cref="SvgFragment"/>
    /// |       \---class <see cref="SvgDocument"/>
    /// |   +---class <see cref="SvgMask"/>
    /// |   +---class <see cref="SvgDefinitionList"/>
    /// |   +---class <see cref="SvgDescription"/>
    /// |   +---class <see cref="SvgDocumentMetadata"/>
    /// |   +---class <see cref="SvgTitle"/>
    /// |   +---class <see cref="SvgMergeNode"/>
    /// |   +---class <see cref="SvgFilter"/>
    /// |   +---class <see cref="NonSvgElement"/>
    /// |   +---class <see cref="SvgGradientStop"/>
    /// |   +---class <see cref="SvgUnknownElement"/>
    /// |   +---class <see cref="SvgFont"/>
    /// |   +---class <see cref="SvgFontFace"/>
    /// |   +---class <see cref="SvgFontFaceSrc"/>
    /// |   +---class <see cref="SvgFontFaceUri"/>
    /// |   +---abstract class <see cref="SvgVisualElement"/>
    /// |   |   +---class <see cref="SvgImage"/>
    /// |   |   +---class <see cref="SvgSwitch"/>
    /// |   |   +---class <see cref="SvgSymbol"/>
    /// |   |   +---class <see cref="SvgUse"/>
    /// |   |   +---class <see cref="SvgForeignObject"/>
    /// |   |   +---abstract class <see cref="SvgPathBasedElement"/>
    /// |   |       +---<see cref="SvgCircle"/>
    /// |   |       +---<see cref="SvgEllipse"/>
    /// |   |       +---<see cref="SvgRectangle"/>
    /// |   |       +---<see cref="SvgMarker"/>
    /// |   |       +---<see cref="SvgGlyph"/>
    /// |   |       +---abstract class <see cref="SvgMarkerElement"/>
    /// |   |           +---class <see cref="SvgGroup"/>
    /// |   |           +---class <see cref="SvgLine"/>
    /// |   |           +---class <see cref="SvgPath"/>
    /// |   |           \---class <see cref="SvgPolygon"/>
    /// |   |               \---class <see cref="SvgPolyline"/>
    /// |   \-------abstract class <see cref="SvgTextBase"/>
    /// |           +----class <see cref="SvgText"/>
    /// |           +----class <see cref="SvgTextPath"/>
    /// |           +----class <see cref="SvgTextRef"/>
    /// |           \----class <see cref="SvgTextSpan"/>
    /// +---abstract class <see cref="SvgFilterPrimitive"/>
    /// |   +---class <see cref="SvgColourMatrix"/>
    /// |   +---class <see cref="SvgGaussianBlur"/>
    /// |   +---class <see cref="SvgMerge"/>
    /// |   \---class <see cref="SvgOffset"/>
    /// +---abstract class <see cref="SvgPaintServer"/>
    /// |   +---class <see cref="SvgColourServer"/>
    /// |   +---class <see cref="SvgDeferredPaintServer"/>
    /// |   +---class <see cref="SvgFallbackPaintServer"/>
    /// |   \---class <see cref="SvgPatternServer"/>
    /// |       \---abstract class <see cref="SvgGradientServer"/>
    /// |           +---class <see cref="SvgLinearGradientServer"/>
    /// |           \---class <see cref="SvgRadialGradientServer"/>
    /// \---abstract class <see cref="SvgKern"/>
    ///     +---class <see cref="SvgVerticalKern"/>
    ///     \---class <see cref="SvgHorizontalKern"/>

    /// The <see cref="SvgPathSegment"/> object graph.
    /// +---abstract class <see cref="SvgPathSegment"/>
    ///     +---class <see cref="SvgArcSegment"/>
    ///     +---class <see cref="SvgClosePathSegment"/>
    ///     +---class <see cref="SvgCubicCurveSegment"/>
    ///     +---class <see cref="SvgLineSegment"/>
    ///     +---class <see cref="SvgMoveToSegment"/>
    ///     \---class <see cref="SvgQuadraticCurveSegment"/>
}
