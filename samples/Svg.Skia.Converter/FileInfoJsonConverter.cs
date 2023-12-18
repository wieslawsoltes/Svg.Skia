using System;
using System.IO;
using Newtonsoft.Json;

namespace Svg.Skia.Converter;

internal class FileInfoJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(FileInfo);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value is string s)
        {
            return new FileInfo(s);
        }
        throw new ArgumentOutOfRangeException(nameof(reader));
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (!(value is FileInfo fileInfo))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        writer.WriteValue(fileInfo.FullName);
    }
}
