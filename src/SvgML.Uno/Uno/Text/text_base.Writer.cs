using System.Security;

namespace SvgML;

public abstract partial class text_base
{
    protected override void WriteContents(TextWriter writer, element parent)
    {
        if (!string.IsNullOrEmpty(Text))
        {
            writer.Write(SecurityElement.Escape(Text) ?? string.Empty);
        }

        base.WriteContents(writer, parent);
    }
}
