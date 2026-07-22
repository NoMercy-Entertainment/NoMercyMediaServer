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

using System.Globalization;
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
            if (double.IsInfinity(d: doubleValue) || double.IsNaN(d: doubleValue))
                writer.WriteNull();
            else
                writer.WriteValue(value: doubleValue);
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        return reader.TokenType switch
        {
            JsonToken.Null => null,
            JsonToken.Float or JsonToken.Integer => Convert.ToDouble(value: reader.Value),
            // JSON always uses '.' as the decimal separator regardless of host
            // locale — parsing with the current culture silently mangles the
            // value on any machine where '.' isn't the decimal separator (e.g.
            // "7.5" reads as 75 under a culture that treats '.' as a group
            // separator).
            JsonToken.String
                when double.TryParse(
                    s: (string)reader.Value!,
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out double result
                ) =>
                result is not double.NaN
                && result is not double.PositiveInfinity
                && result is not double.NegativeInfinity
                    ? result
                    : throw new JsonSerializationException(message: $"Invalid double value: {reader.Value}"),
            _ => throw new JsonSerializationException(
                message: $"Unexpected token {reader.TokenType} when parsing double."
            ),
        };
    }
}
