using System;
using System.Linq;
using System.Threading;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgPathDivisionServiceTests
{
    [Fact]
    public void OverlappingRectanglesProduceThreeDisjointCells()
    {
        using var a = Rectangle(0, 0, 20, 20);
        using var b = Rectangle(10, 0, 20, 20);
        var beforeA = a.ToSvgPathData();
        var beforeB = b.ToSvgPathData();
        using var result = SvgPathDivisionService.Divide(new[] { a, b });
        Assert.Equal(3, result.Pieces.Count);
        Assert.Equal(0, OwnerAt(result, 5, 10));
        Assert.Equal(1, OwnerAt(result, 15, 10));
        Assert.Equal(1, OwnerAt(result, 25, 10));
        AssertCoverage(new[] { a, b }, result, -5, -5, 40, 30);
        Assert.Equal(beforeA, a.ToSvgPathData());
        Assert.Equal(beforeB, b.ToSvgPathData());
    }

    [Fact]
    public void CoincidentOperandsHaveOneFrontmostCell()
    {
        using var a = Rectangle(0, 0, 20, 20);
        using var b = Rectangle(0, 0, 20, 20);
        using var result = SvgPathDivisionService.Divide(new[] { a, b });
        Assert.Equal(1, Assert.Single(result.Pieces).SourceIndex);
        AssertCoverage(new[] { a, b }, result, -5, -5, 30, 30);
    }

    [Fact]
    public void ContainmentRetainsTheSurroundingHole()
    {
        using var a = Rectangle(0, 0, 30, 30);
        using var b = Rectangle(10, 10, 10, 10);
        using var result = SvgPathDivisionService.Divide(new[] { a, b });
        Assert.Equal(2, result.Pieces.Count);
        var outside = Assert.Single(result.Pieces.Where(p => p.SourceIndex == 0));
        Assert.True(outside.Geometry.Contains(5, 5));
        Assert.False(outside.Geometry.Contains(15, 15));
        AssertCoverage(new[] { a, b }, result, -5, -5, 40, 40);
    }

    [Fact]
    public void ThreeOperandsPreserveEveryBoundaryAndFrontmostOwner()
    {
        using var a = Rectangle(0, 0, 30, 30);
        using var b = Rectangle(10, -10, 10, 50);
        using var c = Rectangle(-10, 10, 50, 10);
        using var result = SvgPathDivisionService.Divide(new[] { a, b, c });
        Assert.Equal(2, OwnerAt(result, 15, 15));
        Assert.Equal(1, OwnerAt(result, 15, 5));
        Assert.Equal(0, OwnerAt(result, 5, 5));
        AssertCoverage(new[] { a, b, c }, result, -15, -15, 60, 60);
    }

    [Fact]
    public void EvenOddOperandsPreserveTheirUnpaintedHoles()
    {
        using var ring = Rectangle(0, 0, 30, 30);
        ring.AddRect(SKRect.Create(5, 5, 20, 20));
        ring.FillType = SKPathFillType.EvenOdd;
        using var bar = Rectangle(12, -5, 6, 40);
        using var result = SvgPathDivisionService.Divide(new[] { ring, bar });
        Assert.Equal(-1, OwnerAt(result, 10, 15));
        Assert.Equal(1, OwnerAt(result, 15, 15));
        AssertCoverage(new[] { ring, bar }, result, -5, -10, 40, 50);
        Assert.Equal(SKPathFillType.EvenOdd, ring.FillType);
    }

    [Fact]
    public void TouchingEdgesDoNotCreateZeroAreaPieces()
    {
        using var a = Rectangle(0, 0, 10, 10);
        using var b = Rectangle(10, 0, 10, 10);
        using var c = Rectangle(20, 10, 10, 10);
        using var result = SvgPathDivisionService.Divide(new[] { a, b, c });
        Assert.Equal(3, result.Pieces.Count);
        Assert.All(result.Pieces, piece => Assert.False(piece.Geometry.IsEmpty));
    }

    [Fact]
    public void CubicAndConicGeometryIsNotPolygonized()
    {
        using var a = new SKPath();
        a.AddCircle(20, 20, 15);
        using var b = new SKPath();
        b.MoveTo(10, 0);
        b.CubicTo(45, 0, 45, 40, 10, 40);
        b.Close();
        using var result = SvgPathDivisionService.Divide(new[] { a, b });
        AssertCoverage(new[] { a, b }, result, 0, 0, 50, 45);
        Assert.Contains(result.Pieces, piece => HasCurve(piece.Geometry));
    }

    [Fact]
    public void EmptyInputsAndEmptyOperandsAreSupported()
    {
        using var emptyResult = SvgPathDivisionService.Divide(Array.Empty<SKPath>());
        Assert.Empty(emptyResult.Pieces);
        using var empty = new SKPath();
        using var shape = Rectangle(0, 0, 10, 10);
        using var result = SvgPathDivisionService.Divide(new[] { empty, shape, empty });
        Assert.Equal(1, Assert.Single(result.Pieces).SourceIndex);
    }

    [Fact]
    public void BudgetFailureDoesNotModifyInputAndNextCallStillSucceeds()
    {
        using var a = Rectangle(0, 0, 20, 20);
        using var b = Rectangle(10, 0, 20, 20);
        var before = a.ToSvgPathData();
        Assert.Throws<InvalidOperationException>(() => SvgPathDivisionService.Divide(new[] { a, b }, 2));
        Assert.Equal(before, a.ToSvgPathData());
        using var result = SvgPathDivisionService.Divide(new[] { a, b }, 3);
        Assert.Equal(3, result.Pieces.Count);
    }

    [Fact]
    public void CancellationIsReportedBeforeNativeWork()
    {
        using var shape = Rectangle(0, 0, 10, 10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            SvgPathDivisionService.Divide(new[] { shape }, cancellationToken: cancellation.Token));
        Assert.True(shape.Contains(5, 5));
    }

    [Fact]
    public void InverseFilledOperandsAndInvalidLimitsAreRejected()
    {
        using var shape = Rectangle(0, 0, 10, 10);
        shape.FillType = SKPathFillType.InverseWinding;
        Assert.Throws<ArgumentException>(() => SvgPathDivisionService.Divide(new[] { shape }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgPathDivisionService.Divide(new[] { shape }, 0));
        Assert.Throws<ArgumentException>(() =>
            SvgPathDivisionService.Divide(Enumerable.Repeat(shape, SvgPathDivisionService.MaximumOperands + 1).ToArray()));
    }

    [Fact]
    public void ResultDisposalIsIdempotentAndDoesNotOwnInputs()
    {
        using var shape = Rectangle(0, 0, 10, 10);
        var result = SvgPathDivisionService.Divide(new[] { shape });
        result.Dispose();
        result.Dispose();
        Assert.True(shape.Contains(5, 5));
    }

    private static SKPath Rectangle(float x, float y, float width, float height)
    {
        var path = new SKPath();
        path.AddRect(SKRect.Create(x, y, width, height));
        return path;
    }

    private static int OwnerAt(SvgPathDivisionResult result, float x, float y)
    {
        var hits = result.Pieces.Where(piece => piece.Geometry.Contains(x, y)).ToArray();
        Assert.True(hits.Length <= 1, $"Overlapping cells at ({x}, {y}).");
        return hits.Length == 0 ? -1 : hits[0].SourceIndex;
    }

    private static void AssertCoverage(SKPath[] inputs, SvgPathDivisionResult result,
        int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                // Avoid integer-aligned boundaries; compare coverage and paint ownership,
                // not raster antialiasing differences between separate path draws.
                var px = column + 0.371f;
                var py = row + 0.613f;
                var expected = -1;
                for (var index = 0; index < inputs.Length; index++)
                    if (inputs[index].Contains(px, py))
                        expected = index;
                Assert.Equal(expected, OwnerAt(result, px, py));
            }
        }
    }

    private static bool HasCurve(SKPath path)
    {
        using var iterator = path.CreateRawIterator();
        var points = new SKPoint[4];
        for (var verb = iterator.Next(points); verb != SKPathVerb.Done; verb = iterator.Next(points))
            if (verb is SKPathVerb.Conic or SKPathVerb.Quad or SKPathVerb.Cubic)
                return true;
        return false;
    }
}
