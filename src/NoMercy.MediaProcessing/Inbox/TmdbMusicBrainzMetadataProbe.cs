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

using NoMercy.Database.Models.Libraries;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Inbox;

public sealed class TmdbMusicBrainzMetadataProbe : IInboxMetadataProbe
{
    public async Task<CandidateMatch[]> SearchMoviesAsync(
        string title,
        int? year,
        CancellationToken ct
    )
    {
        using TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbMovie>? response = await client.Movie(title, year?.ToString());

        if (response?.Results is null || response.Results.Count == 0)
            return [];

        return response
            .Results.Take(5)
            .Select(m => new CandidateMatch
            {
                Provider = "tmdb",
                ExternalId = m.Id.ToString(),
                Title = m.Title,
                Year = m.ReleaseDate?.Year,
                PosterPath = m.PosterPath,
                Score = NormalizePopularity(m.Popularity, m.VoteAverage),
            })
            .ToArray();
    }

    public async Task<CandidateMatch[]> SearchTvAsync(string title, int? year, CancellationToken ct)
    {
        using TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbTvShow>? response = await client.TvShow(title, year?.ToString());

        if (response?.Results is null || response.Results.Count == 0)
            return [];

        return response
            .Results.Take(5)
            .Select(show => new CandidateMatch
            {
                Provider = "tmdb",
                ExternalId = show.Id.ToString(),
                Title = show.Name,
                Year = show.FirstAirDate?.Year,
                PosterPath = show.PosterPath,
                Score = NormalizePopularity(show.Popularity, show.VoteAverage),
            })
            .ToArray();
    }

    public async Task<CandidateMatch?> LookupMusicReleaseAsync(Guid releaseId, CancellationToken ct)
    {
        using MusicBrainzReleaseClient client = new();
        MusicBrainzReleaseAppends? release = await client.WithAllAppends(releaseId);

        if (release is null)
            return null;

        string artist = release.ArtistCredit.Select(ac => ac.Name).FirstOrDefault() ?? string.Empty;

        int? year = release
            .ReleaseEvents?.Select(e => e.DateTime?.Year)
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .FirstOrDefault();

        return new CandidateMatch
        {
            Provider = "musicbrainz",
            ExternalId = releaseId.ToString(),
            Title = string.IsNullOrEmpty(artist) ? release.Title : $"{artist} – {release.Title}",
            Year = year == 0 ? null : year,
            PosterPath = null,
            Score = 1.0,
        };
    }

    private static double NormalizePopularity(double popularity, double voteAverage)
    {
        double popScore = Math.Min(popularity / 100.0, 1.0);
        double voteScore = voteAverage / 10.0;
        return Math.Round((popScore * 0.4) + (voteScore * 0.6), 4);
    }
}
