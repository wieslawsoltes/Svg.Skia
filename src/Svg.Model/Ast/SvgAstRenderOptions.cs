// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Svg.Model.Ast;

/// <summary>
/// Configures how the AST-based renderer constructs Skia pictures.
/// </summary>
public sealed class SvgAstRenderOptions
{
    /// <summary>
    /// Gets the default rendering options.
    /// </summary>
    public static SvgAstRenderOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the fallback viewport width in user units when the SVG does not specify one.
    /// </summary>
    public float DefaultViewportWidth { get; init; } = 100f;

    /// <summary>
    /// Gets or sets the fallback viewport height in user units when the SVG does not specify one.
    /// </summary>
    public float DefaultViewportHeight { get; init; } = 100f;

    /// <summary>
    /// Gets or sets a value indicating whether the renderer should preserve the aspect ratio when a viewBox is present.
    /// </summary>
    public bool PreserveAspectRatio { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether shape paints should be antialiased.
    /// </summary>
    public bool EnableAntialiasing { get; init; } = true;
}
