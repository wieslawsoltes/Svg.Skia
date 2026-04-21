using System.Globalization;
using System.Security;

namespace SvgML;

public partial class foreignObject
{
    // Hangul Filler preserves a stretchable text advance without painting placeholder ink.
    private const string PlaceholderGlyph = "\u3164";

    protected override void Write(TextWriter writer, element parent)
    {
        if (Child is not null && IsInTextTree())
        {
            WriteInlinePlaceholder(writer);
            return;
        }

        WriteBeginStartTag(writer, parent);
        WriteXmlns(writer, parent);
        WriteAttributes(writer, parent);
        WriteGeneratedMappingId(writer);
        WriteMeasuredSizeAttributes(writer);
        WriteEndStartTag(writer, parent);
        WriteContents(writer, parent);
        WriteEndTag(writer, parent);
    }

    private void WriteInlinePlaceholder(TextWriter writer)
    {
        var placeholderSize = GetHostSlotSize().OrFallback();
        var widthValue = placeholderSize.Width.ToString("G7", CultureInfo.InvariantCulture);
        var heightValue = placeholderSize.Height.ToString("G7", CultureInfo.InvariantCulture);

        writer.WriteLine("<tspan");
        writer.WriteLine($"id=\"{ToSvgString(EffectiveMappingId)}\"");
        writer.WriteLine($"font-size=\"{heightValue}\"");
        writer.WriteLine($"textLength=\"{widthValue}\"");
        writer.WriteLine("lengthAdjust=\"spacingAndGlyphs\"");
        writer.WriteLine(">");
        writer.Write(SecurityElement.Escape(PlaceholderGlyph) ?? string.Empty);
        writer.WriteLine("</tspan>");
    }

    private void WriteGeneratedMappingId(TextWriter writer)
    {
        if (Child is not null && string.IsNullOrWhiteSpace(id))
        {
            writer.WriteLine($"id=\"{ToSvgString(EffectiveMappingId)}\"");
        }
    }

    private void WriteMeasuredSizeAttributes(TextWriter writer)
    {
        if (Child is null)
        {
            return;
        }

        var size = GetHostSlotSize();
        if (!IsWidthSet() && size.Width > 0D)
        {
            writer.WriteLine($"width=\"{size.Width.ToString("G7", CultureInfo.InvariantCulture)}\"");
        }

        if (!IsHeightSet() && size.Height > 0D)
        {
            writer.WriteLine($"height=\"{size.Height.ToString("G7", CultureInfo.InvariantCulture)}\"");
        }
    }
}
