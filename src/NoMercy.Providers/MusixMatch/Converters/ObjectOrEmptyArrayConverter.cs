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

namespace NoMercy.Providers.MusixMatch.Converters;

/// <summary>
/// Musixmatch returns an empty JSON array (<c>[]</c>) instead of an object for a
/// macro body that carries no data (e.g. a track with no subtitles). Deserializing
/// that array onto an object model throws and fails the whole response. This reads
/// the empty array as an empty <typeparamref name="T"/> so the rest of the payload
/// still deserializes; downstream callers already treat an empty body as "no data".
/// </summary>
public class ObjectOrEmptyArrayConverter<T> : JsonConverter<T?>
    where T : class, new()
{
    public override bool CanWrite => false;

    public override T? ReadJson(
        JsonReader reader,
        Type objectType,
        T? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return null;
            case JsonToken.StartArray:
                JArray.Load(reader: reader);
                return new();
            default:
                JObject json = JObject.Load(reader: reader);
                return json.ToObject<T>(jsonSerializer: serializer);
        }
    }

    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer) =>
        throw new NotSupportedException();
}
