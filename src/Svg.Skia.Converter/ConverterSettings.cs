// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.IO;

namespace Svg.Skia.Converter
{
    public class ConverterSettings
    {
        public FileInfo[] Files { get; set; }
        public DirectoryInfo[] Directories { get; set; }
        public DirectoryInfo Output { get; set; }
        public string Pattern { get; set; }
        public string Format { get; set; }
        public int Quality { get; set; }
        public string Background { get; set; }
        public float Scale { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public bool Debug { get; set; }
        public bool Quiet { get; set; }
    }
}
