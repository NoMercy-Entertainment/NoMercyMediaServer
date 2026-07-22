// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public class GuidKeyDictionaryConverter<TValue> : JsonConverter
    where TValue : class
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Dictionary<Guid, TValue>);
    }

    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        Dictionary<Guid, TValue?> dictionary = new();
        if (reader.TokenType == JsonToken.Null)
            return dictionary;
        JObject jObject;
        try
        {
            jObject = JObject.Load(reader: reader);
        }
        catch (JsonReaderException exception)
        {
            Logger.Error(message: exception, level: LogEventLevel.Error);
            return dictionary;
        }

        foreach (JProperty property in jObject.Properties())
            if (Guid.TryParse(input: (ReadOnlySpan<char>)property.Name, result: out Guid key))
            {
                TValue? value = property.Value.ToObject<TValue>(jsonSerializer: serializer);
                dictionary[key: key] = value;
            }
            else
            {
                // Handle invalid GUIDs here, e.g., set to Guid.Empty or skip
                TValue? value = property.Value.ToObject<TValue>(jsonSerializer: serializer);
                dictionary[key: Guid.Empty] = value;
            }

        return dictionary;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        Dictionary<Guid, TValue>? dictionary = value as Dictionary<Guid, TValue>;
        JObject jObject = new();

        if (dictionary != null)
            foreach (KeyValuePair<Guid, TValue> kvp in dictionary)
                jObject.Add(propertyName: kvp.Key.ToString(), value: JToken.FromObject(o: kvp.Value, jsonSerializer: serializer));

        jObject.WriteTo(writer: writer);
    }
}
