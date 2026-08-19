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

using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Providers.AniList;
using NoMercy.Providers.AniList.Models;
using NoMercy.Providers.Jikan;
using NoMercy.Providers.Jikan.Models;

namespace NoMercy.MediaProcessing.Shows;

public class AnimeEnrichmentService(
    IMediaTypeClassifier mediaTypeClassifier,
    IAniListMetadataProvider aniListMetadataProvider,
    IJikanMetadataProvider jikanMetadataProvider,
    IShowRepository showRepository,
    IMovieRepository movieRepository
) : IAnimeEnrichmentService
{
    public async Task EnrichTvAsync(
        int tvId,
        string title,
        int? year,
        string[]? originCountry,
        bool? priority = false
    )
    {
        string? classification = await mediaTypeClassifier.ClassifyAsync(
            title,
            year,
            originCountry
        );
        if (classification != "anime")
            return;

        (AniListMedia? aniListMatch, JikanAnime? jikanMatch) = await FetchAsync(
            title,
            year,
            priority
        );

        if (aniListMatch is not null)
        {
            IEnumerable<AnimeThemeTv> themes = await ResolveThemesAsync(
                aniListMatch,
                name => showRepository.ResolveAnimeThemeIdAsync(name),
                animeThemeId => new AnimeThemeTv { AnimeThemeId = animeThemeId, TvId = tvId }
            );
            await showRepository.StoreAnimeThemes(themes);

            if (aniListMatch.SeasonYear is not null && aniListMatch.Season is not null)
                await showRepository.StoreAnimeSeason(
                    tvId,
                    aniListMatch.SeasonYear.Value,
                    aniListMatch.Season
                );
        }

        if (jikanMatch is not null && jikanMatch.Demographics.Length > 0)
        {
            IEnumerable<AnimeDemographicTv> demographics = await ResolveDemographicsAsync(
                jikanMatch,
                name => showRepository.ResolveAnimeDemographicIdAsync(name),
                animeDemographicId => new AnimeDemographicTv
                {
                    AnimeDemographicId = animeDemographicId,
                    TvId = tvId,
                }
            );
            await showRepository.StoreAnimeDemographics(demographics);
        }
    }

    public async Task EnrichMovieAsync(
        int movieId,
        string title,
        int? year,
        string[]? originCountry,
        bool? priority = false
    )
    {
        string? classification = await mediaTypeClassifier.ClassifyAsync(
            title,
            year,
            originCountry
        );
        if (classification != "anime")
            return;

        (AniListMedia? aniListMatch, JikanAnime? jikanMatch) = await FetchAsync(
            title,
            year,
            priority
        );

        if (aniListMatch is not null)
        {
            IEnumerable<AnimeThemeMovie> themes = await ResolveThemesAsync(
                aniListMatch,
                name => movieRepository.ResolveAnimeThemeIdAsync(name),
                animeThemeId => new AnimeThemeMovie
                {
                    AnimeThemeId = animeThemeId,
                    MovieId = movieId,
                }
            );
            await movieRepository.StoreAnimeThemes(themes);

            if (aniListMatch.SeasonYear is not null && aniListMatch.Season is not null)
                await movieRepository.StoreAnimeSeason(
                    movieId,
                    aniListMatch.SeasonYear.Value,
                    aniListMatch.Season
                );
        }

        if (jikanMatch is not null && jikanMatch.Demographics.Length > 0)
        {
            IEnumerable<AnimeDemographicMovie> demographics = await ResolveDemographicsAsync(
                jikanMatch,
                name => movieRepository.ResolveAnimeDemographicIdAsync(name),
                animeDemographicId => new AnimeDemographicMovie
                {
                    AnimeDemographicId = animeDemographicId,
                    MovieId = movieId,
                }
            );
            await movieRepository.StoreAnimeDemographics(demographics);
        }
    }

    private async Task<(AniListMedia?, JikanAnime?)> FetchAsync(
        string title,
        int? year,
        bool? priority
    )
    {
        AniListMedia? aniListMatch = await aniListMetadataProvider.SearchAsync(
            title,
            year,
            priority
        );
        JikanAnime? jikanMatch = await jikanMetadataProvider.SearchAsync(title, year, priority);
        return (aniListMatch, jikanMatch);
    }

    // AniList tags carry no stable numeric id (unlike TMDB genres), so each
    // distinct tag name is resolved against the AnimeThemes table by name,
    // creating the row on first sight, before a join row can reference it.
    private static async Task<IEnumerable<TLink>> ResolveThemesAsync<TLink>(
        AniListMedia aniListMatch,
        Func<string, Task<int>> resolveThemeId,
        Func<int, TLink> toLink
    )
    {
        List<TLink> links = [];
        foreach (
            AniListMediaTag tag in aniListMatch.Tags.Where(t => !string.IsNullOrWhiteSpace(t.Name))
        )
        {
            int animeThemeId = await resolveThemeId(tag.Name);
            links.Add(toLink(animeThemeId));
        }

        return links;
    }

    // Jikan demographics carry a MalId that is only stable within MyAnimeList's
    // own numbering, not the local AnimeDemographics table, so each distinct
    // demographic name is resolved by name (creating the row on first sight)
    // before a join row can reference it — mirrors ResolveThemesAsync above.
    private static async Task<IEnumerable<TLink>> ResolveDemographicsAsync<TLink>(
        JikanAnime jikanMatch,
        Func<string, Task<int>> resolveDemographicId,
        Func<int, TLink> toLink
    )
    {
        List<TLink> links = [];
        foreach (
            JikanGenre demographic in jikanMatch.Demographics.Where(d =>
                !string.IsNullOrWhiteSpace(d.Name)
            )
        )
        {
            int animeDemographicId = await resolveDemographicId(demographic.Name);
            links.Add(toLink(animeDemographicId));
        }

        return links;
    }
}
