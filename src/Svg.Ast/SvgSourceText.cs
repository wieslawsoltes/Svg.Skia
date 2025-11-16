// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Language.Xml;

namespace Svg.Ast;

/// <summary>
/// Represents the raw SVG source buffer and provides span-based helpers without copying data.
/// </summary>
public sealed class SvgSourceText
{
    private readonly string? _stringBacking;
    private int[]? _lineStarts;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSourceText"/> class.
    /// </summary>
    /// <param name="buffer">The SVG text stored as <see cref="ReadOnlyMemory{T}"/>.</param>
    /// <param name="encoding">Encoding information, defaults to UTF-8 when null.</param>
    /// <param name="sourceName">Optional source identifier (file path, in-memory label, etc.).</param>
    /// <param name="stringBacking">Optional string used to back the buffer for zero-copy parser integration.</param>
    public SvgSourceText(ReadOnlyMemory<char> buffer, Encoding? encoding = null, string? sourceName = null, string? stringBacking = null)
    {
        Content = buffer;
        Encoding = encoding ?? Encoding.UTF8;
        SourceName = sourceName;
        _stringBacking = stringBacking;
    }

    /// <summary>
    /// Gets the full SVG content as <see cref="ReadOnlyMemory{Char}"/>.
    /// </summary>
    public ReadOnlyMemory<char> Content { get; }

    /// <summary>
    /// Gets the encoding used to decode the SVG text.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// Gets an optional source identifier (file path, resource name, etc.).
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    /// Gets the length of the content in characters.
    /// </summary>
    public int Length => Content.Length;

    /// <summary>
    /// Gets the line/column information for the specified absolute position.
    /// </summary>
    public SvgLinePosition GetLinePosition(int position)
    {
        if (Length == 0)
        {
            return new SvgLinePosition(1, 1);
        }

        var lineStarts = EnsureLineStarts();
        var clamped = ClampToRange(position, 0, Length);
        var lineIndex = GetLineIndex(lineStarts, clamped);
        var column = (clamped - lineStarts[lineIndex]) + 1;
        return new SvgLinePosition(lineIndex + 1, column);
    }

    /// <summary>
    /// Creates an instance from an existing <see cref="string"/>.
    /// </summary>
    public static SvgSourceText FromString(string text, Encoding? encoding = null, string? sourceName = null, bool normalizeLineEndings = true)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var normalized = normalizeLineEndings ? NormalizeLineEndings(text) : text;
        var encodingToUse = encoding ?? Encoding.UTF8;
        return new SvgSourceText(normalized.AsMemory(), encodingToUse, sourceName, normalized);
    }

    /// <summary>
    /// Creates an instance from a file on disk.
    /// </summary>
    public static SvgSourceText FromFile(string path, Encoding? encoding = null, bool normalizeLineEndings = true)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var encodingToUse = encoding ?? Encoding.UTF8;
        using var reader = new StreamReader(path, encodingToUse, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var finalEncoding = reader.CurrentEncoding ?? encodingToUse;
        return FromString(text, finalEncoding, path, normalizeLineEndings);
    }

    /// <summary>
    /// Creates an instance from a stream.
    /// </summary>
    public static SvgSourceText FromStream(Stream stream, Encoding? encoding = null, string? sourceName = null, bool normalizeLineEndings = true)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var encodingToUse = encoding ?? Encoding.UTF8;
        using var reader = new StreamReader(stream, encodingToUse, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        var text = reader.ReadToEnd();
        var finalEncoding = reader.CurrentEncoding ?? encodingToUse;
        return FromString(text, finalEncoding, sourceName, normalizeLineEndings);
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Char}"/> covering the entire source buffer.
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => Content.Span;

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Char}"/> that starts at <paramref name="start"/> and extends to the end of the buffer.
    /// </summary>
    public ReadOnlySpan<char> Slice(int start)
    {
        ValidateRange(start, Length - start);
        return Content.Span.Slice(start);
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Char}"/> for the specified range.
    /// </summary>
    public ReadOnlySpan<char> Slice(int start, int length)
    {
        ValidateRange(start, length);
        return Content.Span.Slice(start, length);
    }

    /// <summary>
    /// Copies the specified range to a new <see cref="string"/>.
    /// </summary>
    public string SliceToString(int start, int length)
    {
        ValidateRange(start, length);
        return Content.Slice(start, length).ToString();
    }

    /// <summary>
    /// Returns the entire SVG text as a string.
    /// </summary>
    public override string ToString()
    {
        return _stringBacking ?? Content.ToString();
    }

    /// <summary>
    /// Converts the source text into a parser buffer usable by <see cref="Parser"/>.
    /// </summary>
    public StringBuffer ToParserBuffer()
    {
        return new StringBuffer(ToString());
    }

    /// <summary>
    /// Parses the SVG text into an <see cref="XmlDocumentSyntax"/> using <see cref="Parser"/>.
    /// </summary>
    public XmlDocumentSyntax ParseXmlDocument()
    {
        return Parser.Parse(ToParserBuffer());
    }

    /// <summary>
    /// Parses the SVG text incrementally using previous syntax information.
    /// </summary>
    public XmlDocumentSyntax ParseXmlDocumentIncremental(XmlDocumentSyntax previousDocument, TextChangeRange[] changes)
    {
        if (previousDocument is null)
        {
            throw new ArgumentNullException(nameof(previousDocument));
        }

        if (changes is null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        var buffer = ToParserBuffer();
        return Parser.ParseIncremental(buffer, changes, previousDocument);
    }

    private void ValidateRange(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start index must be non-negative.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        }

        if (start > Length || start + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The specified range exceeds the buffer length.");
        }
    }

    private static string NormalizeLineEndings(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                builder.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private int[] EnsureLineStarts()
    {
        return _lineStarts ??= ComputeLineStarts();
    }

    private int[] ComputeLineStarts()
    {
        var text = ToString();
        if (text.Length == 0)
        {
            return new[] { 0 };
        }

        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
            else if (ch == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }

    private static int GetLineIndex(int[] lineStarts, int position)
    {
        var index = Array.BinarySearch(lineStarts, position);
        if (index >= 0)
        {
            return index;
        }

        index = (~index) - 1;
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= lineStarts.Length)
        {
            index = lineStarts.Length - 1;
        }

        return index;
    }

    private static int ClampToRange(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
