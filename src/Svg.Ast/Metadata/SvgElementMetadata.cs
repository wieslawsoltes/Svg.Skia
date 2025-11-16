// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Immutable;

namespace Svg.Ast.Metadata;

/// <summary>
/// Describes a single SVG attribute including its data type and animation support.
/// </summary>
public sealed class SvgAttributeMetadata
{
    public SvgAttributeMetadata(string name, string dataType, bool inherits, bool animatable)
    {
        Name = name;
        DataType = dataType;
        Inherits = inherits;
        Animatable = animatable;
    }

    public string Name { get; }

    public string DataType { get; }

    public bool Inherits { get; }

    public bool Animatable { get; }
}

/// <summary>
/// Describes an SVG element, including its content category and allowed attributes.
/// </summary>
public sealed class SvgElementMetadata
{
    public SvgElementMetadata(
        string name,
        string category,
        bool isContainer,
        ImmutableArray<string> presentationAttributes,
        ImmutableArray<string> additionalAttributes)
    {
        Name = name;
        Category = category;
        IsContainer = isContainer;
        PresentationAttributes = presentationAttributes;
        AdditionalAttributes = additionalAttributes;
    }

    public string Name { get; }

    public string Category { get; }

    public bool IsContainer { get; }

    public ImmutableArray<string> PresentationAttributes { get; }

    public ImmutableArray<string> AdditionalAttributes { get; }

    public bool AllowsAttribute(string name)
    {
        foreach (var attr in PresentationAttributes)
        {
            if (string.Equals(attr, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var attr in AdditionalAttributes)
        {
            if (string.Equals(attr, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
