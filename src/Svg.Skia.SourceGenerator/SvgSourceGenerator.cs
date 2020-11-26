#nullable enable
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Svg.Skia
{
    [Generator]
    public class SvgSourceGenerator : ISourceGenerator
    {
        private static DiagnosticDescriptor ErrorDescriptor = new DiagnosticDescriptor(
#pragma warning disable RS2008 // Enable analyzer release tracking
            "SI0000",
#pragma warning restore RS2008 // Enable analyzer release tracking
            "Error in the SvgSourceGenerator generator",
            "Error in the SvgSourceGenerator generator: '{0}'",
            "SvgSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context)
        {
            //System.Diagnostics.Debugger.Launch();
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                ExecuteInternal(context);
            }
            catch (Exception e)
            {
                // This is temporary till https://github.com/dotnet/roslyn/issues/46084 is fixed
                context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, e.ToString()));
            }
        }

        private string CreateClassName(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string className = name.Replace("-", "_");
            return $"Svg_{className}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ExecuteInternal(GeneratorExecutionContext context)
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
                    context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, "The specified namespace name is invalid."));
                    return;
                }

                if (string.IsNullOrWhiteSpace(className))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, "The specified class name is invalid."));
                    return;
                }

                var svg = file.GetText(context.CancellationToken)?.ToString();

                SvgDocument.SkipGdiPlusCapabilityCheck = true;
                SvgDocument.PointsPerInch = 96;

                var svgDocument = SvgDocument.FromSvg<SvgDocument>(svg);
                if (svgDocument != null)
                {
                    var picture = SKSvg.ToModel(svgDocument);
                    if (picture != null && picture.Commands != null)
                    {
                        var code = SkiaCodeGen.Generate(picture, namespaceName!, className!);
                        var sourceText = SourceText.From(code, Encoding.UTF8);
                        context.AddSource($"{className}.svg.cs", sourceText);
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, "Invalid svg picture model."));
                    }
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, "Could not load svg document."));
                }
            }
        }
    }
}
