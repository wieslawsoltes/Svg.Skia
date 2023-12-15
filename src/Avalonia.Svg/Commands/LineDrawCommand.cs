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

public sealed class LineDrawCommand : DrawCommand
{
    public AM.IPen? Pen { get; }
    public A.Point P1 { get; }
    public A.Point P2 { get; }

    public LineDrawCommand(AM.IPen? pen, A.Point p1, A.Point p2)
    {
        Pen = pen;
        P1 = p1;
        P2 = p2;
    }
}
