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

using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// Computes a deterministic fingerprint of a <see cref="EncodingProfile"/> so
/// the reconciler can tell "same preset id, but edited in place" apart from
/// "genuinely unchanged". A preset id alone is not a safe anchor — editing an
/// existing preset's settings keeps its id, so re-dispatching against the
/// edited preset must still be recognised as a profile change.
///
/// The fingerprint is a SHA-256 hash of the profile's canonical JSON: every
/// object's properties are sorted alphabetically at every nesting level
/// before hashing, so the result depends only on the profile's actual field
/// values — never on property declaration order, dictionary insertion order,
/// or which .NET version wrote the record.
/// </summary>
public static class ProfileFingerprint
{
    private static readonly JsonSerializer CanonicalSerializer = JsonSerializer.Create(
        settings: new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Converters = { new UlidNewtonsoftConverter(), new StringEnumConverter() },
        }
    );

    public static string Compute(EncodingProfile profile)
    {
        JToken raw = JToken.FromObject(o: profile, jsonSerializer: CanonicalSerializer);
        JToken canonical = Canonicalize(token: raw);
        string json = canonical.ToString(formatting: Formatting.None);
        byte[] hash = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: json));
        return Convert.ToHexString(inArray: hash).ToLowerInvariant();
    }

    private static JToken Canonicalize(JToken token)
    {
        if (token is JObject obj)
        {
            JObject sorted = new();
            foreach (
                JProperty property in obj.Properties().OrderBy(keySelector: p => p.Name, comparer: StringComparer.Ordinal)
            )
                sorted.Add(propertyName: property.Name, value: Canonicalize(token: property.Value));
            return sorted;
        }

        if (token is JArray array)
        {
            JArray sortedArray = new();
            foreach (JToken item in array)
                sortedArray.Add(item: Canonicalize(token: item));
            return sortedArray;
        }

        return token;
    }
}
