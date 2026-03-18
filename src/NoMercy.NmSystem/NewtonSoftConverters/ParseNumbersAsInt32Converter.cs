using Newtonsoft.Json;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class ParseNumbersAsInt32Converter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(long) || objectType == typeof(object);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is int intValue)
        {
            writer.WriteValue(intValue);
        }
        else
        {
            // Fallback to default serialization for other types
            // serializer.Serialize(writer, value);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        return reader.Value is long
            ? Convert.ToInt64(reader.Value ?? 0)
            : reader.Value;
    }
}