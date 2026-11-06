namespace Svg;

/// <summary>
/// Identifies the safety boundary that stopped SVG processing.
/// </summary>
public enum SvgProcessingLimitKind
{
    /// <summary>
    /// A structural or processing depth exceeded its supported maximum.
    /// </summary>
    Depth,

    /// <summary>
    /// A synchronous operation attempted to resolve too many resource references.
    /// </summary>
    ReferenceCount,

    /// <summary>
    /// The runtime detected insufficient remaining execution stack.
    /// </summary>
    ExecutionStack
}
