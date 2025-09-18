using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKMatrixTests
{
    [Fact]
    public void Identity_IsIdentity()
    {
        var m = SKMatrix.Identity;
        Assert.True(m.IsIdentity);
    }

    [Fact]
    public void CreateTranslation_Works()
    {
        var m = SKMatrix.CreateTranslation(5, 7);
        Assert.Equal(5, m.TransX);
        Assert.Equal(7, m.TransY);
        Assert.Equal(1, m.ScaleX);
        Assert.Equal(1, m.ScaleY);
    }

    [Fact]
    public void CreateScale_WithPivot_Works()
    {
        var m = SKMatrix.CreateScale(2, 3, 10, 20);
        Assert.Equal(2, m.ScaleX);
        Assert.Equal(3, m.ScaleY);
        Assert.Equal(10 - 2 * 10, m.TransX);
        Assert.Equal(20 - 3 * 20, m.TransY);
    }

    [Fact]
    public void CreateRotationDegrees_Works()
    {
        var m = SKMatrix.CreateRotationDegrees(90);
        // sin=1 cos=0
        Assert.Equal(0, m.ScaleX, 6);
        Assert.Equal(-1, m.SkewX, 6);
        Assert.Equal(1, m.SkewY, 6);
        Assert.Equal(0, m.ScaleY, 6);
    }

    [Fact]
    public void Multiply_Matrices_Works()
    {
        var scale = SKMatrix.CreateScale(2, 2);
        var trans = SKMatrix.CreateTranslation(5, 5);
        var result = scale * trans;
        // order: scale then translate
        Assert.Equal(2, result.ScaleX);
        Assert.Equal(2, result.ScaleY);
        Assert.Equal(10, result.TransX); // scaleX*trans.TransX + SkewX*trans.TransY = 2*5 + 0*5 =10
        Assert.Equal(10, result.TransY); // SkewY*trans.TransX + ScaleY*trans.TransY = 0*5 + 2*5 = 10
    }

    [Fact]
    public void TryInvert_InvertsMatrix()
    {
        var m = SKMatrix.CreateScale(2, 3) * SKMatrix.CreateTranslation(5, 7);
        Assert.True(m.TryInvert(out var inv));
        var identity = m * inv;
        Assert.True(identity.IsIdentity);
    }
}
