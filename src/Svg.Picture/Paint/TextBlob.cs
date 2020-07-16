﻿using System;

namespace Svg.Picture
{
    public sealed class TextBlob : IDisposable
    {
        public string? Text { get; set; }
        public Point[]? Points { get; }

        public void Dispose()
        {
        }
    }
}
