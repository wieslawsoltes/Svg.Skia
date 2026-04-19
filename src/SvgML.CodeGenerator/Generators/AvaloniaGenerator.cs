using System.Text;

namespace CodeGenerator;

internal class AvaloniaGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType)
    {
        sb.AppendLine(
            value: $"""
                        public static readonly Avalonia.StyledProperty<{t}?> {ReplaceDash(p: p.Name)}Property =
                            Avalonia.AvaloniaProperty.Register<{classType}, {t}?>("{p.Name}");
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName)
    {
        sb.AppendLine(
            value: $$"""
                         public {{t}}? {{HandleReserved(propertyName)}}
                         {
                             get => GetValue({{propertyName}}Property);
                             set => SetValue({{propertyName}}Property, value);
                         }
                     """);
    }
}
