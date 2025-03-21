using System;

namespace ShimSkiaSharp;

public struct SKMatrix : IEquatable<SKMatrix>
{
    public float ScaleX { get; set; }

    public float SkewX { get; set; }

    public float TransX { get; set; }

    public float ScaleY { get; set; }

    public float SkewY { get; set; }

    public float TransY { get; set; }

    public float Persp0 { get; set; }

    public float Persp1 { get; set; }

    public float Persp2 { get; set; }

    internal const float DegreesToRadians = (float)Math.PI / 180.0f;

    public static readonly SKMatrix Empty;

    public static readonly SKMatrix Identity = new() {ScaleX = 1, ScaleY = 1, Persp2 = 1};

    public bool IsIdentity => Equals(Identity);

    public static SKMatrix CreateIdentity()
    {
        return new() {ScaleX = 1, ScaleY = 1, Persp2 = 1};
    }

    public static SKMatrix CreateTranslation(float x, float y)
    {
        if (x == 0 && y == 0)
        {
            return Identity;
        }

        return new SKMatrix
        {
            ScaleX = 1,
            ScaleY = 1,
            TransX = x,
            TransY = y,
            Persp2 = 1,
        };
    }

    public static SKMatrix CreateScale(float x, float y)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (x == 1 && y == 1)
        {
            return Identity;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        return new SKMatrix
        {
            ScaleX = x,
            ScaleY = y,
            Persp2 = 1,
        };
    }

    public static SKMatrix CreateScale(float x, float y, float pivotX, float pivotY)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (x == 1 && y == 1)
        {
            return Identity;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        var tx = pivotX - x * pivotX;
        var ty = pivotY - y * pivotY;

        return new SKMatrix
        {
            ScaleX = x,
            ScaleY = y,
            TransX = tx,
            TransY = ty,
            Persp2 = 1,
        };
    }

    public static SKMatrix CreateRotation(float radians)
    {
        if (radians == 0)
        {
            return Identity;
        }

        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);

        var matrix = Identity;
        SetSinCos(ref matrix, sin, cos);
        return matrix;
    }

    public static SKMatrix CreateRotation(float radians, float pivotX, float pivotY)
    {
        if (radians == 0)
        {
            return Identity;
        }

        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);

        var matrix = Identity;
        SetSinCos(ref matrix, sin, cos, pivotX, pivotY);
        return matrix;
    }

    public static SKMatrix CreateRotationDegrees(float degrees)
    {
        if (degrees == 0)
        {
            return Identity;
        }

        return CreateRotation(degrees * DegreesToRadians);
    }

    public static SKMatrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
    {
        if (degrees == 0)
        {
            return Identity;
        }

        return CreateRotation(degrees * DegreesToRadians, pivotX, pivotY);
    }

    public static SKMatrix CreateSkew(float x, float y)
    {
        if (x == 0 && y == 0)
        {
            return Identity;
        }

        return new SKMatrix
        {
            ScaleX = 1,
            SkewX = x,
            SkewY = y,
            ScaleY = 1,
            Persp2 = 1,
        };
    }

    private static void SetSinCos(ref SKMatrix matrix, float sin, float cos)
    {
        matrix.ScaleX = cos;
        matrix.SkewX = -sin;
        matrix.TransX = 0;
        matrix.SkewY = sin;
        matrix.ScaleY = cos;
        matrix.TransY = 0;
        matrix.Persp0 = 0;
        matrix.Persp1 = 0;
        matrix.Persp2 = 1;
    }

    private static void SetSinCos(ref SKMatrix matrix, float sin, float cos, float pivotx, float pivoty)
    {
        var oneMinusCos = 1 - cos;
        matrix.ScaleX = cos;
        matrix.SkewX = -sin;
        matrix.TransX = Dot(sin, pivoty, oneMinusCos, pivotx);
        matrix.SkewY = sin;
        matrix.ScaleY = cos;
        matrix.TransY = Dot(-sin, pivotx, oneMinusCos, pivoty);
        matrix.Persp0 = 0;
        matrix.Persp1 = 0;
        matrix.Persp2 = 1;
    }

    private static float Dot(float a, float b, float c, float d)
    {
        return a * b + c * d;
    }

    private static float MulAddMul(float a, float b, float c, float d)
    {
        return (float)(a * (double)b + c * (double)d);
    }

    private static SKMatrix Concat(SKMatrix a, SKMatrix b)
    {
        return new()
        {
            ScaleX = MulAddMul(a.ScaleX, b.ScaleX, a.SkewX, b.SkewY),
            SkewX = MulAddMul(a.ScaleX, b.SkewX, a.SkewX, b.ScaleY),
            TransX = MulAddMul(a.ScaleX, b.TransX, a.SkewX, b.TransY) + a.TransX,
            SkewY = MulAddMul(a.SkewY, b.ScaleX, a.ScaleY, b.SkewY),
            ScaleY = MulAddMul(a.SkewY, b.SkewX, a.ScaleY, b.ScaleY),
            TransY = MulAddMul(a.SkewY, b.TransX, a.ScaleY, b.TransY) + a.TransY,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    public SKMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY, float persp0, float persp1, float persp2)
    {
        ScaleX = scaleX;
        SkewX = skewX;
        TransX = transX;
        SkewY = skewY;
        ScaleY = scaleY;
        TransY = transY;
        Persp0 = persp0;
        Persp1 = persp1;
        Persp2 = persp2;
    }

    public readonly SKMatrix PreConcat(SKMatrix matrix)
    {
        return Concat(this, matrix);
    }

    public readonly SKMatrix PostConcat(SKMatrix matrix)
    {
        return Concat(matrix, this);
    }

    public readonly SKRect MapRect(SKRect source)
    {
        var left = source.Left;
        var top = source.Top;
        var right = source.Right;
        var bottom = source.Bottom;
        // TODO: MapRect
        return new SKRect(
            left * ScaleX + top * SkewX + TransX,
            left * SkewY + top * ScaleY + TransY,
            right * ScaleX + bottom * SkewX + TransX,
            right * SkewY + bottom * ScaleY + TransY);
    }

    public bool Equals(SKMatrix other)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        return ScaleX == other.ScaleX &&
               SkewY == other.SkewY &&
               SkewX == other.SkewX &&
               ScaleY == other.ScaleY &&
               TransX == other.TransX &&
               TransY == other.TransY;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    public override bool Equals(object? obj) => obj is SKMatrix other && Equals(other);

    public override int GetHashCode()
    {
        return ScaleX.GetHashCode() + SkewY.GetHashCode() +
               SkewX.GetHashCode() + ScaleY.GetHashCode() +
               TransX.GetHashCode() + TransY.GetHashCode();
    }

    public static bool operator ==(SKMatrix value1, SKMatrix value2)
    {
        return value1.Equals(value2);
    }

    public static bool operator !=(SKMatrix value1, SKMatrix value2)
    {
        return !value1.Equals(value2);
    }

    public static SKMatrix operator *(SKMatrix value1, SKMatrix value2)
    {
        return value1.PreConcat(value2);
    }

    public override string ToString()
    {
        return FormattableString.Invariant($"{ScaleX}, {SkewX}, {TransX}, {SkewY}, {ScaleY}, {TransY}");
    }
}