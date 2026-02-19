using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class GuidKeyDictionaryConverter<TValue> : JsonConverter where TValue : class
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Dictionary<Guid, TValue>);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        Dictionary<Guid, TValue?> dictionary = new();
        if (reader.TokenType == JsonToken.Null)
            return dictionary;
        JObject jObject;
        try
        {
            jObject = JObject.Load(reader);
        }
        catch (JsonReaderException exception)
        {
            Logger.Error(exception, LogEventLevel.Error);
            return dictionary;
        }

        foreach (JProperty property in jObject.Properties())
            if (Guid.TryParse((ReadOnlySpan<char>)property.Name, out Guid key))
            {
                TValue? value = property.Value.ToObject<TValue>(serializer);
                dictionary[key] = value;
            }
            else
            {
                // Handle invalid GUIDs here, e.g., set to Guid.Empty or skip
                TValue? value = property.Value.ToObject<TValue>(serializer);
                dictionary[Guid.Empty] = value;
            }

        return dictionary;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        Dictionary<Guid, TValue>? dictionary = value as Dictionary<Guid, TValue>;
        JObject jObject = new();

        if (dictionary != null)
            foreach (KeyValuePair<Guid, TValue> kvp in dictionary)
                jObject.Add(kvp.Key.ToString(), JToken.FromObject(kvp.Value, serializer));

        jObject.WriteTo(writer);
    }
}