// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Svg.Skia.Converter
{
    internal class FileInfoJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FileInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                return new FileInfo(s);
            }
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
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

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                return new DirectoryInfo(s);
            }
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is DirectoryInfo directoryInfo))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            writer.WriteValue(directoryInfo.FullName);
        }
    }

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var optionFile = new Option(new[] { "--files", "-f" }, "The relative or absolute path to the input files")
            {
                Argument = new Argument<FileInfo[]>(defaultValue: () => null)
            };

            var optionDirectory = new Option(new[] { "--directories", "-d" }, "The relative or absolute path to the input directories")
            {
                Argument = new Argument<DirectoryInfo[]>(defaultValue: () => null)
            };

            var optionOutput = new Option(new[] { "--output", "-o" }, "The relative or absolute path to the output directory")
            {
                Argument = new Argument<DirectoryInfo>(defaultValue: () => null)
            };

            var optionPattern = new Option(new[] { "--pattern", "-p" }, "The search string to match against the names of files in the input directory")
            {
                Argument = new Argument<string>(defaultValue: () => null)
            };

            var optionFormat = new Option(new[] { "--format" }, "The output image format")
            {
                Argument = new Argument<string>(defaultValue: () => "png")
            };

            var optionQuality = new Option(new[] { "--quality", "-q" }, "The output image quality")
            {
                Argument = new Argument<int>(defaultValue: () => 100)
            };

            var optionBackground = new Option(new[] { "--background", "-b" }, "The output image background")
            {
                Argument = new Argument<string>(defaultValue: () => "#00000000")
            };

            var optionScale = new Option(new[] { "--scale", "-s" }, "The output image horizontal and vertical scaling factor")
            {
                Argument = new Argument<float>(defaultValue: () => 1f)
            };

            var optionScaleX = new Option(new[] { "--scaleX", "-sx" }, "The output image horizontal scaling factor")
            {
                Argument = new Argument<float>(defaultValue: () => 1f)
            };

            var optionScaleY = new Option(new[] { "--scaleY", "-sy" }, "The output image vertical scaling factor")
            {
                Argument = new Argument<float>(defaultValue: () => 1f)
            };

            var optionDebug = new Option(new[] { "--debug" }, "Write debug output to a file")
            {
                Argument = new Argument<bool>()
            };

            var optionQuiet = new Option(new[] { "--quiet" }, "Set verbosity level to quiet")
            {
                Argument = new Argument<bool>()
            };

            var optionLoadConfig = new Option(new[] { "--load-config", "-c" }, "The relative or absolute path to the config file")
            {
                Argument = new Argument<FileInfo>(defaultValue: () => null)
            };

            var optionSaveConfig = new Option(new[] { "--save-config" }, "The relative or absolute path to the config file")
            {
                Argument = new Argument<FileInfo>(defaultValue: () => null)
            };

            var rootCommand = new RootCommand()
            {
                Description = "Converts a svg file to an encoded bitmap image."
            };

            rootCommand.AddOption(optionFile);
            rootCommand.AddOption(optionDirectory);
            rootCommand.AddOption(optionOutput);
            rootCommand.AddOption(optionPattern);
            rootCommand.AddOption(optionFormat);
            rootCommand.AddOption(optionQuality);
            rootCommand.AddOption(optionBackground);
            rootCommand.AddOption(optionScale);
            rootCommand.AddOption(optionScaleX);
            rootCommand.AddOption(optionScaleY);
            rootCommand.AddOption(optionDebug);
            rootCommand.AddOption(optionQuiet);
            rootCommand.AddOption(optionLoadConfig);
            rootCommand.AddOption(optionSaveConfig);

            rootCommand.Handler = CommandHandler.Create((ConverterSettings converterSettings, FileInfo loadConfig, FileInfo saveConfig) => 
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
                    var loadedConverterSettings = JsonConvert.DeserializeObject<ConverterSettings>(json, jsonSerializerSettings);
                    if (loadedConverterSettings != null)
                    {
                        Converter.Convert(loadedConverterSettings);
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
                        string json = JsonConvert.SerializeObject(converterSettings, jsonSerializerSettings);
                        File.WriteAllText(saveConfig.FullName, json);
                    }
                    Converter.Convert(converterSettings);
                }
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
