using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

[Collection(SvgExternalResourceStateCollection.Name)]
public class SvgReferenceTraversalLimitTests
{
    [Fact]
    public void SharedAcyclicGraphIsCheckedWithoutExpandingEveryPath()
    {
        const int levels = 30;
        const int maximumVisits = 2 * levels + 1;
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg'><defs><g id='level0'><rect width='1' height='1'/></g>");
        for (var i = 1; i <= levels; i++)
        {
            source.Append($"<g id='level{i}'><use href='#level{i - 1}'/><use href='#level{i - 1}'/></g>");
        }

        source.Append($"</defs><use id='start' href='#level{levels}'/></svg>");
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source.ToString()));
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));
        var visits = 0;

        var hasCycle = start.HasRecursiveReference(use =>
        {
            // Fail promptly if traversal regresses to visiting the graph's 2^30
            // expansion paths. The assertion measures work instead of elapsed time.
            Assert.True(++visits <= maximumVisits, "Shared references were expanded repeatedly.");
            return SvgService.GetEffectiveReferenceUri(use, use.ReferencedElement);
        }, new HashSet<Uri>());

        Assert.False(hasCycle);
        Assert.InRange(visits, levels + 1, maximumVisits);
    }

    [Theory]
    [InlineData(125, false)]
    [InlineData(126, true)]
    public void ShallowCachedReferenceCannotBypassDepthLimitWhenReusedDeeper(int prefixLength, bool exceedsLimit)
    {
        var source = new StringBuilder("""
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <rect id='leaf' width='1' height='1'/>
                <g id='shared'><use href='#leaf'/></g>
                <g id='root'><use href='#shared'/><use href='#path0'/></g>
            """);
        for (var i = 0; i < prefixLength; i++)
        {
            var target = i + 1 == prefixLength ? "shared" : $"path{i + 1}";
            source.Append($"<g id='path{i}'><use href='#{target}'/></g>");
        }

        source.Append("</defs><use id='start' href='#root'/></svg>");
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source.ToString()));
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));

        if (exceedsLimit)
        {
            var exception = Assert.Throws<SvgProcessingLimitException>(() => HasCycle(start));
            Assert.Equal(SvgProcessingLimitKind.Depth, exception.Kind);
            Assert.Equal(128, exception.Limit);
        }
        else
        {
            Assert.False(HasCycle(start));
        }
    }

    [Fact]
    public void ReferencesSharedBySiblingBranchesRemainAcyclic()
    {
        const string source = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <rect id='leaf' width='1' height='1'/>
                <use id='shared' href='#leaf'/>
                <g id='left'><use href='#shared'/></g>
                <g id='right'><use href='#shared'/></g>
                <g id='root'><use href='#left'/><use href='#right'/></g>
              </defs>
              <use id='start' href='#root'/>
            </svg>
            """;
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source));
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));

        Assert.False(HasCycle(start));
    }

    [Fact]
    public void CycleInLaterSiblingIsNotHiddenByCompletedSharedBranch()
    {
        const string source = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <rect id='leaf' width='1' height='1'/>
                <use id='shared' href='#leaf'/>
                <g id='loop'><use href='#shared'/><use href='#root'/></g>
                <g id='root'><use href='#shared'/><use href='#loop'/></g>
              </defs>
              <use id='start' href='#root'/>
            </svg>
            """;
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source));
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));

        Assert.True(HasCycle(start));
    }

    [Fact]
    public void ReferenceMemoizationDoesNotSurviveDocumentMutation()
    {
        const string source = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <rect id='leaf' width='1' height='1'/>
                <use id='shared' href='#leaf'/>
                <g id='root'><use href='#shared'/><use href='#shared'/></g>
              </defs>
              <use id='start' href='#root'/>
            </svg>
            """;
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source));
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));
        var shared = Assert.IsType<SvgUse>(document.GetElementById("shared"));

        Assert.False(HasCycle(start));
        shared.ReferencedElement = new Uri("#root", UriKind.Relative);
        Assert.True(HasCycle(start));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteReferencesPreserveFragmentIdentityAndCanonicalizeDocumentUri(bool containsCycle)
    {
        var aliasTarget = containsCycle ? "root" : "leaf";
        var source = $"""
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <rect id='leaf' width='1' height='1'/>
                <use id='alias' href='https://example.invalid/scene.svg#{aliasTarget}'/>
                <g id='root'><use href='https://EXAMPLE.invalid:443/nested/../scene.svg#alias'/></g>
              </defs>
              <use id='start' href='https://example.invalid/scene.svg#root'/>
            </svg>
            """;
        var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source));
        document.BaseUri = new Uri("https://example.invalid/scene.svg");
        var start = Assert.IsType<SvgUse>(document.GetElementById("start"));
        var visitedAlias = false;

        var hasCycle = start.HasRecursiveReference(use =>
        {
            visitedAlias |= use.ID == "alias";
            return SvgService.GetEffectiveReferenceUri(use, use.ReferencedElement);
        }, new HashSet<Uri>());

        Assert.Equal(containsCycle, hasCycle);
        Assert.True(visitedAlias);
    }

    [Fact]
    public void IdenticalLocalFragmentsInDifferentDocumentsRemainDistinctOnActivePath()
    {
        const string firstDocument = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <g id='root'><use href='#shared'/></g>
                <use id='shared' href='b.svg#root'/>
              </defs>
            </svg>
            """;
        const string secondDocument = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <g id='root'><use href='#shared'/></g>
                <use id='shared' href='#leaf'/>
                <rect id='leaf' width='1' height='1'/>
              </defs>
            </svg>
            """;

        var (hasCycle, visitedDocuments) = CheckExternalGraph("a.svg#root", string.Empty, firstDocument, secondDocument);

        Assert.False(hasCycle);
        Assert.Contains("a.svg", visitedDocuments);
        Assert.Contains("b.svg", visitedDocuments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedLocalFragmentCannotHideAnotherDocumentsReferenceGraph(bool containsCycle)
    {
        const string definitions = "<g id='root'><use href='a.svg#root'/><use href='b.svg#root'/></g>";
        const string firstDocument = """
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <g id='root'><use href='#shared'/></g>
                <use id='shared' href='#leaf'/>
                <rect id='leaf' width='1' height='1'/>
              </defs>
            </svg>
            """;
        var sharedTarget = containsCycle ? "loop" : "leaf";
        var secondDocument = $"""
            <svg xmlns='http://www.w3.org/2000/svg'>
              <defs>
                <g id='root'><use href='#shared'/></g>
                <use id='shared' href='#{sharedTarget}'/>
                <use id='loop' href='#shared'/>
                <rect id='leaf' width='1' height='1'/>
              </defs>
            </svg>
            """;

        var (hasCycle, visitedDocuments) = CheckExternalGraph("#root", definitions, firstDocument, secondDocument);

        Assert.Equal(containsCycle, hasCycle);
        Assert.Contains("a.svg", visitedDocuments);
        Assert.Contains("b.svg", visitedDocuments);
    }

    [Fact]
    public void UnbasedDocumentsUseSeparateLocalFragmentScopes()
    {
        var firstDocument = Assert.IsType<SvgDocument>(SvgService.FromSvg("""
            <svg xmlns='http://www.w3.org/2000/svg'>
              <use id='start' href='#shared'/>
              <rect id='shared' width='1' height='1'/>
            </svg>
            """));
        var secondDocument = Assert.IsType<SvgDocument>(SvgService.FromSvg("""
            <svg xmlns='http://www.w3.org/2000/svg'>
              <g id='root'><use href='#shared'/></g>
              <rect id='shared' width='1' height='1'/>
            </svg>
            """));
        var start = Assert.IsType<SvgUse>(firstDocument.GetElementById("start"));
        var secondRoot = Assert.IsType<SvgGroup>(secondDocument.GetElementById("root"));
        var callerReferences = new HashSet<Uri> { new("#shared", UriKind.Relative) };

        Assert.False(start.ElementReferencesUri(
            static use => SvgService.GetEffectiveReferenceUri(use, use.ReferencedElement),
            callerReferences,
            secondRoot));
    }

    private static (bool HasCycle, HashSet<string> VisitedDocuments) CheckExternalGraph(
        string startHref, string definitions, string firstDocument, string secondDocument)
    {
        var previousResolveExternalElements = SvgDocument.ResolveExternalElements;
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            SvgDocument.ResolveExternalElements = ExternalType.Local;
            File.WriteAllText(Path.Combine(directory.FullName, "a.svg"), firstDocument);
            File.WriteAllText(Path.Combine(directory.FullName, "b.svg"), secondDocument);
            var source = $"""
                <svg xmlns='http://www.w3.org/2000/svg'>
                  <defs>{definitions}</defs>
                  <use id='start' href='{startHref}'/>
                </svg>
                """;
            var document = Assert.IsType<SvgDocument>(SvgService.FromSvg(source));
            document.BaseUri = new Uri(Path.Combine(directory.FullName, "root.svg"));
            var start = Assert.IsType<SvgUse>(document.GetElementById("start"));
            var visitedDocuments = new HashSet<string>(StringComparer.Ordinal);

            var hasCycle = start.HasRecursiveReference(use =>
            {
                if (use.ID == "shared")
                {
                    visitedDocuments.Add(Path.GetFileName(use.OwnerDocument.BaseUri.LocalPath));
                }

                return SvgService.GetEffectiveReferenceUri(use, use.ReferencedElement);
            }, new HashSet<Uri>());

            return (hasCycle, visitedDocuments);
        }
        finally
        {
            SvgDocument.ResolveExternalElements = previousResolveExternalElements;
            directory.Delete(recursive: true);
        }
    }

    private static bool HasCycle(SvgUse use)
    {
        return use.HasRecursiveReference(
            static element => SvgService.GetEffectiveReferenceUri(element, element.ReferencedElement),
            new HashSet<Uri>());
    }
}
