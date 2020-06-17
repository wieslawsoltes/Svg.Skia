﻿namespace Svg.Picture
{
    public readonly struct Size
    {
        public float Width { get; }
        public float Height { get; }

        public static readonly Size Empty;

        public bool IsEmpty => Width == default && Height == default;

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }
}
