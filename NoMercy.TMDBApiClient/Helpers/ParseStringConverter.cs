﻿using Newtonsoft.Json;

namespace NoMercy.TMDBApi.Helpers;

public class ParseJwtStringConverter : JsonConverter
{
    public static readonly ParseJwtStringConverter Singleton = new();

    public override bool CanConvert(Type t)
    {
        return t == typeof(string);
    }

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        var value = serializer.Deserialize<string>(reader);
        if (long.TryParse(value, out var l)) return l;
        throw new Exception("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (long)untypedValue;
        serializer.Serialize(writer, value.ToString());
    }
}

public class ParseStringConverter : JsonConverter
{
    public static readonly ParseStringConverter Singleton = new();

    public override bool CanConvert(Type t)
    {
        return t == typeof(long) || t == typeof(long?);
    }

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        var value = serializer.Deserialize<string>(reader);
        if (CanConvert(t) && long.TryParse(value, out var l)) return l;
        return null;
        throw new Exception("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (long)untypedValue;
        serializer.Serialize(writer, value.ToString());
    }
}