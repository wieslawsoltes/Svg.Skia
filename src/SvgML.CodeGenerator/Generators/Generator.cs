using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeGenerator;

internal abstract class Generator(GeneratorSettings settings)
{
    private GeneratorSettings Settings { get; } = settings;
    private static readonly HashSet<string> ScalarTypes = new(StringComparer.Ordinal)
    {
        "string",
        "bool",
        "float",
        "int",
        "numbers",
        "Svg.SvgUnit"
    };

    private bool HidesInheritedProperty(TypeDef typeDef, PropertyDef property)
    {
        var baseType = typeDef.BaseType;
        while (TryGetTypeDef(baseType, out var baseTypeDef))
        {
            if (baseTypeDef.Properties.Any(p => string.Equals(p.Name, property.Name, StringComparison.Ordinal)))
            {
                return true;
            }

            baseType = baseTypeDef.BaseType;
        }

        return false;
    }

    private bool TryGetTypeDef(string targetType, out TypeDef typeDef)
    {
        for (var i = 0; i < Settings.TypeDefs.Length; i++)
        {
            if (string.Equals(Settings.TypeDefs[i].TargetTpe, targetType, StringComparison.Ordinal))
            {
                typeDef = Settings.TypeDefs[i];
                return true;
            }
        }

        typeDef = null!;
        return false;
    }

    public virtual void Generate()
    {
        GenerateEnumSupport();
        GenerateProperties();
        GenerateWriters();
    }

    protected string ReplaceDash(string p)
    {
        return p.Replace(oldValue: "-", newValue: "_");
    }

    protected string HandleReserved(string s)
    {
        return Settings.ReservedWords.Contains(s) ? $"@{s}" : s;
    }

    protected virtual bool UsesEnumBridge(string type)
    {
        return false;
    }

    protected virtual bool UsesCreateFromStringBridge()
    {
        return false;
    }

    protected bool IsEnumBackedType(string type)
    {
        return !ScalarTypes.Contains(type);
    }

    protected string GetRawClrType(string type)
    {
        return HandleReserved(ReplaceDash(type));
    }

    protected string GetPropertyType(string type)
    {
        return UsesEnumBridge(type) ? GetEnumBridgeTypeName(type) : GetRawClrType(type);
    }

    protected string GetEnumBridgeTypeName(string type)
    {
        var rawType = type.StartsWith("Svg.", StringComparison.Ordinal) ? type["Svg.".Length..] : type;
        var sb = new StringBuilder(rawType.Length + "Value".Length);
        var nextUpper = true;

        for (var i = 0; i < rawType.Length; i++)
        {
            var c = rawType[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(nextUpper ? char.ToUpperInvariant(c) : c);
                nextUpper = false;
            }
            else
            {
                nextUpper = true;
            }
        }

        sb.Append("Value");

        return sb.ToString();
    }

    protected abstract void ToFrameworkProperty(StringBuilder sb, string t, PropertyDef p, string classType, bool hidesInheritedProperty);

    protected abstract void ToClrProperty(StringBuilder sb, string t, string propertyName, bool hidesInheritedProperty);

    protected virtual void ToSetProperty(StringBuilder sb, string propertyName, string pns, string name)
    {
        if (string.Equals(name, "marker", StringComparison.Ordinal))
        {
            sb.AppendLine(
                value: $$"""
                                 if ({{propertyName}} is not null)
                                 {
                                     if (marker_start is null)
                                     {
                                         writer.WriteLine($"marker-start=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (marker_mid is null)
                                     {
                                         writer.WriteLine($"marker-mid=\"{ToSvgString({{propertyName}})}\"");
                                     }

                                     if (marker_end is null)
                                     {
                                         writer.WriteLine($"marker-end=\"{ToSvgString({{propertyName}})}\"");
                                     }
                                 }
                         """);
            return;
        }

        sb.AppendLine(
            value: $$"""
                             if ({{propertyName}} is not null)
                             {
                                 writer.WriteLine($"{{pns}}{{name}}=\"{ToSvgString({{propertyName}})}\"");
                             }
                     """);
    }

