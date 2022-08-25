#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Svg.CodeGen.Skia;

namespace Svg.SourceGenerator.Skia;

public class SkiaGeneratorAssetLoader : Svg.Model.IAssetLoader
{
    public ShimSkiaSharp.SKImage LoadImage(System.IO.Stream stream)
    {
        var data = ShimSkiaSharp.SKImage.FromStream(stream);
        using var image = SkiaSharp.SKImage.FromEncodedData(data);
        return new ShimSkiaSharp.SKImage
        {
            Data = data,
            Width = image.Width,
            Height = image.Height
        };
    }

    public List<Model.TypefaceSpan> FindTypefaces(string text, ShimSkiaSharp.SKPaint paintPreferredTypeface)
    {
        // TODO: Font fallback and text advancing code should be generated along with canvas commands instead.
        // Figure out how to somehow lay down information here. Otherwise, some package reference hacking may be needed.
        return new List<Model.TypefaceSpan>
        { new (text, text.Length * paintPreferredTypeface.TextSize, paintPreferredTypeface.Typeface) };
    }
}

[Generator]
public class SvgSourceGenerator : ISourceGenerator
{
    private static readonly  Svg.Model.IAssetLoader s_assetLoader = new SkiaGeneratorAssetLoader();

    private static readonly DiagnosticDescriptor s_errorDescriptor = new(
#pragma warning disable RS2008 // Enable analyzer release tracking
        "SVG0001",
#pragma warning restore RS2008 // Enable analyzer release tracking
        $"Error in the {nameof(SvgSourceGenerator)} generator",
        $"Error in the {nameof(SvgSourceGenerator)} generator: " + "{0}",
        $"{nameof(SvgSourceGenerator)}",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
        // System.Diagnostics.Debugger.Launch();
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NamespaceName", out var globalNamespaceName);

            var files = context.AdditionalFiles.Where(at => at.Path.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase));

            foreach (var file in files)
            {
                string? namespaceName = null;

                if (!string.IsNullOrWhiteSpace(globalNamespaceName))
                {
                    namespaceName = globalNamespaceName;
                }

                if (context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.AdditionalFiles.NamespaceName", out var perFilenamespaceName))
                {
                    if (!string.IsNullOrWhiteSpace(perFilenamespaceName))
                    {
                        namespaceName = perFilenamespaceName;
                    }
                }

                if (string.IsNullOrWhiteSpace(namespaceName))
                {
                    namespaceName = "Svg";
                }

                context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.AdditionalFiles.ClassName", out var className);

                if (string.IsNullOrWhiteSpace(className))
                {
                    className = CreateClassName(file.Path);
                }

                if (string.IsNullOrWhiteSpace(namespaceName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "The specified namespace name is invalid."));
                    return;
                }

                if (string.IsNullOrWhiteSpace(className))
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "The specified class name is invalid."));
                    return;
                }

                var svg = file.GetText(context.CancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(svg))
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "Svg file is null or empty."));
                    return;
                }

                var svgDocument =  Svg.Model.SvgExtensions.FromSvg(svg!);
                if (svgDocument is { })
                {
                    var picture =  Svg.Model.SvgExtensions.ToModel(svgDocument, s_assetLoader, out _, out _);
                    if (picture is { } && picture.Commands is { })
                    {
                        var code = SkiaCSharpCodeGen.Generate(picture, namespaceName!, className!);
                        var sourceText = SourceText.From(code, Encoding.UTF8);
                        context.AddSource($"{className}.svg.cs", sourceText);
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "Invalid svg picture model."));
                        return;
                    }
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "Could not load svg document."));
                    return;
                }
            }
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, e.Message + " " + e.StackTrace));
        }
    }

    private string CreateClassName(string path)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        string className = name.Replace("-", "_");
        return $"Svg_{className}";
    }
}
