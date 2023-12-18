using System;
using System.IO;
using Newtonsoft.Json;

namespace Svg.Skia.Converter;

internal class DirectoryInfoJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(DirectoryInfo);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value is string s)
        {
            return new DirectoryInfo(s);
        }
        throw new ArgumentOutOfRangeException(nameof(reader));
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (!(value is DirectoryInfo directoryInfo))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        writer.WriteValue(directoryInfo.FullName);
    }
}
