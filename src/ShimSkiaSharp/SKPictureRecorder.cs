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
using System.Collections.Generic;

namespace ShimSkiaSharp;

public sealed class SKPictureRecorder
{
    public SKRect CullRect { get; private set; }

    public SKCanvas? RecordingCanvas { get; private set; }

    public SKCanvas BeginRecording(SKRect cullRect)
    {
        CullRect = cullRect;

        RecordingCanvas = new SKCanvas(new List<CanvasCommand>(), SKMatrix.Identity);

        return RecordingCanvas;
    }

    public SKPicture EndRecording()
    {
        var picture = new SKPicture(CullRect, RecordingCanvas?.Commands);

        CullRect = SKRect.Empty;
        RecordingCanvas = null;

        return picture;
    }
}
