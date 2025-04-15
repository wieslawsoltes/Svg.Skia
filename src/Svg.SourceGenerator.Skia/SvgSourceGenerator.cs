using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Svg.CodeGen.Skia;

namespace Svg.SourceGenerator.Skia;

[Generator]
public class SvgSourceGenerator : IIncrementalGenerator
{
    private static readonly Model.IAssetLoader s_assetLoader = new SkiaGeneratorAssetLoader();

    private static readonly DiagnosticDescriptor s_errorDescriptor = new(
#pragma warning disable RS2008 // Enable analyzer release tracking
        "SVG0001",
#pragma warning restore RS2008 // Enable analyzer release tracking
        $"Error in the {nameof(SvgSourceGenerator)} generator",
        $"Error in the {nameof(SvgSourceGenerator)} generator: " + "{0}",
        $"{nameof(SvgSourceGenerator)}",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the additional files provider to get all .svg files
        var svgFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase));

        // Create a provider for the global namespace from build properties
        var globalNamespace = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => 
            {
                options.GlobalOptions.TryGetValue("build_property.NamespaceName", out var namespaceName);
                return namespaceName;
            });

        // Combine the SVG files with their analyzer config options and global namespace
        IncrementalValuesProvider<(AdditionalText File, AnalyzerConfigOptionsProvider Options, string? GlobalNamespace)> combined =
            svgFiles.Combine(context.AnalyzerConfigOptionsProvider.Select((options, _) => options))
                   .Combine(globalNamespace)
                   .Select((pair, _) => (pair.Left.Left, pair.Left.Right, pair.Right));

        // Register the source output
        context.RegisterSourceOutput(combined, (spc, data) => Execute(spc, data.File, data.Options, data.GlobalNamespace));
    }

    private void Execute(SourceProductionContext context, AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider, string? globalNamespaceName)
    {
        try
        {
            string? namespaceName = null;

            if (!string.IsNullOrWhiteSpace(globalNamespaceName))
            {
                namespaceName = globalNamespaceName;
            }

            var options = optionsProvider.GetOptions(file);
            if (options.TryGetValue("build_metadata.AdditionalFiles.NamespaceName", out var perFilenamespaceName))
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

            options.TryGetValue("build_metadata.AdditionalFiles.ClassName", out var className);

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

            var svgDocument = Model.IoService.FromSvg(svg!);
            if (svgDocument is { })
            {
                var picture = Model.IoService.ToModel(svgDocument, s_assetLoader, out _, out _);
                if (picture is { } && picture.Commands is { })
                {
                    var code = SkiaCSharpCodeGen.Generate(picture, namespaceName!, className!);
                    var sourceText = SourceText.From(code, Encoding.UTF8);
                    context.AddSource($"{className}.svg.cs", sourceText);
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "Invalid svg picture model."));
                }
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, "Could not load svg document."));
            }
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, e.Message + " " + e.StackTrace));
        }
    }

    // https://gist.github.com/FabienDehopre/5245476
    private const string FormattingCharacter = @"\p{Cf}";
    private const string ConnectingCharacter = @"\p{Pc}";
    private const string DecimalDigitCharacter = @"\p{Nd}";
    private const string CombiningCharacter = @"\p{Mn}|\p{Mc}";
    private const string LetterCharacter = @"\p{Lu}|\p{Ll}|\p{Lt}|\p{Lm}|\p{Lo}|\p{Nl}";
    private const string IdentifierPartCharacter = LetterCharacter + "|" +
                                                   DecimalDigitCharacter + "|" +
                                                   ConnectingCharacter + "|" +
                                                   CombiningCharacter + "|" +
                                                   FormattingCharacter;
    private const string InvalidIdentifierCharacterRegex = "(?!" + IdentifierPartCharacter + ").";
    private static readonly Regex s_regexReplaceName = new Regex(InvalidIdentifierCharacterRegex, RegexOptions.Compiled);
    
    private string CreateClassName(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var className = s_regexReplaceName.Replace(name, "_");
        return $"Svg_{className}";
    }
}
