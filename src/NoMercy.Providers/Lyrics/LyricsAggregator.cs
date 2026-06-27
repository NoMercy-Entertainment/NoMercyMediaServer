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
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Lrclib.Client;
using NoMercy.Providers.Lrclib.Models;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Client;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Providers.Lyrics;

public class LyricsAggregator : ILyricsAggregator
{
    /// <summary>
    /// Resolves lyrics for a track, preferring the free Lrclib service and
    /// falling back to Musixmatch. Every candidate is validated against the
    /// track's title, artist and (for synced lyrics) release length, so a
    /// mismatched song or a wrong-release timing is rejected instead of stored.
    /// </summary>
    public async Task<LyricLine[]?> SearchLyrics(Track track)
    {
        // `using` so an exception mid-search doesn't leak the HttpClient wrappers.
        using MusixmatchClient musixmatchClient = new();
        using LrclibClient lrclibClient = new();

        string[] artists = track
            .ArtistTrack.Select(artistTrack => artistTrack.Artist.Name)
            .ToArray();
        string? albumName = track.AlbumTrack.FirstOrDefault()?.Album.Name;
        int parsedDuration = track.Duration.ToSeconds();
        int? durationSeconds = parsedDuration > 0 ? parsedDuration : null;

        LyricQuery query = new(track.Name, artists, albumName, durationSeconds);

        // Free service first, but timed (synced) lyrics always win over untimed:
        // if Lrclib only has plain lyrics, still try Musixmatch for a synced match
        // and prefer it. Fall back to whatever plain result exists otherwise.
        LyricCandidate? lrclib = await FromLrclib(lrclibClient, query, artists);
        if (lrclib is { HasSyncedLyrics: true })
            return lrclib.Lines;

        LyricCandidate? musixmatch = await FromMusixmatch(musixmatchClient, query);
        if (musixmatch is { HasSyncedLyrics: true })
            return musixmatch.Lines;

        return (lrclib ?? musixmatch)?.Lines;
    }

    private static async Task<LyricCandidate?> FromLrclib(
        LrclibClient client,
        LyricQuery query,
        string[] artists
    )
    {
        List<LyricCandidate> candidates = [];

        LrclibSongResult? exact = await client.Get(
            artists,
            query.Title,
            query.Album,
            query.DurationSeconds
        );
        if (exact is not null && LrclibClient.ToCandidate(exact) is { } exactCandidate)
            candidates.Add(exactCandidate);

        LrclibSongResult[]? results = await client.Search(artists, query.Title);
        if (results is not null)
            foreach (LrclibSongResult result in results)
                if (LrclibClient.ToCandidate(result) is { } candidate)
                    candidates.Add(candidate);

        return LyricMatcher.PickBest(query, candidates);
    }

    private static async Task<LyricCandidate?> FromMusixmatch(
        MusixmatchClient client,
        LyricQuery query
    )
    {
        string artistNames = string.Join(",", query.Artists);
        string duration =
            query.DurationSeconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        // Tighten first (album + duration), then relax. Validation gates every
        // result, so relaxing the query never lets a wrong song through.
        LyricCandidate? candidate = Validate(
            query,
            await client.SongSearch(
                new()
                {
                    Album = query.Album,
                    Artist = artistNames,
                    Title = query.Title,
                    Duration = duration,
                    Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc,
                }
            )
        );
        if (candidate is not null)
            return candidate;

        return Validate(
            query,
            await client.SongSearch(
                new()
                {
                    Artist = artistNames,
                    Title = query.Title,
                    Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc,
                }
            )
        );
    }

    private static LyricCandidate? Validate(LyricQuery query, MusixMatchSubtitleGet? response)
    {
        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);
        if (candidate is null)
            return null;
        return LyricMatcher.Score(query, candidate) >= 0 ? candidate : null;
    }
}
