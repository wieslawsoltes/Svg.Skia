using System.IO;

namespace Svg.Model
{
    public class Image
    {
        public byte[]? Data;
        public float Width; // TODO:
        public float Height; // TODO:

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
