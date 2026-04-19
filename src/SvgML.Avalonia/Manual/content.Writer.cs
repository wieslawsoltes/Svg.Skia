namespace SvgML;

internal partial class content
{
    protected override void Write(TextWriter writer, element parent)
    {
        WriteContents(writer, parent);
    }

    protected override void WriteXmlns(TextWriter writer, element parent)
    {
    }

    protected override void WriteBeginStartTag(TextWriter writer, element parent)
    {
    }

    protected override void WriteEndStartTag(TextWriter writer, element parent)
    {
    }

    protected override void WriteEndTag(TextWriter writer, element parent)
    {
    }

    protected override void WriteAttributes(TextWriter writer, element parent)
    {
    }

    protected override void WriteContents(TextWriter writer, element parent)
    {
        writer.Write(Content);
    }
}
