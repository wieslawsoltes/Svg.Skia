using System;
using System.Text;

namespace CodeGenerator;

internal class MauiGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override bool UsesEnumBridge(string type)
    {
        return IsEnumBackedType(type);
    }

    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        // TODO: Nullable value types are broken in maui
        // sb.AppendLine(
        //     value: $"""
        //                 public {newModifier}static readonly Microsoft.Maui.Controls.BindableProperty {ReplaceDash(p: p.Name)}Property = 
        //                     Microsoft.Maui.Controls.BindableProperty.Create("{p.Name}", typeof({t}?), typeof({classType}));
        //             """);
        sb.AppendLine(
            value: $"""
                        public {newModifier}static readonly Microsoft.Maui.Controls.BindableProperty {ReplaceDash(p: p.Name)}Property = 
                            Microsoft.Maui.Controls.BindableProperty.Create("{p.Name}", typeof({t}), typeof({classType}));
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName, bool hidesInheritedProperty)
    {
        var newModifier = hidesInheritedProperty ? "new " : string.Empty;
        // TODO: Nullable value types are broken in maui
        // sb.AppendLine(
        //     value: $$"""
        //                  public {{newModifier}}{{t}}? {{HandleReserved(propertyName)}}
        //                  {
        //                      get => ({{t}}?)GetValue({{propertyName}}Property);
        //                      set => SetValue({{propertyName}}Property, value);
        //                  }
        //              """);
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
                                 if (this.IsSet({{propertyName}}Property))
                                 {
                                     if (!this.IsSet(marker_startProperty))
                                     {
                                         writer.WriteLine($"marker-start=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (!this.IsSet(marker_midProperty))
                                     {
                                         writer.WriteLine($"marker-mid=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (!this.IsSet(marker_endProperty))
                                     {
                                         writer.WriteLine($"marker-end=\"{ToSvgString({{propertyName}})}\"");
                                     }
                                 }
                         """);
            return;
        }

        sb.AppendLine(
            value: $$"""
                             if (this.IsSet({{propertyName}}Property))
                             {
                                 writer.WriteLine($"{{pns}}{{name}}=\"{ToSvgString({{propertyName}})}\"");
                             }
                     """);
    }
}
