using System;
using System.Runtime.Serialization;
using ShimSkiaSharp;
using Svg;

namespace SvgToPng.ViewModels;

[DataContract]
public class Item : IDisposable
{
    [DataMember]
    public string Name { get; set; }

    [DataMember]
    public bool Passed { get; set; }

    [DataMember]
    public string SvgPath { get; set; }

    [DataMember]
    public string ReferencePngPath { get; set; }

    [IgnoreDataMember]
    public SvgDocument Document { get; set; }

    [IgnoreDataMember]
    public SKDrawable Drawable { get; set; }

    [IgnoreDataMember]
    public SKPicture Picture { get; set; }

    [IgnoreDataMember]
    public string Code { get; set; }

    [IgnoreDataMember]
    public SkiaSharp.SKPicture SkiaPicture { get; set; }

    [IgnoreDataMember]
    public SkiaSharp.SKBitmap ReferencePng { get; set; }

    [IgnoreDataMember]
    public SkiaSharp.SKBitmap PixelDiff { get; set; }

    public void Reset()
    {
        Document = null;
        Drawable = null;
        Picture = null;
        Code = null;
        SkiaPicture?.Dispose();
        SkiaPicture = null;
        ReferencePng?.Dispose();
        ReferencePng = null;
        PixelDiff?.Dispose();
        PixelDiff = null;
    }

    public void Dispose()
    {
        Reset();
    }
}
