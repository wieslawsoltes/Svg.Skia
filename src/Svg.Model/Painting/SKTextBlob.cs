using Svg.Model.Primitives;

namespace Svg.Model.Painting
{
    public sealed class SKTextBlob
    {
        public string? Text { get; private set; }
        public SKPoint[]? Points { get; private set; }

        private SKTextBlob()
        {
        }

        public static SKTextBlob CreatePositioned(string? text, SKPoint[]? points)
        {
            return new SKTextBlob() {Text = text, Points = points};
        }
    }
}
