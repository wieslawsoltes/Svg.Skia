﻿#nullable enable
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Svg.CodeGen.Skia;

namespace svgc;

class Program
{
    private static readonly Svg.Model.IAssetLoader AssetLoader = new ImageSharpAssetLoader();

    static void Log(string message)
    {
        Console.WriteLine(message);
    }

    static void Error(Exception ex)
    {
        Log($"{ex.Message}");
        Log($"{ex.StackTrace}");
        if (ex.InnerException is { })
        {
            Error(ex.InnerException);
        }
    }

    static void Generate(string inputPath, string outputPath, string namespaceName = "Svg", string className = "Generated")
    {
        var svg = System.IO.File.ReadAllText(inputPath);
        var svgDocument = Svg.Model.SvgExtensions.FromSvg(svg);
        if (svgDocument is { })
        {
            var picture = Svg.Model.SvgExtensions.ToModel(svgDocument, AssetLoader, out _, out _);
            if (picture is { } && picture.Commands is { })
            {
                var text = SkiaCSharpCodeGen.Generate(picture, namespaceName, className);
                System.IO.File.WriteAllText(outputPath, text);
            }
        }
    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            Description = "Converts a svg file to a C# code."
        };

        var optionInputFile = new Option(["--inputFile", "-i"], "The relative or absolute path to the input file")
        {
            IsRequired = false,
            Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
        };
        rootCommand.AddOption(optionInputFile);

        var optionOutputFile = new Option(["--outputFile", "-o"], "The relative or absolute path to the output file")
        {
            IsRequired = false,
            Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
        };
        rootCommand.AddOption(optionOutputFile);

        var optionJsonFile = new Option(["--jsonFile", "-j"], "The relative or absolute path to the json file")
        {
            IsRequired = false,
            Argument = new Argument<System.IO.FileInfo?>(getDefaultValue: () => null)
        };
        rootCommand.AddOption(optionJsonFile);

        var optionNamespace = new Option(["--namespace", "-n"], "The generated C# namespace name")
        {
            IsRequired = false,
            Argument = new Argument<string>(getDefaultValue: () => "Svg")
        };
        rootCommand.AddOption(optionNamespace);

        var optionClass = new Option(["--class", "-c"], "The generated C# class name")
        {
            IsRequired = false,
            Argument = new Argument<string>(getDefaultValue: () => "Generated")
        };
        rootCommand.AddOption(optionClass);

        rootCommand.Handler = CommandHandler.Create((Settings settings) =>
        {
            try
            {
                if (settings.JsonFile is { })
                {
                    var json = System.IO.File.ReadAllText(settings.JsonFile.FullName);
                    var options = new JsonSerializerOptions
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    var items = JsonSerializer.Deserialize<Item[]>(json, options);
                    if (items is { })
                    {
                        foreach (var item in items)
                        {
                            if (item.InputFile is { } && item.OutputFile is { })
                            {
                                Log($"Generating: {item.OutputFile}");
                                Generate(item.InputFile, item.OutputFile, item.Namespace ?? settings.Namespace, item.Class ?? settings.Class);
                            }
                        }
                    }
                }

                if (settings.InputFile is { } && settings.OutputFile is { })
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
