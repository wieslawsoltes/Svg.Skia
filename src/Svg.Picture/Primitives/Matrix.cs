using System;

namespace Svg.Picture
{
    public struct Matrix
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

        public static readonly Matrix Empty;

        public readonly static Matrix Identity = new Matrix { ScaleX = 1, ScaleY = 1, Persp2 = 1 };

        public static Matrix CreateIdentity()
        {
            return new Matrix { ScaleX = 1, ScaleY = 1, Persp2 = 1 };
        }

        public static Matrix CreateTranslation(float x, float y)
        {
            if (x == 0 && y == 0)
            {
                return Identity;
            }

            return new Matrix
            {
                ScaleX = 1,
                ScaleY = 1,
                TransX = x,
                TransY = y,
                Persp2 = 1,
            };
        }

        public static Matrix CreateScale(float x, float y)
        {
            if (x == 1 && y == 1)
            {
                return Identity;
            }

            return new Matrix
            {
                ScaleX = x,
                ScaleY = y,
                Persp2 = 1,
            };
        }

        public static Matrix CreateScale(float x, float y, float pivotX, float pivotY)
        {
            if (x == 1 && y == 1)
            {
                return Identity;
            }

            var tx = pivotX - x * pivotX;
            var ty = pivotY - y * pivotY;

            return new Matrix
            {
                ScaleX = x,
                ScaleY = y,
                TransX = tx,
                TransY = ty,
                Persp2 = 1,
            };
        }

        public static Matrix CreateRotation(float radians)
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

        public static Matrix CreateRotation(float radians, float pivotX, float pivotY)
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

        public static Matrix CreateRotationDegrees(float degrees)
        {
            if (degrees == 0)
            {
                return Identity;
            }

            return CreateRotation(degrees * DegreesToRadians);
        }

        public static Matrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
        {
            if (degrees == 0)
            {
                return Identity;
            }

            return CreateRotation(degrees * DegreesToRadians, pivotX, pivotY);
        }

        public static Matrix CreateSkew(float x, float y)
        {
            if (x == 0 && y == 0)
            {
                return Identity;
            }

            return new Matrix
            {
                ScaleX = 1,
                SkewX = x,
                SkewY = y,
                ScaleY = 1,
                Persp2 = 1,
            };
        }

        private static void SetSinCos(ref Matrix matrix, float sin, float cos)
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

        private static void SetSinCos(ref Matrix matrix, float sin, float cos, float pivotx, float pivoty)
        {
            float oneMinusCos = 1 - cos;
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
            return (float)((double)a * (double)b + (double)c * (double)d);
        }

        private static Matrix Concat(Matrix a, Matrix b)
        {
            return new Matrix
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

        public Matrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY, float persp0, float persp1, float persp2)
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

        public readonly Matrix PreConcat(Matrix matrix)
        {
            return Concat(this, matrix);
        }

        public readonly Matrix PostConcat(Matrix matrix)
        {
            return Concat(matrix, this);
        }

        public readonly Rect MapRect(Rect source)
        {
            return source; // TODO:
        }
    }
}
