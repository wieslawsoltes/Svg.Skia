// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public static class SKExtensions
    {
        public static SKBitmap? ToBitmap(this SKPicture skPicture, SKColor background, float scaleX, float scaleY)
        {
            float width = skPicture.CullRect.Width * scaleX;
            float height = skPicture.CullRect.Height * scaleY;
            if (width > 0 && height > 0)
            {
                var skImageInfo = new SKImageInfo((int)width, (int)height);
                var skBitmap = new SKBitmap(skImageInfo);
                using (var skCanvas = new SKCanvas(skBitmap))
                {
                    skCanvas.Clear(SKColors.Transparent);
                    if (background != SKColor.Empty)
                    {
                        skCanvas.DrawColor(background);
                    }
                    skCanvas.Save();
                    skCanvas.Scale(scaleX, scaleY);
                    skCanvas.DrawPicture(skPicture);
                    skCanvas.Restore();
                    return skBitmap;
                }
            }
            return null;
        }
    }
}
