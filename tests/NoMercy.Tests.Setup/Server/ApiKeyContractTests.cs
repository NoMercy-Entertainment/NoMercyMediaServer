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
using NoMercy.Setup.Dto;

namespace NoMercy.Tests.Setup.Server;

/// <summary>
/// Requirement: every key the provider API serves must land in a populated field on
/// <see cref="Keys"/>. A <c>JsonProperty</c> name that does not match what the API
/// actually sends leaves its field at <see cref="string.Empty"/>, and nothing fails —
/// the provider that needs it just starts sending requests with a blank credential and
/// gets rejected forever.
/// </summary>
/// <remarks>
/// The payload below is the field-name shape of a live <c>GET /v1/info</c> response
/// (values replaced with markers). It is written out by hand ON PURPOSE: serializing
/// <see cref="Keys"/> and reading it back — which the other key tests do — round-trips
/// through the same attribute on both sides, so it agrees with itself no matter which
/// name the attribute carries and can never catch a drift from the real contract.
///
/// Caught in the wild 2026-08-02: the DTO asked for <c>acoustic_id_key</c> and
/// <c>make_mkv_key</c> while the API sends <c>acoustic_id</c> and <c>makemkv_key</c>.
/// AcoustID lookups went out with <c>client=</c> and every one came back
/// <c>400 missing required parameter "client"</c>, so no untagged album could ever be
/// identified by fingerprint.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ApiKeyContractTests
{
    private const string LiveApiKeyPayload = """
        {
            "makemkv_key": "makemkv-value",
            "omdb_key": "omdb-value",
            "tadb_key": "tadb-value",
            "tmdb_key": "tmdb-value",
            "tmdb_token": "tmdb-token-value",
            "tvdb_key": "tvdb-value",
            "fanart_key": "fanart-value",
            "rotten_tomatoes": "rotten-value",
            "acoustic_id": "acoustid-value",
            "musixmatch_key": "musixmatch-value"
        }
        """;

    private static Keys DeserializeLivePayload() =>
        JsonConvert.DeserializeObject<Keys>(LiveApiKeyPayload)
        ?? throw new InvalidOperationException("payload did not deserialize");

    [Fact]
    public void EveryKeyTheApiServes_LandsOnAPopulatedField()
    {
        Keys keys = DeserializeLivePayload();

        Assert.Equal("makemkv-value", keys.MakeMkvKey);
        Assert.Equal("omdb-value", keys.OmdbKey);
        Assert.Equal("tadb-value", keys.TadbKey);
        Assert.Equal("tmdb-value", keys.TmdbKey);
        Assert.Equal("tmdb-token-value", keys.TmdbToken);
        Assert.Equal("tvdb-value", keys.TvdbKey);
        Assert.Equal("fanart-value", keys.FanArtKey);
        Assert.Equal("rotten-value", keys.RottenTomatoes);
        Assert.Equal("acoustid-value", keys.AcousticIdKey);
        Assert.Equal("musixmatch-value", keys.MusixmatchKey);
    }

    /// <summary>
    /// The failure this guards against is silent: an unmatched name leaves the field
    /// empty rather than throwing, so assert that NO field is left empty by the payload
    /// the API really sends. A key added to the DTO without a matching served name
    /// fails here too.
    /// </summary>
    [Fact]
    public void NoKeyIsLeftEmptyByTheLivePayload()
    {
        Keys keys = DeserializeLivePayload();

        List<string> empty =
        [
            .. typeof(Keys)
                .GetProperties()
                .Where(property => property.PropertyType == typeof(string))
                .Where(property => string.IsNullOrEmpty((string?)property.GetValue(keys)))
                .Select(property => property.Name),
        ];

        Assert.Empty(empty);
    }
}
