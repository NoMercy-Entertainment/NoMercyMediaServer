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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzTrack
{
    private int _number;

    [JsonProperty(propertyName: "artist-credit")]
    public ReleaseArtistCredit[] ArtistCredit { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    /** Track length in milliseconds */
    [JsonProperty(propertyName: "length")]
    public int? Length { get; set; }

    public double Duration => (Length ?? 0) / 1000.0;

    // Bound as the raw JSON token (Newtonsoft renders a numeric token to its
    // string form when the target type is string) so a vinyl-style "A1" or
    // box-set "1-05" track number reaches the parser below. Number's own
    // setter used to run Convert.ToInt32 on an already-int value — a
    // conversion that can never throw — so the fallback for non-numeric
    // input was unreachable dead code.
    [JsonProperty(propertyName: "number")]
    private string? NumberToken
    {
        set => _number = ParseNumber(raw: value);
    }

    [JsonIgnore]
    public int Number => _number;

    /// <summary>
    /// Most releases send a plain integer. Vinyl/cassette media prefix a side
    /// letter (e.g. "A1") and box sets sometimes encode disc-track as "1-05"
    /// — both fall back to the trailing numeric segment. Unparseable input
    /// defaults to 0.
    /// </summary>
    internal static int ParseNumber(string? raw)
    {
        if (raw is not null && int.TryParse(s: raw, result: out int parsed))
            return parsed;

        return raw?.Replace(oldValue: "A", newValue: string.Empty).Split(separator: "-").LastOrDefault()?.ToInt() ?? 0;
    }

    [JsonProperty(propertyName: "position")]
    public int Position { get; set; }

    [JsonProperty(propertyName: "recording")]
    public TrackRecording Recording { get; set; } = new();

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "genres")]
    public MusicBrainzGenreDetails[]? Genres { get; set; }
}
