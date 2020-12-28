#nullable enable
using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Svg.CodeGen.Skia;
using Svg.Model;
using Svg.Skia;

namespace Svg.SourceGenerator.Skia
{
    [Generator]
    public class SvgSourceGenerator : ISourceGenerator
    {
        private static readonly IAssetLoader AssetLoader = new SkiaAssetLoader();

        private static readonly DiagnosticDescriptor s_errorDescriptor = new DiagnosticDescriptor(
#pragma warning disable RS2008 // Enable analyzer release tracking
            "SV0000",
#pragma warning restore RS2008 // Enable analyzer release tracking
            $"Error in the {nameof(SvgSourceGenerator)} generator",
            $"Error in the {nameof(SvgSourceGenerator)} generator: '{0}'",
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

                    var svgDocument = SvgModelExtensions.FromSvg(svg!);
                    if (svgDocument != null)
                    {
                        var picture = SvgModelExtensions.ToModel(svgDocument, AssetLoader);
                        if (picture != null && picture.Commands != null)
                        {
                            var code = SkiaCodeGen.Generate(picture, namespaceName!, className!);
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
                context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, e.ToString()));
            }
        }

        private string CreateClassName(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string className = name.Replace("-", "_");
            return $"Svg_{className}";
        }
    }
}
