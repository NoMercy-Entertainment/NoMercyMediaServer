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
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;

namespace NoMercy.Api.DTOs.Media;

public record SeasonDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public long SeasonNumber { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "episodes")]
    public IEnumerable<EpisodeDto> Episodes { get; set; }

    [JsonProperty(propertyName: "translations")]
    public IEnumerable<TranslationDto> Translations { get; set; }

    public SeasonDto(Season season)
    {
        string? title = season.Translations.FirstOrDefault()?.Title;
        string? overview = season.Translations.FirstOrDefault()?.Overview;

        Id = season.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : season.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : season.Overview;
        Poster = season.Poster;
        SeasonNumber = season.SeasonNumber;
        ColorPalette = season.ColorPalette;
        Translations = season.Translations.Select(selector: translation => new TranslationDto(translation: translation));
        Episodes = season
            .Episodes.OrderBy(keySelector: episode => episode.EpisodeNumber)
            .Select(selector: episode => new EpisodeDto(episode: episode));
    }

    public SeasonDto(int tvId, TmdbSeason tmdbSeason, string country)
    {
        TmdbSeasonClient tmdbSeasonClient = new(tvId: tvId, seasonNumber: tmdbSeason.SeasonNumber);
        TmdbSeasonAppends? seasonData = tmdbSeasonClient.WithAllAppends().Result;

        string? title = seasonData
            ?.Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Data.Title;

        string? overview = seasonData
            ?.Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Data.Overview;

        Id = tmdbSeason.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tmdbSeason.Name;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tmdbSeason.Overview;
        Poster = tmdbSeason.PosterPath;
        SeasonNumber = tmdbSeason.SeasonNumber;
        ColorPalette = new();
        Translations =
            seasonData?.Translations.Translations.Select(selector: translation => new TranslationDto(
                translation: translation
            )) ?? [];
        Episodes =
            seasonData
                ?.Episodes.OrderBy(keySelector: episode => episode.EpisodeNumber)
                .Select(selector: episode => new EpisodeDto(
                    tvId: tvId,
                    seasonNumber: tmdbSeason.SeasonNumber,
                    episodeNumber: episode.EpisodeNumber,
                    language: country
                ))
            ?? [];
    }
}
