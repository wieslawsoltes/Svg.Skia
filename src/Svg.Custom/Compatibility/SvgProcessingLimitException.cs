using System;

namespace Svg;

/// <summary>
/// The SVG requires more nesting than can be processed safely.
/// </summary>
/// <remarks>
/// This exception is raised before input-controlled recursion can exhaust the managed stack.
/// Hosts may catch it and report that the document exceeds the supported processing limits.
/// </remarks>
public sealed class SvgProcessingLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SvgProcessingLimitException"/> class.
    /// </summary>
    /// <param name="operation">The operation whose depth limit was exceeded.</param>
    /// <param name="maximumDepth">The maximum supported depth.</param>
    public SvgProcessingLimitException(string operation, int maximumDepth)
        : base($"SVG {operation} exceeds the supported depth limit of {maximumDepth}.")
    {
        Operation = operation;
        MaximumDepth = maximumDepth;
    }

    /// <summary>
    /// Gets the operation whose depth limit was exceeded.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the maximum supported depth.
    /// </summary>
    public int MaximumDepth { get; }
}
