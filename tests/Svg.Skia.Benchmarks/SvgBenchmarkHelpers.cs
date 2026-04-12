using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia.Benchmarks;

internal static class SvgBenchmarkHelpers
{
    public static SvgDocument ParseDocument(SvgLoadPipelineBenchmarkScenario scenario)
    {
        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(scenario.SvgText);
        if (scenario.BaseUri is not null)
        {
            document.BaseUri = scenario.BaseUri;
        }

        return document;
    }

    public static SvgDocument ParseDocument(Stream stream, Uri? baseUri)
    {
        var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(stream, new SvgOptions());
        if (baseUri is not null)
        {
            document.BaseUri = baseUri;
        }

        return document;
    }

    public static SvgDocument ParseDocument(XmlReader reader, Uri? baseUri)
    {
        var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(reader);
        if (baseUri is not null)
        {
            document.BaseUri = baseUri;
        }

        return document;
    }

    public static byte[] GetUtf8Bytes(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    public static SKRect GetDocumentViewport(SvgDocument document)
    {
        var size = SvgService.GetDimensions(document);
        var bounds = SKRect.Create(size);
        if (!bounds.IsEmpty)
        {
            return bounds;
        }

        if (document.ViewBox.Width > 0f && document.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                document.ViewBox.MinX,
                document.ViewBox.MinY,
                document.ViewBox.Width,
                document.ViewBox.Height);
        }

        return SKRect.Empty;
    }

    public static SvgSceneNode GetTopLevelNode(SvgSceneDocument sceneDocument)
    {
        for (var i = 0; i < sceneDocument.Root.Children.Count; i++)
        {
            var candidate = sceneDocument.Root.Children[i];
            if (sceneDocument.CreateNodeModel(candidate) is not null)
            {
                return candidate;
            }
        }

        return sceneDocument.Root;
    }

    public static SvgSceneNode GetLeafNode(SvgSceneDocument sceneDocument)
    {
        return sceneDocument
            .Traverse()
            .First(node => node.Children.Count == 0 && sceneDocument.CreateNodeModel(node) is not null);
    }
}
