using Newtonsoft.Json;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class LongConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(long) || objectType == typeof(long?);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is long longValue)
            writer.WriteValue(longValue);
        else
            writer.WriteNull();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        if (reader.Value is null) return null;

        return reader.TokenType switch
        {
            JsonToken.Null => null,
            JsonToken.Integer => Convert.ToInt64(reader.Value),
            JsonToken.String when long.TryParse((string)reader.Value, out long result) => result,
            _ => null
        };
    }
}