// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using Svg;

namespace Svg.Skia.Converter
{
    public class Converter
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }

        public static void Error(Exception ex)
        {
            Log($"{ex.Message}");
            Log($"{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Error(ex.InnerException);
            }
        }

        public static bool Save(FileInfo path, DirectoryInfo output, string format, int quality, string background, float scale, float scaleX, float scaleY, bool debug, bool quiet, int i)
        {
            try
            {
                if (quiet == false)
                {
                    Log($"[{i}] File: {path}");
                }

                var extension = path.Extension;
                string imagePath = path.FullName.Remove(path.FullName.Length - extension.Length) + "." + format.ToLower();
                if (!string.IsNullOrEmpty(output.FullName))
                {
                    imagePath = Path.Combine(output.FullName, Path.GetFileName(imagePath));
                }

                using (var svg = new Svg())
                {
                    if (svg.Load(path.FullName) != null)
                    {
                        if (debug == true && svg.Document != null)
                        {
                            string ymlPath = path.FullName.Remove(path.FullName.Length - extension.Length) + ".yml";
                            if (!string.IsNullOrEmpty(output.FullName))
                            {
                                ymlPath = Path.Combine(output.FullName, Path.GetFileName(ymlPath));
                            }
                            SvgDebug.Print(svg.Document, ymlPath);
                        }

                        if (Enum.TryParse<SKEncodedImageFormat>(format, true, out var skEncodedImageFormat))
                        {
                            if (SKColor.TryParse(background, out var skBackgroundColor))
                            {
                                if (scale != 1f)
                                {
                                    svg.Save(imagePath, skBackgroundColor, skEncodedImageFormat, quality, scale, scale);
                                }
                                else
                                {
                                    svg.Save(imagePath, skBackgroundColor, skEncodedImageFormat, quality, scaleX, scaleY);
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Invalid output image background.", nameof(background));
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid output image format.", nameof(format));
                        }
                    }
                }

                if (quiet == false)
                {
                    Log($"[{i}] Success: {imagePath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                if (quiet == false)
                {
                    Log($"[{i}] Error: {path}");
                    Error(ex);
                }
            }

            return false;
        }

        public static void GetFiles(DirectoryInfo directory, string pattern, List<FileInfo> paths)
        {
            var files = Directory.EnumerateFiles(directory.FullName, pattern);
            if (files != null)
            {
                foreach (var path in files)
                {
                    paths.Add(new FileInfo(path));
                }
            }
        }

        public static void Convert(ConverterSettings settings)
        {
            try
            {
                var paths = new List<FileInfo>();

                if (settings.Files != null)
                {
                    foreach (var file in settings.Files)
                    {
                        paths.Add(file);
                    }
                }

                if (settings.Directories != null)
                {
                    foreach (var directory in settings.Directories)
                    {
                        if (settings.Pattern == null)
                        {
                            GetFiles(directory, "*.svg", paths);
                            GetFiles(directory, "*.svgz", paths);
                        }
                        else
                        {
                            GetFiles(directory, settings.Pattern, paths);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(settings.Output.FullName))
                {
                    if (!Directory.Exists(settings.Output.FullName))
                    {
                        Directory.CreateDirectory(settings.Output.FullName);
                    }
                }

                var sw = Stopwatch.StartNew();

                int processed = 0;

                for (int i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];

                    if (Save(path, settings.Output, settings.Format, settings.Quality, settings.Background, settings.Scale, settings.ScaleX, settings.ScaleY, settings.Debug, settings.Quiet, i))
                    {
                        processed++;
                    }
                }

                sw.Stop();

                if (settings.Quiet == false && paths.Count > 0)
                {
                    Log($"Done: {sw.Elapsed} ({processed}/{paths.Count})");
                }
            }
            catch (Exception ex)
            {
                if (settings.Quiet == false)
                {
                    Error(ex);
                }
            }
        }
    }
}
