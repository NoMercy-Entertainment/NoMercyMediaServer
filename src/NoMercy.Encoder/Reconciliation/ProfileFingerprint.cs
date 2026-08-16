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
    /// <summary>
    /// Names what the hash below covers, and is written into the fingerprint so
    /// a stored one says what it measured. A value carrying a different name (or
    /// none) measured something else and cannot be compared against this — that
    /// is "unreadable", not "different", and <c>EncodeReconciler</c> treats it
    /// exactly as it treats no fingerprint at all: as the same profile. Without
    /// that, the first server upgrade to touch the hash would re-encode an
    /// operator's entire library.
    /// </summary>
    private const string CoverageTag = "streams";

    /// <summary>
    /// Profile fields that shape a sidecar artifact rather than an encoded
    /// stream. They are deliberately outside the fingerprint: whether a sprite
    /// sheet is current is answered by looking at the sheet on disk, and a
    /// preview-tile size has no business deciding whether the video has to be
    /// encoded again.
    /// </summary>
    private static readonly string[] DerivativeGeometryFields =
    [
        CamelCase(nameof(HlsDerivatives.SpriteVttThumbnailWidth)),
        CamelCase(nameof(HlsDerivatives.SpriteVttIntervalSeconds)),
        CamelCase(nameof(HlsDerivatives.SpriteVttColumns)),
        CamelCase(nameof(HlsDerivatives.SpriteVttRows)),
    ];

    private static readonly JsonSerializer CanonicalSerializer = JsonSerializer.Create(
        new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Converters = { new UlidNewtonsoftConverter(), new StringEnumConverter() },
        }
    );

    public static string Compute(EncodingProfile profile)
    {
        JToken raw = JToken.FromObject(profile, CanonicalSerializer);
        StripDerivativeGeometry(raw);
        JToken canonical = Canonicalize(raw);
        string json = canonical.ToString(Formatting.None);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"{CoverageTag}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    /// <summary>
    /// Whether a stored fingerprint measured the same thing this one does, and
    /// can therefore be compared against it — see <see cref="CoverageTag"/>.
    /// </summary>
    public static bool IsComparable(string? storedFingerprint) =>
        storedFingerprint?.StartsWith($"{CoverageTag}:", StringComparison.Ordinal) == true;

    private static void StripDerivativeGeometry(JToken raw)
    {
        if (
            raw is not JObject root
            || root[CamelCase(nameof(EncodingProfile.HlsDerivatives))] is not JObject derivatives
        )
            return;

        foreach (string field in DerivativeGeometryFields)
            derivatives.Remove(field);
    }

    private static string CamelCase(string propertyName) =>
        char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    private static JToken Canonicalize(JToken token)
    {
        if (token is JObject obj)
        {
            JObject sorted = new();
            foreach (
                JProperty property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
            )
                sorted.Add(property.Name, Canonicalize(property.Value));
            return sorted;
        }

        if (token is JArray array)
        {
            JArray sortedArray = [];
            foreach (JToken item in array)
                sortedArray.Add(Canonicalize(item));
            return sortedArray;
        }

        return token;
    }
}
