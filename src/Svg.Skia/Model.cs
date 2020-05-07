using System.Collections.Generic;

namespace Svg.Skia.Model
{
    public enum PaintStyle
    {
        Fill = 0,
        Stroke = 1,
        StrokeAndFill = 2
    }

    public struct Color
    {
        public byte Green { get; }
        public byte Red { get; }
        public byte Alpha { get; }
        public byte Blue { get; }
    }

    public enum StrokeJoin
    {
        Miter = 0,
        Round = 1,
        Bevel = 2
    }

    public enum ShaderTileMode
    {
        Clamp = 0,
        Repeat = 1,
        Mirror = 2
    }

    public abstract class Shader
    {
    }

    public class ColorShader : Shader
    {
        public Color Color { get; set; }
    }

    public class LinearGradientShader : Shader
    {
        public Point Start { get; set; }
        public Point End { get; set; }
        public Color[]? Colors { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }

    public class TwoPointConicalGradientShader : Shader
    {
        public Point Start { get; set; }
        public float StartRadius { get; set; }
        public Point End { get; set; }
        public float EndRadius { get; set; }
        public Color[]? Colors { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }

    public class PictureShader : Shader
    {
        public Picture? Src { get; set; }
        public ShaderTileMode TmX { get; set; }
        public ShaderTileMode TmY { get; set; }
        public Matrix LocalMatrix { get; set; }
        public Rect Tile { get; set; }
    }

    public class PerlinNoiseFractalNoiseShader : Shader
    {
        public float BaseFrequencyX { get; set; }
        public float BaseFrequencyY { get; set; }
        public int NumOctaves { get; set; }
        public float Seed { get; set; }
        public PointI TileSize { get; set; }
    }

    public class PerlinNoiseTurbulenceShader : Shader
    {
        public float BaseFrequencyX { get; set; }
        public float BaseFrequencyY { get; set; }
        public int NumOctaves { get; set; }
        public float Seed { get; set; }
        public PointI TileSize { get; set; }
    }

    public abstract class ColorFilter
    {
    }

    public abstract class ImageFilter
    {
    }

    public enum BlendMode
    {
        Clear = 0,
        Src = 1,
        Dst = 2,
        SrcOver = 3,
        DstOver = 4,
        SrcIn = 5,
        DstIn = 6,
        SrcOut = 7,
        DstOut = 8,
        SrcATop = 9,
        DstATop = 10,
        Xor = 11,
        Plus = 12,
        Modulate = 13,
        Screen = 14,
        Overlay = 15,
        Darken = 16,
        Lighten = 17,
        ColorDodge = 18,
        ColorBurn = 19,
        HardLight = 20,
        SoftLight = 21,
        Difference = 22,
        Exclusion = 23,
        Multiply = 24,
        Hue = 25,
        Saturation = 26,
        Color = 27,
        Luminosity = 28
    }

    public enum FilterQuality
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum FontStyleSlant
    {
        Upright = 0,
        Italic = 1,
        Oblique = 2
    }

    public class FontStyle
    {
        public int Weight { get; }
        public int Width { get; }
        public FontStyleSlant Slant { get; }
    }

    public class Typeface
    {
        public string? FamilyName { get; }
        public FontStyle? FontStyle { get; }
        public int FontWidth { get; }
        public bool IsBold { get; }
        public bool IsItalic { get; }
        public FontStyleSlant FontSlant { get; }
    }

    public enum TextAlign
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public abstract class PathEffect
    {
    }

    public class DashPathEffect : PathEffect
    {
        public float[]? Intervals { get; set; }
        public float Phase { get; set; }
    }

    public enum StrokeCap
    {
        Butt = 0,
        Round = 1,
        Square = 2
    }

    public class Paint
    {
        public PaintStyle Style { get; set; }
        public Color? Color { get; set; }
        public float StrokeWidth { get; set; }
        public bool IsAntialias { get; set; }
        public float StrokeMiter { get; set; }
        public StrokeJoin StrokeJoin { get; set; }
        public Shader? Shader { get; set; }
        public ColorFilter? ColorFilter { get; set; }
        public ImageFilter? ImageFilter { get; set; }
        public BlendMode BlendMode { get; set; }
        public FilterQuality FilterQuality { get; set; }
        public Typeface? Typeface { get; set; }
        public float TextSize { get; set; }
        public TextAlign TextAlign { get; set; }
        public PathEffect? PathEffect { get; set; }
        public StrokeCap StrokeCap { get; set; }
    }

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
    }

    public struct Rect
    {
        public float Left { get; set; }
        public float Top { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }
    }

    public abstract class PictureCommand
    {
    }

    public class SaveLayerCommand : PictureCommand
    {
        public Paint? Paint { get; set; }
    }

    public class SetMatrixCommand : PictureCommand
    {
        public Matrix? Matrix { get; set; }
    }
 
    public class RestorePictureCommand : PictureCommand
    {
    }

    public enum ClipOperation
    {
        Difference = 0,
        Intersect = 1
    }

    public class ClipRectPictureCommand : PictureCommand
    {
        public Rect? Rect { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }
    }

    public class ClipPathPictureCommand : PictureCommand
    {
        public Path? Path { get; set; }
        public ClipOperation Operation { get; set; }
        public bool Antialias { get; set; }
    }

    public class DrawPathPictureCommand : PictureCommand
    {
        public Path? Path { get; set; }
        public Paint? Paint { get; set; }
    }

    public class Picture
    {
        public Rect CullRect { get; set; }
        public IList<PictureCommand>? Commands { get; set; }
    }

    public struct Point
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public struct PointI
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public enum PathFillType
    {
        Winding = 0,
        EvenOdd = 1
    }

    public abstract class PathCommand
    {
    }

    public class MoveToPathCommand : PathCommand
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class LineToPathCommand : PathCommand
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class CubicToPathCommand : PathCommand
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    public class QuadToPathCommand : PathCommand
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
    }

    public enum PathArcSize
    {
        Small = 0,
        Large = 1
    }

    public enum PathDirection
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    public class ArcToPathCommand : PathCommand
    {
        public double RX { get; set; }
        public double RY { get; set; }
        public double XAxisRotate { get; set; }
        public PathArcSize LargeArc { get; set; }
        public PathDirection Sweep { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ClosePathCommand : PathCommand
    {
    }

    public class AddPolyPathCommand : PathCommand
    {
        public IList<Point>? Points { get; set; }
        public bool Close { get; set; }
    }

    public class AddRoundRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
        public double RX { get; set; }
        public double RY { get; set; }
    }

    public class AddRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
    }

    public class AddCirclePathCommand : PathCommand
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; }
    }

    public class AddOvalPathCommand : PathCommand
    {
        public Rect Rect { get; set; }
    }

    public class Path
    {
        public PathFillType FillType { get; set; }
        public IList<PathCommand>? Commands { get; set; }
    }
}
