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
using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class RectangleDrawCommand : DrawCommand
{
    public AM.IBrush? Brush { get; }
    public AM.IPen? Pen { get; }
    public A.Rect Rect { get; }
    public double RadiusX { get; }
    public double RadiusY { get; }

    public RectangleDrawCommand(AM.IBrush? brush, AM.IPen? pen, A.Rect rect, double radiusX, double radiusY)
    {
        Brush = brush;
        Pen = pen;
        Rect = rect;
        RadiusX = radiusX;
        RadiusY = radiusY;
    }
}
