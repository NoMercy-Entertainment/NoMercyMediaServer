using Newtonsoft.Json;

namespace NoMercy.Providers.Helpers;
public class ParseStringConverter : JsonConverter
{
    public static readonly ParseStringConverter Singleton = new();

    public override bool CanConvert(Type t)
    {
        return t == typeof(long) || t == typeof(long?);
    }

    public override object? ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        string? value = serializer.Deserialize<string>(reader);

        if (CanConvert(t) && long.TryParse(value, out long l)) return l;

        throw new("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        long value = (long)untypedValue;
        serializer.Serialize(writer, value.ToString());
    }
}