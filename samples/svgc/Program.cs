#nullable enable
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Svg;
using Svg.Skia;

namespace svgc
{
    class Settings
    {
        public System.IO.FileInfo? InputFile { get; set; }
        public System.IO.FileInfo? OutputFile { get; set; }
        public System.IO.FileInfo? JsonFile { get; set; }
        public string Namespace { get; set; } = "Svg";
        public string Class { get; set; } = "Generated";
    }

    class Item
    {
        public string? InputFile { get; set; }
        public string? OutputFile { get; set; }
        public string? Namespace { get; set; }
        public string? Class { get; set; }
    }

    class Program
    {
        static void Log(string message)
        {
            Console.WriteLine(message);
        }

        static void Error(Exception ex)
        {
            Log($"{ex.Message}");
            Log($"{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Error(ex.InnerException);
            }
        }

        static void Generate(string inputPath, string outputPath, string namespaceName = "Svg", string className = "Generated")
        {
            var svg = System.IO.File.ReadAllText(inputPath);
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
            SvgDocument.PointsPerInch = 96;
            var svgDocument = SvgDocument.FromSvg<SvgDocument>(svg);
            if (svgDocument != null)
            {
                var picture = SKSvg.ToModel(svgDocument);
                if (picture != null && picture.Commands != null)
                {
                    var text = SkiaCodeGen.Generate(picture, namespaceName, className);
                    System.IO.File.WriteAllText(outputPath, text);
                }
            }
        }

        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand()
            {
                Description = "Converts a svg file to a C# code."
            };

            var optionInputFile = new Option(new[] { "--inputFile", "-i" }, "The relative or absolute path to the input file")
            {
                IsRequired = false,
                Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
            };
            rootCommand.AddOption(optionInputFile);

            var optionOutputFile = new Option(new[] { "--outputFile", "-o" }, "The relative or absolute path to the output file")
            {
                IsRequired = false,
                Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
            };
            rootCommand.AddOption(optionOutputFile);

            var optionJsonFile = new Option(new[] { "--jsonFile", "-j" }, "The relative or absolute path to the json file")
            {
                IsRequired = false,
                Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
            };
            rootCommand.AddOption(optionJsonFile);

            var optionNamespace = new Option(new[] { "--namespace", "-n" }, "The generated C# namespace name")
            {
                IsRequired = false,
                Argument = new Argument<string>(getDefaultValue: () => "Svg")
            };
            rootCommand.AddOption(optionNamespace);

            var optionClass = new Option(new[] { "--class", "-c" }, "The generated C# class name")
            {
                IsRequired = false,
                Argument = new Argument<string>(getDefaultValue: () => "Generated")
            };
            rootCommand.AddOption(optionClass);

            rootCommand.Handler = CommandHandler.Create((Settings settings) =>
            {
                try
                {
                    if (settings.JsonFile != null)
                    {
                        var json = System.IO.File.ReadAllText(settings.JsonFile.FullName);
                        var options = new JsonSerializerOptions
                        {
                            ReadCommentHandling = JsonCommentHandling.Skip
                        };
                        var items = JsonSerializer.Deserialize<Item[]>(json, options);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                if (item.InputFile != null && item.OutputFile != null)
                                {
                                    Log($"Generating: {item.OutputFile}");
                                    Generate(item.InputFile, item.OutputFile, item.Namespace ?? settings.Namespace, item.Class ?? settings.Class);
                                }
                            }
                        }
                    }

                    if (settings.InputFile != null && settings.OutputFile != null)
                    {
                        Log($"Generating: {settings.OutputFile.FullName}");
                        Generate(settings.InputFile.FullName, settings.OutputFile.FullName, settings.Namespace, settings.Class);
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
