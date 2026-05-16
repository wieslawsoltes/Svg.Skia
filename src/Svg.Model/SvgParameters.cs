// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using System.Drawing;

namespace Svg.Model;

public readonly record struct SvgParameters(
    Dictionary<string, string>? Entities,
    string? Css,
    Color? CurrentColor = null,
    SvgDocumentLoadOptions? LoadOptions = null)
{
    public SvgParameters(Dictionary<string, string>? entities, string? css)
        : this(entities, css, null, null)
    {
    }

    public SvgParameters(Dictionary<string, string>? entities, string? css, Color? currentColor)
        : this(entities, css, currentColor, null)
    {
    }

    public void Deconstruct(out Dictionary<string, string>? entities, out string? css)
    {
        entities = Entities;
        css = Css;
    }
}
