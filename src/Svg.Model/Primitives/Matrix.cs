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
    }
}
