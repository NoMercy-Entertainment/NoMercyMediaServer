using Newtonsoft.Json;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class UlidNewtonsoftConverter : JsonConverter<Ulid>
{
    public override Ulid ReadJson(
        JsonReader reader,
        Type objectType,
        Ulid existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
            return default;
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Expected string Ulid, got {reader.TokenType}");
        string? s = (string?)reader.Value;
        return string.IsNullOrEmpty(s) ? default : Ulid.Parse(s);
    }

    public override void WriteJson(JsonWriter writer, Ulid value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}