    protected virtual void AppendAdditionalWriterAttributes(StringBuilder sb, TypeDef typeDef)
    {
    }

    private void GenerateProperties(TypeDef typeDef)
    {
        var sb = new StringBuilder();

        var ns = Settings.Namespace;
        var isAbstract = typeDef.IsAbstract;
        var className = ReplaceDash(p: typeDef.TargetTpe);
        var baseType = typeDef.TargetTpe == Settings.BaseWriterType
            ? Settings.ElementBaseType
            : ReplaceDash(p: typeDef.BaseType);

        // Begin Class

        sb.AppendLine(
            value: $$"""
                     // <auto-generated />
                     #nullable enable

                     namespace {{ns}};
 
                     public{{(isAbstract ? " abstract" : "")}} partial class {{HandleReserved(className)}} : {{HandleReserved(baseType)}}
                     {
                     """);

        // Svg Tag

        if (typeDef.TargetTpe == Settings.BaseWriterType)
        {
            sb.AppendLine(
                value: $$"""
                             protected abstract string SvgTag { get; }
                         """);

            if (typeDef.Properties.Length > 0)
            {
                sb.AppendLine(
                    value: $$"""

                             """);
            }
        }
        else
        {
            if (!isAbstract)
            {
                sb.AppendLine(
                    value: $$"""
                                 protected override string SvgTag => "{{typeDef.TargetTpe}}";
                             """);

                if (typeDef.Properties.Length > 0)
                {
                    sb.AppendLine(
                        value: $$"""

                                 """);
                }
            }
        }

        // Register Avalonia Properties

        for (var j = 0; j < typeDef.Properties.Length; j++)
        {
            var p = typeDef.Properties[j];
            var t = GetPropertyType(p.Type);
            var classType = HandleReserved(ReplaceDash(p: typeDef.TargetTpe));
            var hidesInheritedProperty = HidesInheritedProperty(typeDef, p);

            ToFrameworkProperty(sb, t, p, classType, hidesInheritedProperty);

            sb.AppendLine(value: "");
        }

        // CLR Properties

        for (var k = 0; k < typeDef.Properties.Length; k++)
        {
            var p = typeDef.Properties[k];
            var t = GetPropertyType(p.Type);
            var propertyName = ReplaceDash(p: p.Name);
            var hidesInheritedProperty = HidesInheritedProperty(typeDef, p);

            ToClrProperty(sb, t, propertyName, hidesInheritedProperty);

            if (k < typeDef.Properties.Length - 1)
            {
                sb.AppendLine(value: "");
            }
        }

        // End Class

        sb.AppendLine(
            value: $$"""
                     }
                     """);

        WriteFile(typeDef, Settings.BasePath, ".Properties.g.cs", sb);
    }

    private void GenerateProperties()
    {
        for (var i = 0; i < Settings.TypeDefs.Length; i++)
        {
            var typeDef = Settings.TypeDefs[i];

            GenerateProperties(typeDef);
        }
    }

