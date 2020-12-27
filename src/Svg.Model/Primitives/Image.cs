using System.IO;

namespace Svg.Model
{
    public class Image
    {
        public byte[]? Data { get; set; }
        public float Width { get; set; } // TODO:
        public float Height { get; set; } // TODO:

        public static Image FromEncodedData(Stream sourceStream)
        {
            using var memoryStream = new MemoryStream();
            sourceStream.CopyTo(memoryStream);
            var data = memoryStream.ToArray();

            return new Image()
            {
                Data = data
            };
        }
    }
}
