using System;
using System.Runtime.Serialization;
using SkiaSharp;
using Svg;
using Svg.Model.Primitives;

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
        public SvgDocument Document { get; set; }

        [IgnoreDataMember]
        public Drawable Drawable { get; set; }

        [IgnoreDataMember]
        public Picture Picture { get; set; }

        [IgnoreDataMember]
        public string Code { get; set; }
        
        [IgnoreDataMember]
        public SKPicture SkiaPicture { get; set; }

        [IgnoreDataMember]
        public SKBitmap ReferencePng { get; set; }

        [IgnoreDataMember]
        public SKBitmap PixelDiff { get; set; }

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
}
