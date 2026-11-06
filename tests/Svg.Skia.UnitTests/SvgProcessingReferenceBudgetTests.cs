using System;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingReferenceBudgetTests
{
    private const int MaximumReferenceResolutions = 500_000;

    private static readonly Type LimitsType = typeof(SvgDocument).Assembly.GetType("Svg.SvgProcessingLimits", throwOnError: true)!;
    private static readonly MethodInfo EnterMethod = LimitsType.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Action CountReference = LimitsType.GetMethod("CountReferenceResolution", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Action>();

    [Fact]
    public void ReferenceBudgetAllowsExactBoundaryAndRejectsTheNextAttempt()
    {
        using var scope = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);

        var exception = Assert.Throws<SvgProcessingLimitException>(CountReference);
        Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
        Assert.Equal(MaximumReferenceResolutions, exception.Limit);
        Assert.Equal("reference resolution", exception.Operation);
        Assert.Contains("reference count", exception.Message);
    }

    [Fact]
    public void NestedScopesShareWorkAndDoNotRefundItOnExit()
    {
        using var outer = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions / 2);
        using (EnterOperation())
        {
            ConsumeReferences(MaximumReferenceResolutions / 2);
        }

        using var sibling = EnterOperation();
        Assert.Throws<SvgProcessingLimitException>(CountReference);
    }

    [Fact]
    public void IndependentOperationsRecoverAfterBudgetRejection()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = EnterOperation();
            ConsumeReferences(MaximumReferenceResolutions);
            Assert.Throws<SvgProcessingLimitException>(CountReference);
        }

        using var next = EnterOperation();
        CountReference();
    }

    [Fact]
    public void IndependentThreadsHaveIndependentReferenceBudgets()
    {
        using var outer = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);

        Exception? workerFailure = null;
        var worker = new Thread(() =>
        {
            try
            {
                using var scope = EnterOperation();
                CountReference();
            }
            catch (Exception exception)
            {
                workerFailure = exception;
            }
        });
        worker.Start();
        worker.Join();

        Assert.Null(workerFailure);
        Assert.Throws<SvgProcessingLimitException>(CountReference);
    }

    [Fact]
    public void MissingUseTargetsConsumeTheOuterOperationBudget()
    {
        using var svg = new SKSvg();
        using (EnterOperation())
        {
            ConsumeReferences(MaximumReferenceResolutions);
            var exception = Assert.Throws<SvgProcessingLimitException>(() =>
                svg.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><use href='#missing'/></svg>"));
            Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
        }

        Assert.NotNull(svg.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10'/></svg>"));
    }

    [Fact]
    public void CachedPaintReferencesStillConsumeWork()
    {
        var document = SvgDocument.FromSvg<SvgDocument>("""
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <defs><linearGradient id="paint"><stop offset="0" stop-color="red"/></linearGradient></defs>
              <rect id="shape" width="10" height="10" fill="url(#paint)"/>
            </svg>
            """);
        var shape = document.GetElementById<SvgRectangle>("shape");
        Assert.NotNull(SvgDeferredPaintServer.TryGet<SvgGradientServer>(shape.Fill, shape));

        using var scope = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);
        var exception = Assert.Throws<SvgProcessingLimitException>(() =>
            SvgDeferredPaintServer.TryGet<SvgGradientServer>(shape.Fill, shape));
        Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
    }

    [Fact]
    public void CurrentColorDoesNotConsumeAResourceReference()
    {
        var document = SvgDocument.FromSvg<SvgDocument>("""
            <svg xmlns="http://www.w3.org/2000/svg"><rect id="shape" color="red" fill="currentColor"/></svg>
            """);
        var shape = document.GetElementById<SvgRectangle>("shape");

        using var scope = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);
        Assert.NotNull(SvgDeferredPaintServer.TryGet<SvgColourServer>(shape.Fill, shape));
    }

    [Fact]
    public void ImageReferencesConsumeTheOuterOperationBudget()
    {
        using var svg = new SKSvg();
        using var scope = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);

        var exception = Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <image width="10" height="10" href="unavailable.svg"/>
            </svg>
            """));
        Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
    }

    [Fact]
    public void DirectPatternObjectsConsumeTheOuterOperationBudget()
    {
        var document = SvgDocument.FromSvg<SvgDocument>("""
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <defs><pattern id="paint" width="10" height="10" patternUnits="userSpaceOnUse">
                <rect width="10" height="10" fill="red"/>
              </pattern></defs>
              <rect id="shape" width="10" height="10"/>
            </svg>
            """);
        document.GetElementById<SvgRectangle>("shape").Fill = document.GetElementById<SvgPatternServer>("paint");

        using var svg = new SKSvg();
        using var scope = EnterOperation();
        ConsumeReferences(MaximumReferenceResolutions);
        var exception = Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvgDocument(document));
        Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
    }

    [Fact]
    public void ShallowAcyclicUseExpansionIsRejectedAndNextLoadSucceeds()
    {
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><defs><g id='g0'/>");
        for (var level = 1; level <= 8; level++)
        {
            source.Append($"<g id='g{level}'>");
            for (var sibling = 0; sibling < 8; sibling++)
            {
                source.Append($"<use href='#g{level - 1}'/>");
            }

            source.Append("</g>");
        }

        source.Append("</defs><use href='#g8'/></svg>");
        var document = SvgDocument.FromSvg<SvgDocument>(source.ToString());
        var definitions = document.GetElementById<SvgGroup>("g0").Parent;
        using var svg = new SKSvg();

        var exception = Assert.Throws<SvgProcessingLimitException>(() => svg.FromSvgDocument(document));
        Assert.Equal(SvgProcessingLimitKind.ReferenceCount, exception.Kind);
        Assert.Equal(MaximumReferenceResolutions, exception.Limit);
        for (var level = 0; level <= 8; level++)
        {
            Assert.Same(definitions, document.GetElementById<SvgGroup>($"g{level}").Parent);
        }

        Assert.NotNull(svg.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10'/></svg>"));
    }

    [Fact]
    public void DepthDiagnosticRetainsItsNumericLimit()
    {
        var exception = new SvgProcessingLimitException("element nesting", 256);
        Assert.Equal(SvgProcessingLimitKind.Depth, exception.Kind);
        Assert.Equal(256, exception.Limit);
        Assert.Contains("depth limit", exception.Message);
    }

    private static IDisposable EnterOperation()
    {
        return (IDisposable)EnterMethod.Invoke(null, new object[] { "reference budget regression" })!;
    }

    private static void ConsumeReferences(int count)
    {
        for (var i = 0; i < count; i++)
        {
            CountReference();
        }
    }
}
