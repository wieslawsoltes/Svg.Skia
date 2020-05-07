// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Svg.Skia
{
    public static class CSharpCodeGen
    {
        private static readonly CultureInfo s_provider = CultureInfo.GetCultureInfo("en-GB");

        private static string WriteFloat(this float value) => float.IsNaN(value) ? $"float.NaN" : $"{value.ToString(s_provider)}f";

        private static string WriteBool(this bool value) => value ? "true" : "false";

        public static void Generate(SKMatrix skMatrix, StringBuilder sb, string indent = "", string id = "skMatrix")
        {
            sb.AppendLine($"{indent}var {id} = new {nameof(SKMatrix)}()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.ScaleX)} = {skMatrix.ScaleX.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.SkewY)} = {skMatrix.SkewY.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.SkewX)} = {skMatrix.SkewX.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.ScaleY)} = {skMatrix.ScaleY.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.TransX)} = {skMatrix.TransX.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.TransY)} = {skMatrix.TransY.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.Persp0)} = {0f.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.Persp1)} = {0f.WriteFloat()},");
            sb.AppendLine($"{indent}    {nameof(SKMatrix.Persp2)} = {1f.WriteFloat()}");
            sb.AppendLine($"{indent}}};");
        }

        public static void Generate(SKPath skPath, StringBuilder sb, string indent = "", string id = "skPath")
        {
            sb.AppendLine($"{indent}var {id} = new {nameof(SKPath)}()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {nameof(SKPath.FillType)} = {(skPath.FillType == SKPathFillType.Winding ? $"{nameof(SKPathFillType)}.{nameof(SKPathFillType.Winding)}" : $"{nameof(SKPathFillType)}.{nameof(SKPathFillType.EvenOdd)}")}");
            sb.AppendLine($"{indent}}};");

            using (var rawIterator = skPath.CreateRawIterator())
            {
                var skPoints = new SKPoint[4];
                var skPathVerb = SKPathVerb.Move;
                while ((skPathVerb = rawIterator.Next(skPoints)) != SKPathVerb.Done)
                {
                    switch (skPathVerb)
                    {
                        case SKPathVerb.Move:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.MoveTo)}({skPoints[0].X.WriteFloat()}, {skPoints[0].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Line:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.LineTo)}({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Cubic:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.CubicTo)}({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()}, {skPoints[3].X.WriteFloat()}, {skPoints[3].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Quad:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.QuadTo)}({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Conic:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.ConicTo)}({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()}, {rawIterator.ConicWeight().WriteFloat()});");
                            break;
                        case SKPathVerb.Close:
                            sb.AppendLine($"{indent}{id}.{nameof(SKPath.Close)}();");
                            break;
                        case SKPathVerb.Done:
                        default:
                            break;
                    }
                }
            }
        }

        public static void Generate(SKPaint skPaint, StringBuilder sb, string indent = "", string id = "skPaint")
        {
            sb.AppendLine($"{indent}var {id} = new {nameof(SKPaint)}()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {nameof(SKPaint.IsAntialias)} = {skPaint.IsAntialias.WriteBool()},");
            sb.AppendLine($"{indent}}};");
        }
    }
}