    private void GenerateWriters(TypeDef typeDef)
    {
        var sb = new StringBuilder();

        var ns = Settings.Namespace;
        var isAbstract = typeDef.IsAbstract;
        var className = HandleReserved(ReplaceDash(p: typeDef.TargetTpe));

        // Begin Class

        sb.AppendLine(
            value: $$"""
                     // <auto-generated />
                     #nullable enable

                     namespace {{ns}};

                     public{{(isAbstract ? " abstract" : "")}} partial class {{className}}
                     {
                     """);

        // WriteAttributes

        // TODO: hack
        if (typeDef.TargetTpe == Settings.BaseWriterType)
        {
            sb.AppendLine(
                value: $$"""
                             protected virtual void WriteAttributes(TextWriter writer, element parent)
                             {
                         """);
        }
        else
        {
            sb.AppendLine(
                value: $$"""
                             protected override void WriteAttributes(TextWriter writer, element parent)
                             {
                                 base.WriteAttributes(writer, parent);
                         """);

            if (typeDef.Properties.Length > 0)
            {
                sb.AppendLine(
                    value: $$"""

                             """);
            }
        }

        for (var k = 0; k < typeDef.Properties.Length; k++)
        {
            var p = typeDef.Properties[k];
            var propertyName = HandleReserved(ReplaceDash(p: p.Name));
            var pns = p.Name == "href" ? "xlink:" : p.Name == "sp" ? "xml:" : "";
            var attributeName = p.Name == "sp" ? "space" : p.Name;

            ToSetProperty(sb, propertyName, pns, attributeName);

            if (k < typeDef.Properties.Length - 1)
            {
                sb.AppendLine(value: "");
            }
        }

        AppendAdditionalWriterAttributes(sb, typeDef);

        sb.AppendLine(
            value: $$"""
                         }
                     """);

        // End Class

        sb.AppendLine(
            value: $$"""
                     }
                     """);

        WriteFile(typeDef, Settings.BasePath, ".Writer.g.cs", sb);
    }

    private void GenerateWriters()
    {
        for (var i = 0; i < Settings.TypeDefs.Length; i++)
        {
            var typeDef = Settings.TypeDefs[i];

            GenerateWriters(typeDef);
        }
    }

    private IEnumerable<string> GetBridgeableEnumTypes()
    {
        return Settings.TypeDefs
            .SelectMany(static typeDef => typeDef.Properties)
            .Select(static property => property.Type)
            .Where(IsEnumBackedType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static type => type, StringComparer.Ordinal);
    }

