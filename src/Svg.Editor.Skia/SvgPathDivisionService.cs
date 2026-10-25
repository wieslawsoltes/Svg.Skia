using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A non-overlapping cell of a path arrangement, owned by a division result.</summary>
public sealed class SvgPathDivisionPiece
{
    internal SvgPathDivisionPiece(int sourceIndex, SK.SKPath geometry)
    { SourceIndex = sourceIndex; Geometry = geometry; }
    /// <summary>The highest (frontmost) input index covering this cell.</summary>
    public int SourceIndex { get; }
    /// <summary>Owned by the result. Copy before retaining beyond the result's lifetime.</summary>
    public SK.SKPath Geometry { get; }
}

public readonly struct SvgPathDivisionProgress
{
    internal SvgPathDivisionProgress(int completed, int total, int pieces)
    { OperandsCompleted = completed; OperandCount = total; PieceCount = pieces; }
    public int OperandsCompleted { get; }
    public int OperandCount { get; }
    public int PieceCount { get; }
}

/// <summary>Owns all native paths produced by a successful division.</summary>
public sealed class SvgPathDivisionResult : IDisposable
{
    private bool _disposed;
    internal SvgPathDivisionResult(List<SvgPathDivisionPiece> pieces) => Pieces = pieces.AsReadOnly();
    public IReadOnlyList<SvgPathDivisionPiece> Pieces { get; }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var piece in Pieces) piece.Geometry.Dispose();
    }
}

/// <summary>
/// Partitions filled paths in a common coordinate space, ordered back to front.
/// Inputs are borrowed, never mutated, and must remain alive and unchanged until completion.
/// Native operations retain curves. Disconnected contours remain compound paths.
/// </summary>
public static class SvgPathDivisionService
{
    public const int DefaultMaximumPieces = 4096;
    public const int MaximumOperands = 128;

    public static SvgPathDivisionResult Divide(IReadOnlyList<SK.SKPath> paths,
        int maximumPieces = DefaultMaximumPieces, CancellationToken cancellationToken = default)
    {
        SvgPathDivisionResult? result = null;
        foreach (var _ in Steps(paths, maximumPieces, cancellationToken, pieces => result = new SvgPathDivisionResult(pieces))) { }
        return result!;
    }

