// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.IO;

namespace Svg.Skia.Converter
{
    public class ConverterSettings
    {
        public FileInfo[]? Files { get; set; }
        public DirectoryInfo[]? Directories { get; set; }
        public DirectoryInfo? Output { get; set; }
        public string? Pattern { get; set; }
        public string Format { get; set; } = "png";
        public int Quality { get; set; } = 100;
        public string Background { get; set; } = "#00000000";
        public float Scale { get; set; } = 1f;
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;
        public bool Quiet { get; set; }
    }
}
