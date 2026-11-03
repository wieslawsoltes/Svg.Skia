using System;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingParserLimitTests
{
    // Publicly documented depth includes the document root and the leaf element.
    private const int MaximumElementDepth = 256;
    private const string SimpleSvg = "<svg xmlns='http://www.w3.org/2000/svg'><rect id='target' width='10' height='10' fill='red'/></svg>";

    public enum LoadingApi
    {
        DocumentString,
        DocumentStream,
        DocumentNonSeekableStream,
        DocumentXmlReader,
        DocumentXmlDocument,
        DocumentPath,
        CompatibilityString,
        CompatibilityStream,
        CompatibilityNonSeekableStream,
        CompatibilityXmlReader,
        CompatibilityPath
    }

    public static TheoryData<LoadingApi> LoadingApis => new()
    {
        LoadingApi.DocumentString,
        LoadingApi.DocumentStream,
        LoadingApi.DocumentNonSeekableStream,
        LoadingApi.DocumentXmlReader,
        LoadingApi.DocumentXmlDocument,
        LoadingApi.DocumentPath,
        LoadingApi.CompatibilityString,
        LoadingApi.CompatibilityStream,
        LoadingApi.CompatibilityNonSeekableStream,
        LoadingApi.CompatibilityXmlReader,
        LoadingApi.CompatibilityPath
    };

    [Theory]
    [MemberData(nameof(LoadingApis))]
    public void LoadingApis_RejectDeepInputAndRecover(LoadingApi api)
    {
        Assert.Throws<SvgProcessingLimitException>(() => Load(api, CreateNestedSvg(10000)));

        var valid = Load(api, SimpleSvg);
        Assert.IsType<SvgRectangle>(valid.GetElementById("target"));
    }

    [Theory]
    [MemberData(nameof(LoadingApis))]
    public void LoadingApis_AcceptTheDocumentedDepthBoundary(LoadingApi api)
    {
        var document = Load(api, CreateNestedSvg(MaximumElementDepth));
        SvgElement element = document;
        var depth = 1;
        while (element.Children.Count > 0)
        {
            element = Assert.Single(element.Children);
            depth++;
        }

        Assert.Equal(MaximumElementDepth, depth);
        Assert.IsType<SvgRectangle>(element);
        Assert.IsType<SvgColourServer>(Assert.IsType<SvgRectangle>(element).Fill);
    }

    [Theory]
    [MemberData(nameof(LoadingApis))]
    public void LoadingApis_RejectOneElementPastTheBoundary(LoadingApi api)
    {
        Assert.Throws<SvgProcessingLimitException>(() => Load(api, CreateNestedSvg(MaximumElementDepth + 1)));
    }

    [Fact]
    public void DocumentStylesheet_DoesNotLeaveAnExtraParentBeforeRendering()
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><style>rect { fill: red; }</style><defs>");
        for (var i = 3; i < MaximumElementDepth; i++)
        {
            svg.Append("<g>");
        }

        svg.Append("<rect id='definition' width='1' height='1'/>");
        for (var i = 3; i < MaximumElementDepth; i++)
        {
            svg.Append("</g>");
        }

        svg.Append("</defs><rect width='10' height='10'/></svg>");
        var document = SvgDocument.FromSvg<SvgDocument>(svg.ToString());

        Assert.Null(document.Parent);
        using var rendered = new SKSvg();
        Assert.NotNull(rendered.FromSvgDocument(document));
    }

    [Fact]
    public void DocumentStylesheet_PreservesRootCustomPropertiesWhenRemovingTheSelectorParent()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg'><style>:root { --paint: red; } rect { fill: var(--paint); }</style><rect id='target' width='10' height='10'/></svg>";
        var document = SvgDocument.FromSvg<SvgDocument>(svg);

        Assert.Null(document.Parent);
        var rectangle = Assert.IsType<SvgRectangle>(document.GetElementById("target"));
        var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
        Assert.Equal(System.Drawing.Color.Red.ToArgb(), fill.Colour.ToArgb());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Loading_DoesNotConfuseSiblingCountWithDepth(bool compatibilityLoader)
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg'>");
        for (var i = 0; i < 2000; i++)
        {
            svg.Append("<rect width='1' height='1'/>");
        }

        svg.Append("</svg>");
        var document = Load(compatibilityLoader ? LoadingApi.CompatibilityString : LoadingApi.DocumentString, svg.ToString());

        Assert.Equal(2000, document.Children.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Loading_LeavesCallerStreamsOpenAfterRejection(bool compatibilityLoader, bool nonSeekable)
    {
        using Stream stream = CreateStream(CreateNestedSvg(10000), nonSeekable);

        Assert.Throws<SvgProcessingLimitException>(() => LoadStream(stream, compatibilityLoader));

        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Loading_PropagatesLimitsFromGeneratedAttributeConverters(bool compatibilityLoader)
    {
        // These url fallbacks reach the generated Fill setter during FlushStyles.
        // Its normal malformed-attribute recovery must not swallow a safety limit.
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg'><rect fill='");
        for (var i = 0; i < 1024; i++)
        {
            svg.Append("url(#missing) ");
        }

        svg.Append("red'/></svg>");

        Assert.Throws<SvgProcessingLimitException>(() =>
            Load(compatibilityLoader ? LoadingApi.CompatibilityString : LoadingApi.DocumentString, svg.ToString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Loading_StillToleratesOrdinaryMalformedAttributes(bool compatibilityLoader)
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg'><rect id='target' width='invalid' height='10' fill='red'/></svg>";
        var document = Load(compatibilityLoader ? LoadingApi.CompatibilityString : LoadingApi.DocumentString, svg);

        var rectangle = Assert.IsType<SvgRectangle>(document.GetElementById("target"));
        Assert.Equal(10f, rectangle.Height.Value);
        Assert.IsType<SvgColourServer>(rectangle.Fill);
    }

    private static string CreateNestedSvg(int depth)
    {
        var builder = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg'>");
        for (var i = 2; i < depth; i++)
        {
            builder.Append("<g>");
        }

        builder.Append("<rect id='target' width='1' height='1' fill='red'/>");
        for (var i = 2; i < depth; i++)
        {
            builder.Append("</g>");
        }

        return builder.Append("</svg>").ToString();
    }

    private static SvgDocument Load(LoadingApi api, string svg)
    {
        switch (api)
        {
            case LoadingApi.DocumentString:
                return SvgDocument.FromSvg<SvgDocument>(svg);
            case LoadingApi.CompatibilityString:
                return SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
            case LoadingApi.DocumentStream:
            case LoadingApi.DocumentNonSeekableStream:
            case LoadingApi.CompatibilityStream:
            case LoadingApi.CompatibilityNonSeekableStream:
                using (var stream = CreateStream(svg, api is LoadingApi.DocumentNonSeekableStream or LoadingApi.CompatibilityNonSeekableStream))
                {
                    return LoadStream(stream, api is LoadingApi.CompatibilityStream or LoadingApi.CompatibilityNonSeekableStream);
                }

            case LoadingApi.DocumentXmlReader:
            case LoadingApi.CompatibilityXmlReader:
                using (var textReader = new StringReader(svg))
                using (var xmlReader = XmlReader.Create(textReader))
                {
                    return api == LoadingApi.DocumentXmlReader
                        ? SvgDocument.Open<SvgDocument>(xmlReader)
                        : SvgDocumentCompatibilityLoader.Open<SvgDocument>(xmlReader);
                }

            case LoadingApi.DocumentXmlDocument:
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(svg);
                return SvgDocument.Open(xmlDocument);
            case LoadingApi.DocumentPath:
            case LoadingApi.CompatibilityPath:
                var path = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(path, svg);
                    return api == LoadingApi.DocumentPath
                        ? SvgDocument.Open(path)
                        : SvgDocumentCompatibilityLoader.Open<SvgDocument>(path, new SvgOptions());
                }
                finally
                {
                    File.Delete(path);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(api));
        }
    }

    private static SvgDocument LoadStream(Stream stream, bool compatibilityLoader)
    {
        return compatibilityLoader
            ? SvgDocumentCompatibilityLoader.Open<SvgDocument>(stream, new SvgOptions())
            : SvgDocument.Open<SvgDocument>(stream);
    }

    private static Stream CreateStream(string svg, bool nonSeekable)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return nonSeekable ? new NonSeekableReadStream(stream) : stream;
    }

    private sealed class NonSeekableReadStream(Stream source) : Stream
    {
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
