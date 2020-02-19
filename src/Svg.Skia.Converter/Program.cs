// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SkiaSharp;

namespace Svg.Skia.Converter
{
    internal class FileInfoJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FileInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                return new FileInfo(s);
            }
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (!(value is FileInfo fileInfo))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            writer.WriteValue(fileInfo.FullName);
        }
    }

    internal class DirectoryInfoJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DirectoryInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                return new DirectoryInfo(s);
            }
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (!(value is DirectoryInfo directoryInfo))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            writer.WriteValue(directoryInfo.FullName);
        }
    }

    public class Settings
    {
        public FileInfo[]? InputFiles { get; set; }
        public DirectoryInfo[]? InputDirectories { get; set; }
        public FileInfo? OutputFile { get; set; }
        public DirectoryInfo? OutputDirectory { get; set; }
        public string? Pattern { get; set; }
        public string Format { get; set; } = "png";
        public int Quality { get; set; } = 100;
        public string Background { get; set; } = "#00FFFFFF";
        public float Scale { get; set; } = 1f;
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;
        public bool Quiet { get; set; }
    }

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

        public static bool Save(FileInfo inputPath, FileInfo? outputFile, DirectoryInfo? outputDirectory, string format, int quality, string background, float scale, float scaleX, float scaleY, bool quiet, int i)
        {
            try
            {
                if (quiet == false)
                {
                    Log($"[{i}] File: {inputPath}");
                }

                string outputPath = string.Empty;

                if (outputFile != null)
                {
                    outputPath = outputFile.FullName;
                }
                else
                {
                    var inputExtension = inputPath.Extension;
                    outputPath = inputPath.FullName.Remove(inputPath.FullName.Length - inputExtension.Length) + "." + format.ToLower();
                    if (outputDirectory != null && !string.IsNullOrEmpty(outputDirectory.FullName))
                    {
                        outputPath = Path.Combine(outputDirectory.FullName, Path.GetFileName(outputPath));
                    }
                }

                Directory.SetCurrentDirectory(Path.GetDirectoryName(inputPath.FullName));

                using (var svg = new SKSvg())
                {
                    if (svg.Load(inputPath.FullName) != null)
                    {
                        if (string.Compare(format, "pdf", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (SKColor.TryParse(background, out var skBackgroundColor))
                            {
                                if (scale != 1f)
                                {
                                    svg.Picture?.ToPdf(outputPath, skBackgroundColor, scale, scale);
                                }
                                else
                                {
                                    svg.Picture?.ToPdf(outputPath, skBackgroundColor, scaleX, scaleY);
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Invalid output image background.", nameof(background));
                            }
                        }
                        else if (string.Compare(format, "xps", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (SKColor.TryParse(background, out var skBackgroundColor))
                            {
                                if (scale != 1f)
                                {
                                    svg.Picture?.ToXps(outputPath, skBackgroundColor, scale, scale);
                                }
                                else
                                {
                                    svg.Picture?.ToXps(outputPath, skBackgroundColor, scaleX, scaleY);
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Invalid output image background.", nameof(background));
                            }
                        }
                        else
                        {
                            if (Enum.TryParse<SKEncodedImageFormat>(format, true, out var skEncodedImageFormat))
                            {
                                if (SKColor.TryParse(background, out var skBackgroundColor))
                                {
                                    if (scale != 1f)
                                    {
                                        svg.Save(outputPath, skBackgroundColor, skEncodedImageFormat, quality, scale, scale);
                                    }
                                    else
                                    {
                                        svg.Save(outputPath, skBackgroundColor, skEncodedImageFormat, quality, scaleX, scaleY);
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
                }

                if (quiet == false)
                {
                    Log($"[{i}] Success: {outputPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                if (quiet == false)
                {
                    Log($"[{i}] Error: {inputPath}");
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

        public static void Convert(Settings settings)
        {
            try
            {
                var paths = new List<FileInfo>();

                if (settings.InputFiles != null)
                {
                    foreach (var file in settings.InputFiles)
                    {
                        paths.Add(file);
                    }
                }

                if (settings.InputDirectories != null)
                {
                    foreach (var directory in settings.InputDirectories)
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

                if (settings.OutputDirectory != null && !string.IsNullOrEmpty(settings.OutputDirectory.FullName))
                {
                    if (!Directory.Exists(settings.OutputDirectory.FullName))
                    {
                        Directory.CreateDirectory(settings.OutputDirectory.FullName);
                    }
                }

                var sw = Stopwatch.StartNew();

                int processed = 0;

                for (int i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];

                    if (Save(path, settings.OutputFile, settings.OutputDirectory, settings.Format, settings.Quality, settings.Background, settings.Scale, settings.ScaleX, settings.ScaleY, settings.Quiet, i))
                    {
                        processed++;
                    }
                }

                sw.Stop();

                if (paths.Count > 0)
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

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var optionInputFiles = new Option(new[] { "--inputFiles", "-f" }, "The relative or absolute path to the input files")
            {
                Argument = new Argument<FileInfo[]?>(getDefaultValue: () => null)
            };

            var optionInputDirectories = new Option(new[] { "--inputDirectories", "-d" }, "The relative or absolute path to the input directories")
            {
                Argument = new Argument<DirectoryInfo[]?>(getDefaultValue: () => null)
            };

            var optionOutputDirectory = new Option(new[] { "--outputDirectory", "-o" }, "The relative or absolute path to the output directory")
            {
                Argument = new Argument<DirectoryInfo?>(getDefaultValue: () => null)
            };

            var optionOutputFile = new Option(new[] { "--outputFile" }, "The relative or absolute path to the output file")
            {
                Argument = new Argument<DirectoryInfo?>(getDefaultValue: () => null)
            };

            var optionPattern = new Option(new[] { "--pattern", "-p" }, "The search string to match against the names of files in the input directory")
            {
                Argument = new Argument<string?>(getDefaultValue: () => null)
            };

            var optionFormat = new Option(new[] { "--format" }, "The output image format")
            {
                Argument = new Argument<string>(getDefaultValue: () => "png")
            };

            var optionQuality = new Option(new[] { "--quality", "-q" }, "The output image quality")
            {
                Argument = new Argument<int>(getDefaultValue: () => 100)
            };

            var optionBackground = new Option(new[] { "--background", "-b" }, "The output image background")
            {
                Argument = new Argument<string>(getDefaultValue: () => "#00000000")
            };

            var optionScale = new Option(new[] { "--scale", "-s" }, "The output image horizontal and vertical scaling factor")
            {
                Argument = new Argument<float>(getDefaultValue: () => 1f)
            };

            var optionScaleX = new Option(new[] { "--scaleX", "-sx" }, "The output image horizontal scaling factor")
            {
                Argument = new Argument<float>(getDefaultValue: () => 1f)
            };

            var optionScaleY = new Option(new[] { "--scaleY", "-sy" }, "The output image vertical scaling factor")
            {
                Argument = new Argument<float>(getDefaultValue: () => 1f)
            };

            var optionQuiet = new Option(new[] { "--quiet" }, "Set verbosity level to quiet")
            {
                Argument = new Argument<bool>()
            };

            var optionLoadConfig = new Option(new[] { "--load-config", "-c" }, "The relative or absolute path to the config file")
            {
                Argument = new Argument<FileInfo?>(getDefaultValue: () => null)
            };

            var optionSaveConfig = new Option(new[] { "--save-config" }, "The relative or absolute path to the config file")
            {
                Argument = new Argument<FileInfo?>(getDefaultValue: () => null)
            };

            var rootCommand = new RootCommand()
            {
                Description = "Converts a svg file to an encoded bitmap image."
            };

            rootCommand.AddOption(optionInputFiles);
            rootCommand.AddOption(optionInputDirectories);
            rootCommand.AddOption(optionOutputDirectory);
            rootCommand.AddOption(optionOutputFile);
            rootCommand.AddOption(optionPattern);
            rootCommand.AddOption(optionFormat);
            rootCommand.AddOption(optionQuality);
            rootCommand.AddOption(optionBackground);
            rootCommand.AddOption(optionScale);
            rootCommand.AddOption(optionScaleX);
            rootCommand.AddOption(optionScaleY);
            rootCommand.AddOption(optionQuiet);
            rootCommand.AddOption(optionLoadConfig);
            rootCommand.AddOption(optionSaveConfig);

            rootCommand.Handler = CommandHandler.Create((Settings settings, FileInfo loadConfig, FileInfo saveConfig) =>
            {
                if (loadConfig != null)
                {
                    var jsonSerializerSettings = new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters =
                        {
                            new FileInfoJsonConverter(),
                            new DirectoryInfoJsonConverter()
                        }
                    };
                    var json = File.ReadAllText(loadConfig.FullName);
                    var loadedSettings = JsonConvert.DeserializeObject<Settings>(json, jsonSerializerSettings);
                    if (loadedSettings != null)
                    {
                        Converter.Convert(loadedSettings);
                    }
                }
                else
                {
                    if (saveConfig != null)
                    {
                        var jsonSerializerSettings = new JsonSerializerSettings()
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                            Converters =
                            {
                                new FileInfoJsonConverter(),
                                new DirectoryInfoJsonConverter()
                            }
                        };
                        string json = JsonConvert.SerializeObject(settings, jsonSerializerSettings);
                        File.WriteAllText(saveConfig.FullName, json);
                    }
                    Converter.Convert(settings);
                }
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
