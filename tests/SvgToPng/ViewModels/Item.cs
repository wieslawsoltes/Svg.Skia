using System.Runtime.Serialization;
using SkiaSharp;
using Svg.Skia;

namespace SvgToPng.ViewModels
{
    [DataContract]
    public class Item
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string SvgPath { get; set; }

        [DataMember]
        public string ReferencePngPath { get; set; }

        [DataMember]
        public string OutputPngPath { get; set; }

        [IgnoreDataMember]
        public SKSvg Svg { get; set; }

        [IgnoreDataMember]
        public SKBitmap ReferencePng { get; set; }

        [IgnoreDataMember]
        public SKBitmap PixelDiff { get; set; }
    }
}
