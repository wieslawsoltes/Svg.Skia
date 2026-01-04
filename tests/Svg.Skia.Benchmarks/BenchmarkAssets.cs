using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Svg.Skia.Benchmarks;

internal static class BenchmarkAssets
{
    private static readonly string[] s_svgNames =
    {
        "HitTest.svg",
        "HitTestText.svg",
        "__tiger.svg",
        "__Telefunken_FuBK_test_pattern.svg"
    };

    private static readonly Dictionary<string, string> s_textCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, byte[]> s_bytesCache = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> SvgNames => s_svgNames;

    public static string GetSvgText(string name)
    {
        if (!s_textCache.TryGetValue(name, out var text))
        {
            var bytes = GetSvgBytes(name);
            text = Encoding.UTF8.GetString(bytes);
            s_textCache[name] = text;
        }

        return text;
    }

    public static byte[] GetSvgBytes(string name)
    {
        if (!s_bytesCache.TryGetValue(name, out var bytes))
        {
            using var stream = GetResourceStream(name);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            bytes = memoryStream.ToArray();
            s_bytesCache[name] = bytes;
        }

        return bytes;
    }

    private static Stream GetResourceStream(string name)
    {
        var resourceName = $"Svg.Skia.Benchmarks.Resources.{name}";
        var stream = typeof(BenchmarkAssets).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Missing benchmark asset: {resourceName}.");
        }

        return stream;
    }
}
