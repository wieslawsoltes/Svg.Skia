using System.Globalization;
using Svg;

namespace SvgML;

public abstract partial class element
{
    protected string ToSvgString(string value)
    {
        return value;
    }

    protected string ToSvgString(Enum value)
    {
        return value.ToString().Replace('_', '-');
    }

    protected string ToSvgString(float value)
    {
        return value.ToString("G7", CultureInfo.InvariantCulture);
    }

    protected string ToSvgString(float? value)
    {
        return ToSvgString(value.Value);
    }

    protected string ToSvgString(SvgUnit value)
    {
        return value.ToString();
    }

    protected string ToSvgString(SvgUnit? value)
    {
        return ToSvgString(value.Value);
    }

    protected string ToSvgString(SvgNumberCollection value)
    {
        return value.ToString();
    }

    protected string ToSvgString(numbers value)
    {
        return value.ToString();
    }

    protected string ToSvgString(element parent)
    {
        using var writer = new StringWriter();

        Write(writer, parent);

        return writer.ToString();
    }

    protected virtual void Write(TextWriter writer, element parent)
    {
        WriteBeginStartTag(writer, parent);
        WriteXmlns(writer, parent);
        WriteAttributes(writer, parent);
        WriteEndStartTag(writer, parent);
        WriteContents(writer, parent);
        WriteEndTag(writer, parent);
    }

    protected virtual void WriteXmlns(TextWriter writer, element parent)
    {
    }

    protected virtual void WriteBeginStartTag(TextWriter writer, element parent)
    {
        writer.WriteLine($"<{SvgTag}");
    }

    protected virtual void WriteEndStartTag(TextWriter writer, element parent)
    {
        writer.WriteLine(">");
    }

    protected virtual void WriteEndTag(TextWriter writer, element parent)
    {
        writer.WriteLine($"</{SvgTag}>");
    }

    protected virtual void WriteContents(TextWriter writer, element parent)
    {
        foreach (var child in Children)
        {
            child.Write(writer, parent);
        }
    }
}
