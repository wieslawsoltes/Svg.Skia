using System.IO;

namespace ShimSkiaSharp
{
    public class SKImage
    {
        public byte[]? Data { get; set; }

        public float Width { get; set; }

        public float Height { get; set; }

        public static byte[] FromStream(Stream sourceStream)
        {
            using var memoryStream = new MemoryStream();
            sourceStream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
