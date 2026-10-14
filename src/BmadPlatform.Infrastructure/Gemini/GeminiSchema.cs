using System.Text.Json;
using System.Text.Json.Nodes;

namespace BmadPlatform.Infrastructure.Gemini;

/// <summary>
/// Minimal JSON-schema check for the subset the callers use: type, required, enum, properties and items. Unknown
/// properties are allowed here (the drafters reject them themselves).
/// </summary>
internal static class GeminiSchema
{
    public static bool Satisfies(JsonNode? node, JsonObject schema)
    {
        if (node is null)
        {
            return false;
        }

        var type = schema["type"]?.GetValue<string>();

        switch (type)
        {
            case "object":
                if (node is not JsonObject obj)
                {
                    return false;
                }

                foreach (var required in schema["required"]?.AsArray() ?? [])
                {
                    if (obj[required!.GetValue<string>()] is null)
                    {
                        return false;
                    }
                }

                foreach (var (name, propertySchema) in schema["properties"]?.AsObject() ?? [])
                {
                    if (obj[name] is { } value && !Satisfies(value, propertySchema!.AsObject()))
                    {
                        return false;
                    }
                }

                return true;

            case "array":
                if (node is not JsonArray array)
                {
                    return false;
                }

                return schema["items"] is not JsonObject items || array.All(item => Satisfies(item, items));

            case "string":
                if (node is not JsonValue text || text.GetValueKind() != JsonValueKind.String)
                {
                    return false;
                }

                return schema["enum"] is not JsonArray allowed
                       || allowed.Any(option => option?.GetValue<string>() == text.GetValue<string>());

            case "integer":
            case "number":
                return node is JsonValue number && number.GetValueKind() == JsonValueKind.Number;

            case "boolean":
                return node is JsonValue flag && flag.GetValueKind() is JsonValueKind.True or JsonValueKind.False;

            default:
                return false;
        }
    }
}
