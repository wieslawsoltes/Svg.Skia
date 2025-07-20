namespace Svg.Skia;

public static class SKDocumentFactory
{
    public static SkiaSharp.SKDocument CreatePdf(SkiaSharp.SKWStream stream)
    {
        return SkiaSharp.SKDocument.CreatePdf(stream, SkiaSharp.SKDocument.DefaultRasterDpi);
    }

    public static SkiaSharp.SKDocument CreatePdf(System.IO.Stream stream)
    {
        return SkiaSharp.SKDocument.CreatePdf(stream, SkiaSharp.SKDocument.DefaultRasterDpi);
    }

    public static SkiaSharp.SKDocument CreatePdf(SkiaSharp.SKWStream stream, SKSvgSettings? settings)
    {
        return CreatePdf(stream);
    }

    public static SkiaSharp.SKDocument CreatePdf(System.IO.Stream stream, SKSvgSettings? settings)
    {
        return CreatePdf(stream);
    }

    public static SkiaSharp.SKDocument CreatePdf(string path)
    {
        var stream = new SkiaSharp.SKFileWStream(path);
        return CreatePdf(stream);
    }

    public static SkiaSharp.SKDocument CreatePdf(string path, SKSvgSettings? settings)
    {
        return CreatePdf(path);
    }
}
