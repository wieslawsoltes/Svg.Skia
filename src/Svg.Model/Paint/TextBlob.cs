using System;

namespace Svg.Model
{
    public sealed class TextBlob : IDisposable
    {
        public string? Text { get; set; }
        public Point[]? Points { get; set; }

        public void Dispose()
        {
        }
    }
}
