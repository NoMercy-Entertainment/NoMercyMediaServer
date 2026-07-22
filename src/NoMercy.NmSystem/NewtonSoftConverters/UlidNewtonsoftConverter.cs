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
            throw new JsonSerializationException(message: $"Expected string Ulid, got {reader.TokenType}");
        string? s = (string?)reader.Value;
        return string.IsNullOrEmpty(value: s) ? default : Ulid.Parse(base32: s);
    }

    public override void WriteJson(JsonWriter writer, Ulid value, JsonSerializer serializer)
    {
        writer.WriteValue(value: value.ToString());
    }
}
