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

using MovieFileLibrary;
using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// What the server read out of a filename, in the payload's own naming.
///
/// MovieFile comes from MovieFileLibrary and carries no serialization attributes,
/// so serializing it directly puts CLR property names into a payload that is
/// snake_case everywhere else. Projecting through this type keeps the wire
/// consistent for every dashboard client.
/// </summary>
public record ParsedFileDto
{
    [JsonProperty("title")]
    public string? Title { get; init; }

    [JsonProperty("year")]
    public string? Year { get; init; }

    [JsonProperty("season")]
    public int? Season { get; init; }

    [JsonProperty("episode")]
    public int? Episode { get; init; }

    [JsonProperty("is_series")]
    public bool IsSeries { get; init; }

    [JsonProperty("imdb_id")]
    public string? ImdbId { get; init; }

    public static ParsedFileDto? From(MovieFile? parsed) =>
        parsed is null
            ? null
            : new()
            {
                Title = parsed.Title,
                Year = parsed.Year,
                Season = parsed.Season,
                Episode = parsed.Episode,
                IsSeries = parsed.IsSeries,
                ImdbId = parsed.ImdbId,
            };
}
