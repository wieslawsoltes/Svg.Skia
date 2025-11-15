// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Svg.Ast.Emit;

/// <summary>
/// Emits an output representation from an SVG AST.
/// </summary>
public interface ISvgAstEmitter<out TResult>
{
    /// <summary>
    /// Produces the output using the supplied emission context.
    /// </summary>
    TResult Emit(SvgAstEmissionContext context);
}
