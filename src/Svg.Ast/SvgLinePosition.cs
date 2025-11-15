// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace Svg.Ast;

/// <summary>
/// Represents a line/column position inside an SVG source buffer.
/// </summary>
public readonly record struct SvgLinePosition(int Line, int Column)
{
    public override string ToString() => $"L{Line}:C{Column}";
}
