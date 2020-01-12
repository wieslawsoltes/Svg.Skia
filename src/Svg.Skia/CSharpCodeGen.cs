// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Text;
using System.Globalization;
using SkiaSharp;

namespace Svg.Skia
{
    public static class CSharpCodeGen
    {
        private static CultureInfo s_provider = CultureInfo.GetCultureInfo("en-GB");

        private static string WriteFloat(this float value) => float.IsNaN(value) ? "float.NaN" : $"{value.ToString(s_provider)}f";

        private static string WriteBool(this bool value) => value ? "true" : "false";

        public static void Generate(SKMatrix skMatrix, StringBuilder sb, string id = "skMatrix")
        {
            sb.AppendLine($"var {id} = new SKMatrix()");
            sb.AppendLine($"{{");
            sb.AppendLine($"    ScaleX = {skMatrix.ScaleX.WriteFloat()},");
            sb.AppendLine($"    SkewY = {skMatrix.SkewY.WriteFloat()},");
            sb.AppendLine($"    SkewX = {skMatrix.SkewX.WriteFloat()},");
            sb.AppendLine($"    ScaleY = {skMatrix.ScaleY.WriteFloat()},");
            sb.AppendLine($"    TransX = {skMatrix.TransX.WriteFloat()},");
            sb.AppendLine($"    TransY = {skMatrix.TransY.WriteFloat()},");
            sb.AppendLine($"    Persp0 = 0f,");
            sb.AppendLine($"    Persp1 = 0f,");
            sb.AppendLine($"    Persp2 = 1f");
            sb.AppendLine($"}};");
        }

        public static void Generate(SKPath skPath, StringBuilder sb, string id = "skPath")
        {
            sb.AppendLine($"var {id} = new SKPath()");
            sb.AppendLine($"{{");
            sb.AppendLine($"    FillType = {(skPath.FillType == SKPathFillType.Winding ? "SKPathFillType.Winding" : "SKPathFillType.EvenOdd")}");
            sb.AppendLine($"}};");

            using (var rawIterator = skPath.CreateRawIterator())
            {
                var skPoints = new SKPoint[4];
                var skPathVerb = SKPathVerb.Move;
                while ((skPathVerb = rawIterator.Next(skPoints)) != SKPathVerb.Done)
                {
                    switch (skPathVerb)
                    {
                        case SKPathVerb.Move:
                            sb.AppendLine($"{id}.MoveTo({skPoints[0].X.WriteFloat()}, {skPoints[0].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Line:
                            sb.AppendLine($"{id}.LineTo({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Cubic:
                            sb.AppendLine($"{id}.CubicTo({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()}, {skPoints[3].X.WriteFloat()}, {skPoints[3].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Quad:
                            sb.AppendLine($"{id}.QuadTo({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()});");
                            break;
                        case SKPathVerb.Conic:
                            sb.AppendLine($"{id}.ConicTo({skPoints[1].X.WriteFloat()}, {skPoints[1].Y.WriteFloat()}, {skPoints[2].X.WriteFloat()}, {skPoints[2].Y.WriteFloat()}, {rawIterator.ConicWeight().WriteFloat()});");
                            break;
                        case SKPathVerb.Close:
                            sb.AppendLine($"{id}.Close();");
                            break;
                        case SKPathVerb.Done:
                        default:
                            break;
                    }
                }
            }
        }
    }
}
