using Newtonsoft.Json;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class DoubleConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(double) || objectType == typeof(double?);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is double doubleValue)
        {
            if (double.IsInfinity(doubleValue) || double.IsNaN(doubleValue))
                writer.WriteNull();
            else
                writer.WriteValue(doubleValue);
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        return reader.TokenType switch
        {
            JsonToken.Null => null,
            JsonToken.Float or JsonToken.Integer => Convert.ToDouble(reader.Value),
            JsonToken.String when double.TryParse((string)reader.Value!, out double result) => result is not double.NaN && result is not double.PositiveInfinity && result is not double.NegativeInfinity
                ? result / 1000000
                : throw new JsonSerializationException($"Invalid double value: {reader.Value}"),
            _ => throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing double.")
        };
    }
}