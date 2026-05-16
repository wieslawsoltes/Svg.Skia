using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Svg.Model.UnitTests;

public class ProjectBoundaryGuardTests
{
    private static readonly string[] SvgCustomForbiddenSourceReferences =
    [
        "SkiaSharp",
        "ShimSkiaSharp",
        "Svg.Model",
        "Svg.SceneGraph",
        "Svg.Skia",
        "Avalonia",
        "Microsoft.Maui",
        "Maui",
        "Uno",
        "Microsoft.UI",
        "Windows.UI",
        "Skia.Controls"
    ];

    private static readonly string[] SvgCustomForbiddenAssemblyReferences =
    [
        "SkiaSharp",
        "ShimSkiaSharp",
        "Svg.Model",
        "Svg.SceneGraph",
        "Svg.Skia",
        "Avalonia",
        "Microsoft.Maui",
        "Maui",
        "Uno",
        "Microsoft.UI",
        "Windows.UI",
        "Skia.Controls"
    ];

    private static readonly string[] ShimSkiaSharpForbiddenUsingPrefixes =
    [
        "SkiaSharp",
        "Svg.Custom",
        "Svg.Model",
        "Svg.SceneGraph",
        "Svg.Skia"
    ];

    private static readonly string[] ShimSkiaSharpForbiddenAssemblyReferences =
    [
        "SkiaSharp",
        "Svg.Custom",
        "Svg.Model",
        "Svg.SceneGraph",
        "Svg.Skia"
    ];

    private static readonly string[] ModelLayerForbiddenUsingPrefixes =
    [
        "SkiaSharp",
        "Svg.Skia"
    ];

    private static readonly string[] ModelLayerForbiddenAssemblyReferences =
    [
        "SkiaSharp",
        "Svg.Skia"
    ];

    private static readonly Regex UsingDirectiveRegex = new(
        @"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*)?(?<namespace>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*;",
        RegexOptions.Compiled);

