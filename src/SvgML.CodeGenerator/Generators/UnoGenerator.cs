using System;
using System.Text;

namespace CodeGenerator;

internal sealed class UnoGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        sb.AppendLine(
            value: $"""
                        public {newModifier}static readonly Microsoft.UI.Xaml.DependencyProperty {ReplaceDash(p: p.Name)}Property =
                            Microsoft.UI.Xaml.DependencyProperty.Register(
                                "{p.Name}",
                                typeof({t}),
                                typeof({classType}),
                                new Microsoft.UI.Xaml.PropertyMetadata(default({t}), OnSvgPropertyChanged));
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        sb.AppendLine(
            value: $$"""
                         public {{newModifier}}{{t}} {{HandleReserved(propertyName)}}
                         {
                             get => ({{t}})GetValue({{propertyName}}Property);
                             set => SetValue({{propertyName}}Property, value);
                         }
                     """);
    }

    protected override void ToSetProperty(StringBuilder sb, string propertyName, string pns, string name)
    {
        if (string.Equals(name, "marker", StringComparison.Ordinal))
        {
            sb.AppendLine(
                value: $$"""
                                 if (ReadLocalValue({{propertyName}}Property) != Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                                 {
                                     if (ReadLocalValue(marker_startProperty) == Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                                     {
                                         writer.WriteLine($"marker-start=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (ReadLocalValue(marker_midProperty) == Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                                     {
                                         writer.WriteLine($"marker-mid=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (ReadLocalValue(marker_endProperty) == Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                                     {
                                         writer.WriteLine($"marker-end=\"{ToSvgString({{propertyName}})}\"");
                                     }
                                 }
                         """);
            return;
        }

        sb.AppendLine(
            value: $$"""
                             if (ReadLocalValue({{propertyName}}Property) != Microsoft.UI.Xaml.DependencyProperty.UnsetValue)
                             {
                                 writer.WriteLine($"{{pns}}{{name}}=\"{ToSvgString({{propertyName}})}\"");
                             }
                     """);
    }
}
