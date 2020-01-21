using System;
using System.Runtime.Serialization;
using SkiaSharp;
using Svg;
using Svg.Skia;

namespace SvgToPng.ViewModels
{
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
        public SvgDocument Svg { get; set; }

        [IgnoreDataMember]
        public SKPicture Picture { get; set; }

        [IgnoreDataMember]
        public Drawable Drawable { get; set; }

        [IgnoreDataMember]
        public SKBitmap ReferencePng { get; set; }

        [IgnoreDataMember]
        public SKBitmap PixelDiff { get; set; }

        public void Reset()
        {
            Svg = null;
            Picture?.Dispose();
            Picture = null;
            Drawable?.Dispose();
            Drawable = null;
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
}
