using Newtonsoft.Json;

namespace NoMercy.Providers.Helpers;
public class ParseJwtStringConverter : JsonConverter
{
    public static readonly ParseJwtStringConverter Singleton = new();

    public override bool CanConvert(Type t)
    {
        return t == typeof(string);
    }

    public override object? ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        string? value = serializer.Deserialize<string>(reader);

        if (long.TryParse(value, out long l)) return l;

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