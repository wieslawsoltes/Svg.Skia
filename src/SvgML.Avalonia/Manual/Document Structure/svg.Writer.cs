namespace SvgML;

public partial class svg
{
    protected override void WriteXmlns(TextWriter writer, element parent)
    {
        writer.WriteLine("xmlns=\"http://www.w3.org/2000/svg\"");
        writer.WriteLine("xmlns:xlink=\"http://www.w3.org/1999/xlink\"");
    }
}