    private void GenerateEnumSupport()
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            """
            // <auto-generated />
            #nullable enable

            namespace SvgML;

            public interface ISvgEnumBridge
            {
                object RawValue { get; }
            }

            internal static class SvgEnumBridge
            {
                public static string ToSvgString(global::System.Enum value)
                {
                    var name = value.ToString();
                    if (string.IsNullOrEmpty(name))
                    {
                        return string.Empty;
                    }

                    var typeName = value.GetType().FullName;
                    return typeName switch
                    {
                        "Svg.SvgFontWeight" => FormatFontWeight(name),
                        "Svg.XmlSpaceHandling" or "Svg.SvgFillRule" or "Svg.SvgClipRule" => name.ToLowerInvariant(),
                        "Svg.SvgDominantBaseline" or "Svg.SvgFontVariant" or "Svg.SvgTextDecoration" or "Svg.SvgFontStretch" or "Svg.SvgVectorEffect" or "Svg.SvgTransformBox" or "Svg.SvgMixBlendMode" or "Svg.FilterEffects.SvgBlendMode" => ToKebabCase(name),
                        "Svg.SvgPaintOrder" => FormatPaintOrder(name),
                        "Svg.SvgWhiteSpace" => FormatWhiteSpace(name),
                        "Svg.FilterEffects.SvgChannelSelector" => name,
                        _ when typeName is not null && typeName.StartsWith("SvgML.", global::System.StringComparison.Ordinal) => name.Replace("_", "-", global::System.StringComparison.Ordinal),
                        _ => ToCamelCase(name),
                    };
                }

                public static TEnum Parse<TEnum>(string value)
                    where TEnum : struct, global::System.Enum
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return default;
                    }

                    if (TryParseSingle<TEnum>(value, out var parsed))
                    {
                        return parsed;
                    }

                    var parts = value.Split([' ', ','], global::System.StringSplitOptions.RemoveEmptyEntries | global::System.StringSplitOptions.TrimEntries);
                    if (parts.Length > 1)
                    {
                        ulong combined = 0;
                        for (var i = 0; i < parts.Length; i++)
                        {
                            if (!TryParseSingle<TEnum>(parts[i], out var part))
                            {
                                throw new global::System.FormatException($"Unable to convert '{value}' to {typeof(TEnum).FullName}.");
                            }

                            combined |= global::System.Convert.ToUInt64(part, global::System.Globalization.CultureInfo.InvariantCulture);
                        }

                        return (TEnum)global::System.Enum.ToObject(typeof(TEnum), combined);
                    }

                    throw new global::System.FormatException($"Unable to convert '{value}' to {typeof(TEnum).FullName}.");
                }

                private static bool TryParseSingle<TEnum>(string value, out TEnum parsed)
                    where TEnum : struct, global::System.Enum
                {
                    if (typeof(TEnum).FullName == "Svg.SvgFontWeight" && IsFontWeightLiteral(value))
                    {
                        value = "W" + value;
                    }

                    if (global::System.Enum.TryParse(value, ignoreCase: true, out parsed))
                    {
                        return true;
                    }

                    var normalized = NormalizeEnumToken(value);
                    var names = global::System.Enum.GetNames<TEnum>();
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (string.Equals(NormalizeEnumToken(names[i]), normalized, global::System.StringComparison.OrdinalIgnoreCase)
                            && global::System.Enum.TryParse(names[i], out parsed))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                private static string FormatFontWeight(string name)
                {
                    return name.Length == 4 && name[0] == 'W' && IsFontWeightLiteral(name.Substring(1))
                        ? name.Substring(1)
                        : ToCamelCase(name);
                }

                private static string FormatPaintOrder(string name)
                {
                    return name switch
                    {
                        "FillStrokeMarkers" => "fill stroke markers",
                        "FillMarkersStroke" => "fill markers stroke",
                        "StrokeFillMarkers" => "stroke fill markers",
                        "StrokeMarkersFill" => "stroke markers fill",
                        "MarkersFillStroke" => "markers fill stroke",
                        "MarkersStrokeFill" => "markers stroke fill",
                        _ => ToCamelCase(name)
                    };
                }

                private static string FormatWhiteSpace(string name)
                {
                    return name == "NoWrap" ? "nowrap" : ToKebabCase(name);
                }

                private static bool IsFontWeightLiteral(string value)
                {
                    return value.Length == 3
                        && value[0] >= '1' && value[0] <= '9'
                        && value[1] == '0'
                        && value[2] == '0';
                }

                private static string NormalizeEnumToken(string value)
                {
                    return value.Replace("-", string.Empty, global::System.StringComparison.Ordinal)
                        .Replace("_", string.Empty, global::System.StringComparison.Ordinal)
                        .Replace(" ", string.Empty, global::System.StringComparison.Ordinal);
                }

                private static string ToCamelCase(string value)
                {
                    return value.Length == 0
                        ? value
                        : char.ToLowerInvariant(value[0]) + value.Substring(1);
                }

                private static string ToKebabCase(string value)
                {
                    var builder = new global::System.Text.StringBuilder(value.Length + 4);
                    for (var i = 0; i < value.Length; i++)
                    {
                        var c = value[i];
                        if (c == ',')
                        {
                            continue;
                        }

                        if (char.IsWhiteSpace(c))
                        {
                            if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                            {
                                builder.Append(' ');
                            }

                            continue;
                        }

                        if (char.IsUpper(c))
                        {
                            if (i > 0 && value[i - 1] != ' ' && value[i - 1] != ',' && !char.IsUpper(value[i - 1]))
                            {
                                builder.Append('-');
                            }

                            builder.Append(char.ToLowerInvariant(c));
                            continue;
                        }

                        builder.Append(c == '_' ? '-' : c);
                    }

                    return builder.ToString();
                }
            }
            """);

        if (GetBridgeableEnumTypes().Any() && Settings.TypeDefs.Any())
        {
            foreach (var type in GetBridgeableEnumTypes())
            {
                if (!UsesEnumBridge(type))
                {
                    continue;
                }

                var bridgeType = GetEnumBridgeTypeName(type);
                var underlyingType = GetRawClrType(type);

                sb.AppendLine();
                sb.AppendLine($"""[global::System.ComponentModel.TypeConverter(typeof({bridgeType}Converter))]""");
                if (UsesCreateFromStringBridge())
                {
                    sb.AppendLine("""[global::Windows.Foundation.Metadata.CreateFromString(MethodName = nameof(Parse))]""");
                }

                sb.AppendLine(
                    $$"""
                    public readonly partial struct {{bridgeType}} : global::System.IEquatable<{{bridgeType}}>, ISvgEnumBridge
                    {
                        private readonly {{underlyingType}} _value;

                        public {{bridgeType}}({{underlyingType}} value)
                        {
                            _value = value;
                        }

                        public {{underlyingType}} Value => _value;

                        object ISvgEnumBridge.RawValue => _value;

                        public static {{bridgeType}} Parse(string value)
                        {
                            return new {{bridgeType}}(SvgEnumBridge.Parse<{{underlyingType}}>(value));
                        }

                        public override string ToString()
                        {
                            return SvgEnumBridge.ToSvgString(_value);
                        }

                        public bool Equals({{bridgeType}} other)
                        {
                            return _value.Equals(other._value);
                        }

                        public override bool Equals(object? obj)
                        {
                            return obj is {{bridgeType}} other && Equals(other);
                        }

                        public override int GetHashCode()
                        {
                            return _value.GetHashCode();
                        }

                        public static implicit operator {{bridgeType}}({{underlyingType}} value)
                        {
                            return new {{bridgeType}}(value);
                        }

                        public static implicit operator {{underlyingType}}({{bridgeType}} value)
                        {
                            return value._value;
                        }

                        public static bool operator ==({{bridgeType}} left, {{bridgeType}} right)
                        {
                            return left.Equals(right);
                        }

                        public static bool operator !=({{bridgeType}} left, {{bridgeType}} right)
                        {
                            return !left.Equals(right);
                        }
                    }

                    public sealed class {{bridgeType}}Converter : global::System.ComponentModel.TypeConverter
                    {
                        public override bool CanConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Type sourceType)
                        {
                            return sourceType == typeof(string)
                                || sourceType == typeof({{underlyingType}})
                                || base.CanConvertFrom(context, sourceType);
                        }

                        public override object? ConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object value)
                        {
                            return value switch
                            {
                                string svgValue => {{bridgeType}}.Parse(svgValue),
                                {{underlyingType}} enumValue => new {{bridgeType}}(enumValue),
                                _ => base.ConvertFrom(context, culture, value)
                            };
                        }

                        public override bool CanConvertTo(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Type? destinationType)
                        {
                            return destinationType == typeof(string)
                                || destinationType == typeof({{underlyingType}})
                                || base.CanConvertTo(context, destinationType);
                        }

                        public override object? ConvertTo(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object? value, global::System.Type destinationType)
                        {
                            if (value is {{bridgeType}} bridgedValue)
                            {
                                if (destinationType == typeof(string))
                                {
                                    return bridgedValue.ToString();
                                }

                                if (destinationType == typeof({{underlyingType}}))
                                {
                                    return bridgedValue.Value;
                                }
                            }

                            return base.ConvertTo(context, culture, value, destinationType);
                        }
                    }
                    """);
            }
        }

        WriteFile("SvgEnumSupport.g.cs", sb);
    }

    private void WriteFile(TypeDef typeDef, string basePath, string ext, StringBuilder sb)
    {
        var outputPath = Path.Combine(basePath, typeDef.FilePath);

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var fileName = typeDef.TargetTpe + ext;
        var path = Path.Combine(outputPath, fileName);

        File.WriteAllText(path, sb.ToString());
    }

    private void WriteFile(string fileName, StringBuilder sb)
    {
        var path = Path.Combine(Settings.BasePath, fileName);
        File.WriteAllText(path, sb.ToString());
    }
}
