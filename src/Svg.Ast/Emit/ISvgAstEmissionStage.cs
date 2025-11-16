// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Svg.Ast.Emit;

/// <summary>
/// Represents a unit of work executed before the final emitter runs.
/// </summary>
public interface ISvgAstEmissionStage
{
    /// <summary>
    /// Executes the stage against the supplied context.
    /// </summary>
    void Execute(SvgAstEmissionContext context);
}
