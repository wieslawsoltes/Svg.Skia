using System.Text;

namespace CodeGenerator;

internal class AvaloniaGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        sb.AppendLine(
            value: $"""
                        public {newModifier}static readonly Avalonia.StyledProperty<{t}?> {ReplaceDash(p: p.Name)}Property =
                            Avalonia.AvaloniaProperty.Register<{classType}, {t}?>("{p.Name}");
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        sb.AppendLine(
            value: $$"""
                         public {{newModifier}}{{t}}? {{HandleReserved(propertyName)}}
                         {
                             get => GetValue({{propertyName}}Property);
                             set => SetValue({{propertyName}}Property, value);
                         }
                     """);
    }
}
