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

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackSearchParameters
{
    public static Dictionary<
        MusixMatchSortStrategy,
        KeyValuePair<string, string>
    > StrategyDecryptions = new()
    {
        [key: MusixMatchSortStrategy.TrackRatingAsc] = new(key: "s_track_rating", value: "asc"),
        [key: MusixMatchSortStrategy.TrackRatingDesc] = new(key: "s_track_rating", value: "desc"),
        [key: MusixMatchSortStrategy.ArtistRatingAsc] = new(key: "s_artist_rating", value: "asc"),
        [key: MusixMatchSortStrategy.ArtistRatingDesc] = new(key: "s_artist_rating", value: "desc"),
        [key: MusixMatchSortStrategy.ReleaseDateAsc] = new(key: "s_track_release_date", value: "asc"),
        [key: MusixMatchSortStrategy.ReleaseDateDesc] = new(key: "s_track_release_date", value: "desc"),
    };

    public string? Query { get; set; }
    public string? LyricsQuery { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string[]? Artists { get; set; }
    public string? Album { get; init; }
    public string? Duration { get; set; }
    public string? Language { get; set; }
    public bool? HasLyrics { get; set; }
    public bool? HasSubtitles { get; set; }
    public bool? HasRichSync { get; set; }
    public MusixMatchSortStrategy? Sort { get; init; } = MusixMatchSortStrategy.TrackRatingDesc;

    public enum MusixMatchSortStrategy
    {
        TrackRatingAsc,
        TrackRatingDesc,
        ArtistRatingAsc,
        ArtistRatingDesc,
        ReleaseDateAsc,
        ReleaseDateDesc,
    }
}