    [Fact]
    public void SvgCustom_DoesNotReferenceRenderingModelOrHostLayers()
    {
        var repoRoot = FindRepoRoot();
        var projectDirectory = Path.Combine(repoRoot, "src", "Svg.Custom");
        var externalSourceDirectory = Path.Combine(repoRoot, "externals", "SVG", "Source");

        var offenders = FindForbiddenProjectReferences(
                repoRoot,
                Path.Combine(projectDirectory, "Svg.Custom.csproj"),
                SvgCustomForbiddenAssemblyReferences)
            .Concat(FindForbiddenSourceReferences(
                repoRoot,
                [projectDirectory, externalSourceDirectory],
                SvgCustomForbiddenSourceReferences))
            .OrderBy(static offender => offender, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ShimSkiaSharp_DoesNotReferenceSvgOrSkiaProductionLayers()
    {
        var repoRoot = FindRepoRoot();
        var projectDirectory = Path.Combine(repoRoot, "src", "ShimSkiaSharp");

        var offenders = FindForbiddenProjectReferences(
                repoRoot,
                Path.Combine(projectDirectory, "ShimSkiaSharp.csproj"),
                ShimSkiaSharpForbiddenAssemblyReferences)
            .Concat(FindForbiddenUsingDirectives(
                repoRoot,
                projectDirectory,
                ShimSkiaSharpForbiddenUsingPrefixes))
            .OrderBy(static offender => offender, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ModelAndSceneGraph_DoNotReferenceSkiaSharpOrSvgSkiaAssemblies()
    {
        var repoRoot = FindRepoRoot();

        var offenders = new[]
            {
                Path.Combine(repoRoot, "src", "Svg.Model"),
                Path.Combine(repoRoot, "src", "Svg.SceneGraph")
            }
            .SelectMany(projectDirectory =>
                FindForbiddenProjectReferences(
                    repoRoot,
                    Path.Combine(projectDirectory, $"{Path.GetFileName(projectDirectory)}.csproj"),
                    ModelLayerForbiddenAssemblyReferences)
                .Concat(FindForbiddenUsingDirectives(
                    repoRoot,
                    projectDirectory,
                    ModelLayerForbiddenUsingPrefixes)))
            .OrderBy(static offender => offender, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> FindForbiddenProjectReferences(
        string repoRoot,
        string projectPath,
        IReadOnlyCollection<string> forbiddenReferences)
    {
        var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
        var referenceElements = document
            .Descendants()
            .Where(static element =>
                element.Name.LocalName is "PackageReference" or "ProjectReference" or "Reference" or "Import")
            .Select(element =>
            {
                var include = (string?)element.Attribute("Include");
                var update = (string?)element.Attribute("Update");
                var project = (string?)element.Attribute("Project");
                var value = include ?? update ?? project ?? string.Empty;
                return new ProjectReferenceItem(element.Name.LocalName, value);
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value));

        foreach (var reference in referenceElements)
        {
            var referenceName = Path.GetFileNameWithoutExtension(reference.Value.Replace('\\', Path.DirectorySeparatorChar));

            foreach (var forbiddenReference in forbiddenReferences)
            {
                if (IsForbiddenReference(referenceName, forbiddenReference) ||
                    IsForbiddenReference(reference.Value, forbiddenReference))
                {
                    yield return $"{RelativePath(repoRoot, projectPath)}: {reference.Kind} {reference.Value} references {forbiddenReference}";
                }
            }
        }
    }

    private static IEnumerable<string> FindForbiddenSourceReferences(
        string repoRoot,
        IEnumerable<string> sourceDirectories,
        IReadOnlyCollection<string> forbiddenReferences)
    {
        foreach (var path in sourceDirectories.SelectMany(EnumerateProductionSourceFiles))
        {
            var strippedContent = StripCommentsAndStringLiterals(File.ReadAllText(path));
            var lines = strippedContent.Split('\n');

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (var forbiddenReference in forbiddenReferences)
                {
                    if (ContainsTokenReference(lines[lineIndex], forbiddenReference))
                    {
                        yield return $"{RelativePath(repoRoot, path)}:{lineIndex + 1} references {forbiddenReference}";
                    }
                }
            }
        }
    }

    private static IEnumerable<string> FindForbiddenUsingDirectives(
        string repoRoot,
        string projectDirectory,
        IReadOnlyCollection<string> forbiddenUsingPrefixes)
    {
        foreach (var path in EnumerateProductionSourceFiles(projectDirectory))
        {
            var strippedContent = StripCommentsAndStringLiterals(File.ReadAllText(path));
            var lines = strippedContent.Split('\n');

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var match = UsingDirectiveRegex.Match(lines[lineIndex]);
                if (!match.Success)
                {
                    continue;
                }

                var referencedNamespace = match.Groups["namespace"].Value;
                foreach (var forbiddenPrefix in forbiddenUsingPrefixes)
                {
                    if (IsNamespaceOrNestedNamespace(referencedNamespace, forbiddenPrefix))
                    {
                        yield return $"{RelativePath(repoRoot, path)}:{lineIndex + 1} has using {referencedNamespace}";
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProductionSourceFiles(string projectDirectory)
    {
        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !PathContainsSegment(path, "bin") && !PathContainsSegment(path, "obj"));
    }

    private static bool PathContainsSegment(string path, string segment)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(pathSegment => string.Equals(pathSegment, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTokenReference(string text, string token)
    {
        var index = 0;

        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 ? '\0' : text[index - 1];
            var afterIndex = index + token.Length;
            var after = afterIndex >= text.Length ? '\0' : text[afterIndex];

            if (!IsIdentifierOrNamespaceCharacter(before) && !IsIdentifierOrNamespaceCharacter(after))
            {
                return true;
            }

            index += token.Length;
        }

        return false;
    }

    private static bool IsForbiddenReference(string value, string forbiddenReference)
    {
        return string.Equals(value, forbiddenReference, StringComparison.Ordinal) ||
               value.StartsWith(forbiddenReference + ".", StringComparison.Ordinal) ||
               value.EndsWith(Path.DirectorySeparatorChar + forbiddenReference, StringComparison.Ordinal) ||
               value.Contains(Path.DirectorySeparatorChar + forbiddenReference + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool IsNamespaceOrNestedNamespace(string value, string namespacePrefix)
    {
        return string.Equals(value, namespacePrefix, StringComparison.Ordinal) ||
               value.StartsWith(namespacePrefix + ".", StringComparison.Ordinal);
    }

    private static bool IsIdentifierOrNamespaceCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '.';
    }

    private static string StripCommentsAndStringLiterals(string content)
    {
        var result = content.ToCharArray();
        var state = LexerState.Code;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            var next = index + 1 < content.Length ? content[index + 1] : '\0';
            var previous = index > 0 ? content[index - 1] : '\0';

            switch (state)
            {
                case LexerState.Code:
                    if (current == '/' && next == '/')
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index++;
                        state = LexerState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index++;
                        state = LexerState.BlockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index++;
                        state = LexerState.VerbatimString;
                    }
                    else if (current == '"' && previous == '@')
                    {
                        result[index] = ' ';
                        state = LexerState.VerbatimString;
                    }
                    else if (current == '"')
                    {
                        result[index] = ' ';
                        state = LexerState.String;
                    }
                    else if (current == '\'')
                    {
                        result[index] = ' ';
                        state = LexerState.Character;
                    }

                    break;

                case LexerState.LineComment:
                    if (current == '\n')
                    {
                        state = LexerState.Code;
                    }
                    else
                    {
                        result[index] = ' ';
                    }

                    break;

                case LexerState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index++;
                        state = LexerState.Code;
                    }
                    else if (current != '\n' && current != '\r')
                    {
                        result[index] = ' ';
                    }

                    break;

                case LexerState.String:
                    result[index] = current is '\n' or '\r' ? current : ' ';
                    if (current == '"' && previous != '\\')
                    {
                        state = LexerState.Code;
                    }

                    break;

                case LexerState.VerbatimString:
                    result[index] = current is '\n' or '\r' ? current : ' ';
                    if (current == '"' && next == '"')
                    {
                        result[index + 1] = ' ';
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = LexerState.Code;
                    }

                    break;

                case LexerState.Character:
                    result[index] = current is '\n' or '\r' ? current : ' ';
                    if (current == '\'' && previous != '\\')
                    {
                        state = LexerState.Code;
                    }

                    break;
            }
        }

        return new string(result);
    }

    private static string RelativePath(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Svg.Skia.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Svg.Skia repository root.");
    }

    private readonly record struct ProjectReferenceItem(string Kind, string Value);

    private enum LexerState
    {
        Code,
        LineComment,
        BlockComment,
        String,
        VerbatimString,
        Character
    }
}
