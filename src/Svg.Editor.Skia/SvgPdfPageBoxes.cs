using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Svg.Editor.Skia;

/// <summary>Adds physical TrimBox/BleedBox values through a standard PDF incremental update.</summary>
internal static class SvgPdfPageBoxes
{
    public static string BoxDictionary(SvgPrintPage page)
    {
        string Box(SkiaSharp.SKRect box) => FormattableString.Invariant($"[{box.Left:0.#####} {page.MediaSize.Height - box.Bottom:0.#####} {box.Right:0.#####} {page.MediaSize.Height - box.Top:0.#####}]");
        return "/TrimBox " + Box(page.TrimBox) + " /BleedBox " + Box(page.BleedBox);
    }

    public static byte[] Apply(byte[] pdf, IReadOnlyList<SvgPrintPage> pages)
    {
        // This reader is intentionally scoped to PDFs produced by Skia in this process.
        // It reads object offsets from xref, so compressed stream bytes are never parsed as objects.
        var encoding = Encoding.GetEncoding(28591);
        var text = encoding.GetString(pdf);
        var marker = text.LastIndexOf("startxref", StringComparison.Ordinal);
        if (marker < 0)
            throw new InvalidDataException("The PDF encoder did not write a cross-reference table.");
        var start = marker + 9;
        string Token()
        {
            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;
            var begin = start;
            while (start < text.Length && !char.IsWhiteSpace(text[start]))
                start++;
            return text.Substring(begin, start - begin);
        }
        var previousXref = int.Parse(Token(), CultureInfo.InvariantCulture);
        start = previousXref;
        if (Token() != "xref")
            throw new InvalidDataException("The PDF encoder uses an unsupported cross-reference layout.");
        var offsets = new Dictionary<int, int>();
        while (true)
        {
            var token = Token();
            if (token == "trailer")
                break;
            var first = int.Parse(token, CultureInfo.InvariantCulture);
            var count = int.Parse(Token(), CultureInfo.InvariantCulture);
            if (count < 0 || count > 1_000_000)
                throw new InvalidDataException("The PDF xref table is invalid.");
            for (var i = 0; i < count; i++)
            {
                var offset = int.Parse(Token(), CultureInfo.InvariantCulture);
                _ = Token();
                if (Token() == "n")
                    offsets.Add(first + i, offset);
            }
        }
        var trailer = text.Substring(start, marker - start);
        var root = Regex.Match(trailer, @"/Root\s+(\d+)\s+(\d+)\s+R");
        var size = Regex.Match(trailer, @"/Size\s+(\d+)");
        if (!root.Success || !size.Success)
            throw new InvalidDataException("The PDF trailer is incomplete.");
        var sorted = offsets.Values.Concat(new[] { previousXref }).OrderBy(value => value).ToArray();
        string ObjectBody(int id)
        {
            if (!offsets.TryGetValue(id, out var at))
                throw new InvalidDataException("A PDF page-tree object is missing.");
            var end = sorted[Array.BinarySearch(sorted, at) + 1];
            var body = text.Substring(at, end - at);
            var first = body.IndexOf("obj", StringComparison.Ordinal) + 3;
            var last = body.LastIndexOf("endobj", StringComparison.Ordinal);
            if (first < 3 || last < first)
                throw new InvalidDataException("A PDF page-tree object is malformed.");
            return body.Substring(first, last - first).Trim();
        }
        var catalog = ObjectBody(int.Parse(root.Groups[1].Value, CultureInfo.InvariantCulture));
        var pageTree = Regex.Match(catalog, @"/Pages\s+(\d+)\s+\d+\s+R");
        if (!pageTree.Success)
            throw new InvalidDataException("The PDF page tree is missing.");
        var pageIds = new List<int>();
        var visited = new HashSet<int>();
        void Visit(int id)
        {
            if (!visited.Add(id) || visited.Count > 1024)
                throw new InvalidDataException("The PDF page tree contains a cycle.");
            var body = ObjectBody(id);
            if (Regex.IsMatch(body, @"/Type\s*/Page(?:\s|/|>)"))
            { pageIds.Add(id); return; }
            var kids = Regex.Match(body, @"/Kids\s*\[([^\]]*)\]", RegexOptions.Singleline);
            if (!kids.Success)
                throw new InvalidDataException("The PDF page tree has no children.");
            foreach (Match reference in Regex.Matches(kids.Groups[1].Value, @"(\d+)\s+\d+\s+R"))
                Visit(int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture));
        }
        Visit(int.Parse(pageTree.Groups[1].Value, CultureInfo.InvariantCulture));
        if (pageIds.Count != pages.Count)
            throw new InvalidDataException("The PDF page count differs from the print layout.");
        using var output = new MemoryStream();
        output.Write(pdf, 0, pdf.Length);
        void Write(string value)
        { var bytes = encoding.GetBytes(value); output.Write(bytes, 0, bytes.Length); }
        var updates = new SortedDictionary<int, long>();
        for (var i = 0; i < pageIds.Count; i++)
        {
            var id = pageIds[i];
            var body = ObjectBody(id);
            body = Regex.Replace(body, @"/(MediaBox|CropBox|TrimBox|BleedBox)\s*\[[^\]]*\]", string.Empty);
            var closing = body.LastIndexOf(">>", StringComparison.Ordinal);
            if (closing < 0)
                throw new InvalidDataException("The PDF page dictionary is malformed.");
            var media = FormattableString.Invariant($"/MediaBox [0 0 {pages[i].MediaSize.Width:0.#####} {pages[i].MediaSize.Height:0.#####}] ");
            body = body.Insert(closing, " " + media + BoxDictionary(pages[i]) + " ");
            Write("\n");
            updates.Add(id, output.Position);
            Write($"{id} 0 obj\n{body}\nendobj\n");
        }
        var xref = output.Position;
        Write("xref\n");
        foreach (var update in updates)
            Write($"{update.Key} 1\n{update.Value.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
        var info = Regex.Match(trailer, @"/Info\s+\d+\s+\d+\s+R");
        Write($"trailer\n<< /Size {size.Groups[1].Value} /Root {root.Groups[1].Value} {root.Groups[2].Value} R {info.Value} /Prev {previousXref} >>\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        return output.ToArray();
    }
}
