using System.Text.Json;

namespace Svg.Editor.Mcp;

/// <summary>
/// Validates the deliberately bounded JSON Schema vocabulary used by the editor catalog.
/// The catalog is application code, not client-supplied executable schema.
/// Domain rules (locks, revision checks, geometry) remain in the editor target.
/// </summary>
internal static class EditorMcpSchemaValidator
{
    public static void Validate(JsonElement schema, JsonElement value)
    {
        ValidateValue(schema, value, "arguments", 0);
    }

    private static void ValidateValue(JsonElement schema, JsonElement value, string path, int depth)
    {
        if (depth > 64)
        {
            throw new EditorMcpException($"{path} exceeds the supported nesting depth.");
        }

        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            throw new EditorMcpException($"{path} is not allowed.");
        }

        if (schema.TryGetProperty("anyOf", out var anyOf))
        {
            ValidateAlternatives(anyOf, value, path, depth, false);
        }

        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            ValidateAlternatives(oneOf, value, path, depth, true);
        }

        if (schema.TryGetProperty("type", out var type) && !MatchesType(type, value))
        {
            throw new EditorMcpException($"{path} must have type {type.GetRawText()}.");
        }

        if (schema.TryGetProperty("enum", out var enumeration)
            && !enumeration.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
        {
            throw new EditorMcpException($"{path} must be one of {enumeration.GetRawText()}.");
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(schema, value, path, depth);
                break;
            case JsonValueKind.Array:
                var length = value.GetArrayLength();
                CheckBound(schema, "minItems", length, path, false);
                CheckBound(schema, "maxItems", length, path, true);
                if (schema.TryGetProperty("items", out var items))
                {
                    var index = 0;
                    foreach (var item in value.EnumerateArray())
                    {
                        ValidateValue(items, item, $"{path}[{index++}]", depth + 1);
                    }
                }

                break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                CheckBound(schema, "minLength", text.EnumerateRunes().Count(), path, false);
                CheckBound(schema, "maxLength", text.EnumerateRunes().Count(), path, true);
                break;
            case JsonValueKind.Number:
                if (!value.TryGetDouble(out var number) || !double.IsFinite(number))
                {
                    throw new EditorMcpException($"{path} must be a finite number.");
                }

                CheckBound(schema, "minimum", number, path, false);
                CheckBound(schema, "maximum", number, path, true);
                if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimum) && number <= exclusiveMinimum.GetDouble())
                {
                    throw new EditorMcpException($"{path} must be greater than {exclusiveMinimum}.");
                }

                if (schema.TryGetProperty("exclusiveMaximum", out var exclusiveMaximum) && number >= exclusiveMaximum.GetDouble())
                {
                    throw new EditorMcpException($"{path} must be less than {exclusiveMaximum}.");
                }

                break;
        }
    }

    private static void ValidateObject(JsonElement schema, JsonElement value, string path, int depth)
    {
        var propertyCount = value.EnumerateObject().Count();
        CheckBound(schema, "minProperties", propertyCount, path, false);
        CheckBound(schema, "maxProperties", propertyCount, path, true);
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray())
            {
                if (!value.TryGetProperty(name.GetString()!, out _))
                {
                    throw new EditorMcpException($"{path}.{name.GetString()} is required.");
                }
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);
        var hasAdditionalProperties = schema.TryGetProperty("additionalProperties", out var additionalProperties);
        foreach (var property in value.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                ValidateValue(propertySchema, property.Value, $"{path}.{property.Name}", depth + 1);
            }
            else if (hasAdditionalProperties)
            {
                ValidateValue(additionalProperties, property.Value, $"{path}.{property.Name}", depth + 1);
            }
        }
    }

    private static void ValidateAlternatives(JsonElement alternatives, JsonElement value, string path, int depth, bool exactlyOne)
    {
        var matches = 0;
        foreach (var alternative in alternatives.EnumerateArray())
        {
            try
            {
                ValidateValue(alternative, value, path, depth + 1);
                matches++;
            }
            catch (EditorMcpException)
            {
                // An alternative is allowed to reject the value.
            }
        }

        if (matches == 0 || (exactlyOne && matches != 1))
        {
            throw new EditorMcpException($"{path} does not match the required argument shape.");
        }
    }

    private static bool MatchesType(JsonElement type, JsonElement value)
    {
        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray().Any(item => MatchesType(item, value));
        }

        return type.GetString() switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) && Math.Truncate(number) == number,
            _ => false
        };
    }

    private static void CheckBound(JsonElement schema, string keyword, double value, string path, bool upper)
    {
        if (schema.TryGetProperty(keyword, out var bound) && (upper ? value > bound.GetDouble() : value < bound.GetDouble()))
        {
            throw new EditorMcpException($"{path} violates {keyword} {bound}.");
        }
    }
}
