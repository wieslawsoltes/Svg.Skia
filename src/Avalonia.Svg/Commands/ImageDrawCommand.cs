/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Media.Imaging;

namespace Avalonia.Svg.Commands;

public sealed class ImageDrawCommand : DrawCommand
{
    public AM.IImage? Source { get; }
    public A.Rect SourceRect { get; }
    public A.Rect DestRect { get; }
    public AVMI.BitmapInterpolationMode BitmapInterpolationMode { get; }

    public ImageDrawCommand(AM.IImage? source, A.Rect sourceRect, A.Rect destRect, AVMI.BitmapInterpolationMode bitmapInterpolationMode)
    {
        Source = source;
        SourceRect = sourceRect;
        DestRect = destRect;
        BitmapInterpolationMode = bitmapInterpolationMode;
    }

    public override void Dispose()
    {
        base.Dispose();

        if (Source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
