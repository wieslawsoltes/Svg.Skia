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
            throw new NotImplementedException();
        }

        public static Matrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
        {
            throw new NotImplementedException();
        }

        public static Matrix CreateScale(float x, float y)
        {
            throw new NotImplementedException();
        }

        public static Matrix CreateTranslation(float x, float y)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public readonly Matrix PostConcat(Matrix matrix)
        {
            throw new NotImplementedException();
        }

        public readonly Rect MapRect(Rect source)
        {
            throw new NotImplementedException();
        }
    }
}
