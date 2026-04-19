using System.Text;

namespace CodeGenerator;

internal sealed class UnoGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType)
    {
        sb.AppendLine(
            value: $"""
                        public static readonly Microsoft.UI.Xaml.DependencyProperty {ReplaceDash(p: p.Name)}Property =
                            Microsoft.UI.Xaml.DependencyProperty.Register(
                                "{p.Name}",
                                typeof({t}),
                                typeof({classType}),
                                new Microsoft.UI.Xaml.PropertyMetadata(default({t}), OnSvgPropertyChanged));
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName)
    {
        sb.AppendLine(
            value: $$"""
                         public {{t}} {{HandleReserved(propertyName)}}
                         {
                             get => ({{t}})GetValue({{propertyName}}Property);
                             set => SetValue({{propertyName}}Property, value);
                         }
                     """);
    }

    protected override void ToSetProperty(StringBuilder sb, string propertyName, string pns, string name)
    {
        sb.AppendLine(
            value: $$"""
                             if (ReadLocalValue({{propertyName}}Property) != Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                             {
                                 writer.WriteLine($"{{pns}}{{name}}=\"{ToSvgString({{propertyName}})}\"");
                             }
                     """);
    }
}
