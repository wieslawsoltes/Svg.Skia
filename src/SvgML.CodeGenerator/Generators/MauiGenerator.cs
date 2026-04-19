using System.Text;

namespace CodeGenerator;

internal class MauiGenerator(GeneratorSettings settings) : Generator(settings)
{
    protected override void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType)
    {
        // TODO: Nullable value types are broken in maui
        // sb.AppendLine(
        //     value: $"""
        //                 public static readonly Microsoft.Maui.Controls.BindableProperty {ReplaceDash(p: p.Name)}Property = 
        //                     Microsoft.Maui.Controls.BindableProperty.Create("{p.Name}", typeof({t}?), typeof({classType}));
        //             """);
        sb.AppendLine(
            value: $"""
                        public static readonly Microsoft.Maui.Controls.BindableProperty {ReplaceDash(p: p.Name)}Property = 
                            Microsoft.Maui.Controls.BindableProperty.Create("{p.Name}", typeof({t}), typeof({classType}));
                    """);
    }

    protected override void ToClrProperty(StringBuilder sb, string t, string propertyName)
    {
        // TODO: Nullable value types are broken in maui
        // sb.AppendLine(
        //     value: $$"""
        //                  public {{t}}? {{HandleReserved(propertyName)}}
        //                  {
        //                      get => ({{t}}?)GetValue({{propertyName}}Property);
        //                      set => SetValue({{propertyName}}Property, value);
        //                  }
        //              """);
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
        // TODO: if ({{propertyName}} is not null)
        // TODO: Nullable value types are broken in maui
        sb.AppendLine(
            value: $$"""
                             if (this.IsSet({{propertyName}}Property))
                             {
                                 writer.WriteLine($"{{pns}}{{name}}=\"{ToSvgString({{propertyName}})}\"");
                             }
                     """);
    }
}
