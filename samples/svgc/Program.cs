#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Svg.CodeGen.Skia;
using SLIS = SixLabors.ImageSharp;
using SM = Svg.Model;
using SMP = ShimSkiaSharp;

namespace svgc;

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

class ImageSharpAssetLoader : SM.IAssetLoader
{
    public SMP.SKImage LoadImage(Stream stream)
    {
        var data = SMP.SKImage.FromStream(stream);
        using var image = SLIS.Image.Load(data);
        return new SMP.SKImage
        {
            Data = data,
            Width = image.Width,
            Height = image.Height
        };
    }

    public List<SM.TypefaceSpan> FindTypefaces(string text, SMP.SKPaint paintPreferredTypeface)
    {
        // TODO: Font fallback and text advancing code should be generated along with canvas commands instead.
        // Otherwise, some package reference hacking may be needed.
        return new List<SM.TypefaceSpan>
        { new (text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface) };
    }
}

class Program
{
    private static readonly SM.IAssetLoader AssetLoader = new ImageSharpAssetLoader();

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
        var svgDocument = SM.SvgExtensions.FromSvg(svg);
        if (svgDocument is { })
        {
            var picture = SM.SvgExtensions.ToModel(svgDocument, AssetLoader, out _, out _);
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
