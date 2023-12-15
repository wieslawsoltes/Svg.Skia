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
using System.IO;

namespace Svg.Skia.Converter;

public class Settings
{
    public FileInfo[]? InputFiles { get; set; }
    public DirectoryInfo? InputDirectory { get; set; }
    public FileInfo[]? OutputFiles { get; set; }
    public DirectoryInfo? OutputDirectory { get; set; }
    public string? Pattern { get; set; }
    public string Format { get; set; } = "png";
    public int Quality { get; set; } = 100;
    public string Background { get; set; } = "#00FFFFFF";
    public float Scale { get; set; } = 1f;
    public float ScaleX { get; set; } = 1f;
    public float ScaleY { get; set; } = 1f;
    public string? SystemLanguage { get; set; }
    public bool Quiet { get; set; }
}