    /// <summary>
    /// Executes the same engine in bounded cooperative slices. A real timer yield allows
    /// a single-threaded browser host to process input; desktop callers may run this on a
    /// worker. Cancellation cannot interrupt one native Skia operation, but is checked
    /// between cells and operands. Progress reporting is throttled to slice boundaries.
    /// </summary>
    public static async Task<SvgPathDivisionResult> DivideAsync(IReadOnlyList<SK.SKPath> paths,
        int maximumPieces = DefaultMaximumPieces, CancellationToken cancellationToken = default,
        IProgress<SvgPathDivisionProgress>? progress = null)
    {
        SvgPathDivisionResult? result = null;
        var slice = Stopwatch.StartNew();
        var completed = -1;
        foreach (var step in Steps(paths, maximumPieces, cancellationToken, pieces => result = new SvgPathDivisionResult(pieces)))
        {
            var yield = slice.ElapsedMilliseconds >= 8;
            if (yield || completed != step.OperandsCompleted)
            {
                progress?.Report(step);
                completed = step.OperandsCompleted;
            }
            if (yield)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                slice.Restart();
            }
        }
        return result!;
    }

    // Iterator disposal unwinds every native temporary, including cancellation or a
    // throwing progress consumer. Only the final callback transfers result ownership.
    private static IEnumerable<SvgPathDivisionProgress> Steps(IReadOnlyList<SK.SKPath> paths,
        int maximumPieces, CancellationToken token, Action<List<SvgPathDivisionPiece>> complete)
    {
        if (paths is null) throw new ArgumentNullException(nameof(paths));
        if (maximumPieces < 1) throw new ArgumentOutOfRangeException(nameof(maximumPieces));
        if (paths.Count > MaximumOperands)
            throw new ArgumentException($"Divide supports at most {MaximumOperands} operands per operation.", nameof(paths));
        token.ThrowIfCancellationRequested();
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (path is null) throw new ArgumentException("A division operand cannot be null.", nameof(paths));
            if (path.FillType is SK.SKPathFillType.InverseEvenOdd or SK.SKPathFillType.InverseWinding)
                throw new ArgumentException("Unbounded inverse-filled paths cannot be divided.", nameof(paths));
            foreach (var point in path.Points)
                if (!Finite(point.X) || !Finite(point.Y))
                    throw new ArgumentException("Division requires finite path coordinates.", nameof(paths));
        }
        var pieces = new List<SvgPathDivisionPiece>();
        var transferred = false;
        try
        {
            yield return new SvgPathDivisionProgress(0, paths.Count, 0);
            for (var sourceIndex = 0; sourceIndex < paths.Count; sourceIndex++)
            {
                token.ThrowIfCancellationRequested();
                using var source = paths[sourceIndex].Simplify()
                    ?? throw new InvalidOperationException("A selected path could not be normalized.");
                if (!source.IsEmpty)
                {
                    SK.SKPath? uncovered = new SK.SKPath(source);
                    var next = new List<SvgPathDivisionPiece>();
                    var adopted = false;
                    try
                    {
                        foreach (var piece in pieces)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!Overlaps(piece.Geometry.Bounds, source.Bounds))
                                AddOwned(next, piece.SourceIndex, new SK.SKPath(piece.Geometry), maximumPieces);
                            else
                            {
                                using var intersection = Operate(piece.Geometry, source, SK.SKPathOp.Intersect);
                                if (intersection.IsEmpty)
                                    AddOwned(next, piece.SourceIndex, new SK.SKPath(piece.Geometry), maximumPieces);
                                else
                                {
                                    token.ThrowIfCancellationRequested();
                                    AddOwned(next, piece.SourceIndex, Operate(piece.Geometry, source, SK.SKPathOp.Difference), maximumPieces);
                                    AddOwned(next, sourceIndex, new SK.SKPath(intersection), maximumPieces);
                                    token.ThrowIfCancellationRequested();
                                    var remaining = Operate(uncovered, piece.Geometry, SK.SKPathOp.Difference);
                                    uncovered.Dispose(); uncovered = remaining;
                                }
                            }
                            yield return new SvgPathDivisionProgress(sourceIndex, paths.Count, next.Count);
                        }
                        var tail = uncovered; uncovered = null;
                        AddOwned(next, sourceIndex, tail, maximumPieces);
                        DisposePieces(pieces);
                        pieces = next;
                        adopted = true;
                    }
                    finally
                    {
                        uncovered?.Dispose();
                        if (!adopted) DisposePieces(next);
                    }
                }
                yield return new SvgPathDivisionProgress(sourceIndex + 1, paths.Count, pieces.Count);
            }
            token.ThrowIfCancellationRequested();
            complete(pieces);
            transferred = true;
        }
        finally
        {
            if (!transferred) DisposePieces(pieces);
        }
    }

    private static SK.SKPath Operate(SK.SKPath left, SK.SKPath right, SK.SKPathOp operation)
        => left.Op(right, operation) ?? throw new InvalidOperationException("The selected geometry could not be divided.");

    private static void AddOwned(List<SvgPathDivisionPiece> pieces, int sourceIndex, SK.SKPath path, int limit)
    {
        if (path.IsEmpty) { path.Dispose(); return; }
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
        try { pieces.Add(new SvgPathDivisionPiece(sourceIndex, path)); }
        catch { path.Dispose(); throw; }
    }
    private static bool Overlaps(SK.SKRect left, SK.SKRect right)
        => left.Left < right.Right && right.Left < left.Right && left.Top < right.Bottom && right.Top < left.Bottom;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void DisposePieces(List<SvgPathDivisionPiece> pieces)
    { foreach (var piece in pieces) piece.Geometry.Dispose(); }
}
