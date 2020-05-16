using System;

namespace Svg.Model
{
    public struct Matrix
    {
        public float ScaleX;
        public float SkewX;
        public float TransX;
        public float ScaleY;
        public float SkewY;
        public float TransY;
        public float Persp0;
        public float Persp1;
        public float Persp2;

        public readonly static Matrix Empty;

        public readonly static Matrix Identity = new Matrix { ScaleX = 1, ScaleY = 1, Persp2 = 1 };

        public static Matrix CreateIdentity()
        {
            return new Matrix { ScaleX = 1, ScaleY = 1, Persp2 = 1 };
        }

        public static Matrix CreateRotationDegrees(float degrees)
        {
            throw new NotImplementedException(); // TODO:
        }

        public static Matrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
        {
            throw new NotImplementedException(); // TODO:
        }

        public static Matrix CreateScale(float x, float y)
        {
            throw new NotImplementedException(); // TODO:
        }

        public static Matrix CreateSkew(float x, float y)
        {
            throw new NotImplementedException(); // TODO:
        }

        public static Matrix CreateTranslation(float x, float y)
        {
            throw new NotImplementedException(); // TODO:
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
            throw new NotImplementedException(); // TODO:
        }
    }
}
