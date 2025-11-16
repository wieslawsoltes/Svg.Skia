// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Svg.Ast.Metadata;

/// <summary>
/// Provides SVG 1.1 metadata describing element categories and attributes.
/// </summary>
public static partial class SvgMetadata
{
    private static readonly ImmutableDictionary<string, SvgAttributeMetadata> s_attributes;
    private static readonly ImmutableDictionary<string, SvgElementMetadata> s_elements;

    static SvgMetadata()
    {
        var attributeBuilder = LoadGeneratedAttributeMetadata().ToBuilder();
        ApplyAttributeOverrides(attributeBuilder);
        s_attributes = attributeBuilder.ToImmutable();

        var elementBuilder = LoadGeneratedElementMetadata().ToBuilder();
        ApplyElementOverrides(elementBuilder);
        s_elements = elementBuilder.ToImmutable();
    }

    private static void ApplyAttributeOverrides(ImmutableDictionary<string, SvgAttributeMetadata>.Builder builder)
    {
        void Override(string name, string type, bool inherits, bool animatable)
            => builder[name] = new SvgAttributeMetadata(name, type, inherits, animatable);

        Override("id", "Name", false, false);
        Override("class", "List", true, false);
        Override("style", "CssDeclaration", true, true);
        Override("transform", "Transform", false, true);
        Override("fill", "Paint", true, true);
        Override("fill-opacity", "OpacityValue", true, true);
        Override("stroke", "Paint", true, true);
        Override("stroke-width", "Length", true, true);
        Override("stroke-opacity", "OpacityValue", true, true);
        Override("stroke-linecap", "StrokeLinecap", true, true);
        Override("stroke-linejoin", "StrokeLinejoin", true, true);
        Override("opacity", "OpacityValue", true, true);
        Override("display", "Display", true, true);
        Override("visibility", "Visibility", true, true);
        Override("pathLength", "Number", false, true);
        Override("d", "PathData", false, true);
        Override("x", "Length", true, true);
        Override("y", "Length", true, true);
        Override("width", "Length", false, true);
        Override("height", "Length", false, true);
        Override("cx", "Length", true, true);
        Override("cy", "Length", true, true);
        Override("r", "Length", false, true);
        Override("rx", "Length", false, true);
        Override("ry", "Length", false, true);
        Override("points", "PointList", false, true);
        Override("x1", "Length", true, true);
        Override("y1", "Length", true, true);
        Override("x2", "Length", true, true);
        Override("y2", "Length", true, true);
        Override("gradientUnits", "GradientUnits", false, false);
        Override("gradientTransform", "Transform", false, false);
        Override("spreadMethod", "SpreadMethod", false, false);
        Override("offset", "NumberOrPercentage", false, true);
    }

    private static void ApplyElementOverrides(ImmutableDictionary<string, SvgElementMetadata>.Builder builder)
    {
        void Override(string name, string category, bool isContainer, IEnumerable<string> presentationAttributes)
        {
            var existing = FindGeneratedElement(builder, name)
                ?? new SvgElementMetadata(name, string.Empty, false, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

            builder[name] = new SvgElementMetadata(
                name,
                category,
                isContainer,
                presentationAttributes.ToImmutableArray(),
                existing.AdditionalAttributes);
        }

        Override("svg", "Container", true, new[] { "display", "visibility", "opacity" });
        Override("g", "Container", true, new[] { "display", "visibility", "opacity", "fill", "stroke", "stroke-width", "transform" });
        Override("defs", "Container", true, new[] { "display" });
        Override("use", "Graphics referencing", false, new[] { "display", "visibility", "opacity" });
        Override("rect", "Shape", false, new[] { "fill", "stroke", "stroke-width", "opacity" });
        Override("circle", "Shape", false, new[] { "fill", "stroke", "stroke-width", "opacity" });
        Override("ellipse", "Shape", false, new[] { "fill", "stroke", "stroke-width", "opacity" });
        Override("line", "Shape", false, new[] { "stroke", "stroke-width", "stroke-linecap", "stroke-dasharray" });
        Override("polyline", "Shape", false, new[] { "stroke", "stroke-width", "stroke-linejoin", "fill" });
        Override("polygon", "Shape", false, new[] { "stroke", "stroke-width", "stroke-linejoin", "fill" });
        Override("path", "Shape", false, new[] { "fill", "fill-opacity", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "opacity" });
        Override("linearGradient", "PaintServer", true, new[] { "gradientTransform" });
        Override("radialGradient", "PaintServer", true, new[] { "gradientTransform" });
        Override("stop", "PaintServer", false, new[] { "stop-color", "stop-opacity" });
        Override("clipPath", "Container", true, new[] { "clipPathUnits" });
        Override("mask", "Container", true, new[] { "maskContentUnits" });
    }

    private static SvgElementMetadata? FindGeneratedElement(ImmutableDictionary<string, SvgElementMetadata>.Builder builder, string name)
    {
        if (builder.TryGetValue(name, out var metadata))
        {
            return metadata;
        }

        var generatedKey = ":" + name;
        if (builder.TryGetValue(generatedKey, out metadata))
        {
            return metadata;
        }

        return null;
    }

    public static bool TryGetAttribute(string name, out SvgAttributeMetadata metadata)
        => s_attributes.TryGetValue(name, out metadata!);

    public static bool TryGetElement(string name, out SvgElementMetadata metadata)
        => s_elements.TryGetValue(name, out metadata!);

    public static ImmutableDictionary<string, SvgAttributeMetadata> Attributes => s_attributes;

    public static ImmutableDictionary<string, SvgElementMetadata> Elements => s_elements;
}
