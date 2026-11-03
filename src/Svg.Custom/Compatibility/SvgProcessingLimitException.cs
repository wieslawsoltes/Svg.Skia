#nullable enable

using System;

namespace Svg;

/// <summary>
/// The SVG exceeds a supported processing limit.
/// </summary>
/// <remarks>
/// Hosts may catch this exception and report that the document exceeds the supported
/// depth, reference-work, or execution-stack limit.
/// </remarks>
public sealed class SvgProcessingLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SvgProcessingLimitException"/> class.
    /// </summary>
    /// <param name="operation">The operation whose depth limit was exceeded.</param>
    /// <param name="maximumDepth">The maximum supported depth.</param>
    public SvgProcessingLimitException(string operation, int maximumDepth)
        : this(operation, SvgProcessingLimitKind.Depth, maximumDepth)
    {
    }

    internal SvgProcessingLimitException(string operation, SvgProcessingLimitKind kind, int? limit, Exception? innerException = null)
        : base(CreateMessage(operation, kind, limit), innerException)
    {
        Operation = operation;
        Kind = kind;
        Limit = limit;
    }

    /// <summary>
    /// Gets the operation whose processing limit was exceeded.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the kind of processing limit that was exceeded.
    /// </summary>
    public SvgProcessingLimitKind Kind { get; }

    /// <summary>
    /// Gets the configured depth or reference-count limit, or <see langword="null"/>
    /// when the runtime rejected the operation because of available execution stack.
    /// </summary>
    public int? Limit { get; }

    private static string CreateMessage(string operation, SvgProcessingLimitKind kind, int? limit)
    {
        return kind switch
        {
            SvgProcessingLimitKind.Depth => $"SVG {operation} exceeds the supported depth limit of {limit}.",
            SvgProcessingLimitKind.ReferenceCount => $"SVG {operation} exceeds the supported reference count limit of {limit}.",
            SvgProcessingLimitKind.ExecutionStack => $"SVG {operation} requires more execution stack than is available.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
