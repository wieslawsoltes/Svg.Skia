using System;
using System.Collections.Generic;
using System.Threading;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A non-overlapping cell of a path arrangement, owned by a division result.</summary>
public sealed class SvgPathDivisionPiece
{
    internal SvgPathDivisionPiece(int sourceIndex, SK.SKPath geometry)
    {
        SourceIndex = sourceIndex;
        Geometry = geometry;
    }

    /// <summary>The highest (frontmost) input index covering this cell.</summary>
    public int SourceIndex { get; }

    /// <summary>
    /// Vector geometry in the input coordinate space. The result owns this path;
    /// copy it before retaining it beyond the result's lifetime.
    /// Disconnected contours and their holes remain a compound path.
    /// </summary>
    public SK.SKPath Geometry { get; }
}

/// <summary>Owns all native paths produced by a successful division.</summary>
public sealed class SvgPathDivisionResult : IDisposable
{
    private bool _disposed;

    internal SvgPathDivisionResult(List<SvgPathDivisionPiece> pieces)
        => Pieces = pieces.AsReadOnly();

    public IReadOnlyList<SvgPathDivisionPiece> Pieces { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var piece in Pieces)
            piece.Geometry.Dispose();
    }
}

/// <summary>
/// Partitions filled paths into non-overlapping vector cells without flattening curves.
/// Inputs must share a coordinate space and be ordered back to front. Their SVG fill
/// rules must already be represented by SKPath.FillType. Inputs are never mutated.
/// </summary>
public static class SvgPathDivisionService
{
    public const int DefaultMaximumPieces = 4096;
    public const int MaximumOperands = 128;

    /// <summary>
    /// Creates an arrangement of all input outlines. A cell uses the frontmost input's
    /// source index. Cancellation is checked between native path operations; a running
    /// native operation cannot be interrupted. Piece limits bound arrangement growth,
    /// not the complexity of an individual input path.
    /// </summary>
    public static SvgPathDivisionResult Divide(
        IReadOnlyList<SK.SKPath> paths,
        int maximumPieces = DefaultMaximumPieces,
        CancellationToken cancellationToken = default)
    {
        if (paths is null)
            throw new ArgumentNullException(nameof(paths));
        if (maximumPieces < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumPieces));
        if (paths.Count > MaximumOperands)
            throw new ArgumentException($"Divide supports at most {MaximumOperands} operands per operation.", nameof(paths));
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var path in paths)
        {
            if (path is null)
                throw new ArgumentException("A division operand cannot be null.", nameof(paths));
            if (path.FillType is SK.SKPathFillType.InverseEvenOdd or SK.SKPathFillType.InverseWinding)
                throw new ArgumentException("Unbounded inverse-filled paths cannot be divided.", nameof(paths));
            foreach (var point in path.Points)
                if (!Finite(point.X) || !Finite(point.Y))
                    throw new ArgumentException("Division requires finite path coordinates.", nameof(paths));
        }

        var pieces = new List<SvgPathDivisionPiece>();
        try
        {
            for (var sourceIndex = 0; sourceIndex < paths.Count; sourceIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Simplify normalizes self-intersections and zero-area contours while
                // respecting winding/even-odd input. It does not polygonize curves.
                using var source = paths[sourceIndex].Simplify()
                    ?? throw new InvalidOperationException("A selected path could not be normalized.");
                if (source.IsEmpty)
                    continue;
                SK.SKPath? uncovered = new SK.SKPath(source);
                var next = new List<SvgPathDivisionPiece>();
                try
                {
                    foreach (var piece in pieces)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Overlaps(piece.Geometry.Bounds, source.Bounds))
                        {
                            AddOwned(next, piece.SourceIndex, new SK.SKPath(piece.Geometry), maximumPieces);
                            continue;
                        }
                        using var intersection = Operate(piece.Geometry, source, SK.SKPathOp.Intersect);
                        if (intersection.IsEmpty)
                        {
                            AddOwned(next, piece.SourceIndex, new SK.SKPath(piece.Geometry), maximumPieces);
                            continue;
                        }
                        AddOwned(next, piece.SourceIndex,
                            Operate(piece.Geometry, source, SK.SKPathOp.Difference), maximumPieces);
                        AddOwned(next, sourceIndex, new SK.SKPath(intersection), maximumPieces);
                        cancellationToken.ThrowIfCancellationRequested();
                        var remaining = Operate(uncovered, piece.Geometry, SK.SKPathOp.Difference);
                        uncovered.Dispose();
                        uncovered = remaining;
                    }
                    var tail = uncovered;
                    uncovered = null;
                    AddOwned(next, sourceIndex, tail, maximumPieces);
                }
                catch
                {
                    DisposePieces(next);
                    throw;
                }
                finally
                {
                    uncovered?.Dispose();
                }
                DisposePieces(pieces);
                pieces = next;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new SvgPathDivisionResult(pieces);
        }
        catch
        {
            DisposePieces(pieces);
            throw;
        }
    }

    private static SK.SKPath Operate(SK.SKPath left, SK.SKPath right, SK.SKPathOp operation)
        => left.Op(right, operation)
            ?? throw new InvalidOperationException("The selected geometry could not be divided.");

    // Ownership is consumed on every path, including a rejected or empty result.
    private static void AddOwned(List<SvgPathDivisionPiece> pieces, int sourceIndex, SK.SKPath path, int limit)
    {
        if (path.IsEmpty)
        {
            path.Dispose();
            return;
        }
        if (pieces.Count >= limit)
        {
            path.Dispose();
            throw new InvalidOperationException($"Division would exceed the limit of {limit} vector pieces. Divide a smaller selection.");
        }
        var bounds = path.Bounds;
        if (!Finite(bounds.Left) || !Finite(bounds.Top) || !Finite(bounds.Right) || !Finite(bounds.Bottom))
        {
            path.Dispose();
            throw new InvalidOperationException("Division produced coordinates outside the supported finite range.");
        }
        pieces.Add(new SvgPathDivisionPiece(sourceIndex, path));
    }

    private static bool Overlaps(SK.SKRect left, SK.SKRect right)
        => left.Left < right.Right && right.Left < left.Right
            && left.Top < right.Bottom && right.Top < left.Bottom;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void DisposePieces(List<SvgPathDivisionPiece> pieces)
    {
        foreach (var piece in pieces)
            piece.Geometry.Dispose();
    }
}
