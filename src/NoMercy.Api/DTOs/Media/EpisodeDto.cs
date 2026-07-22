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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;

namespace NoMercy.Api.DTOs.Media;

public class EpisodeDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "episode_number")]
    public long EpisodeNumber { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public long SeasonNumber { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "airDate")]
    public DateTime? AirDate { get; set; }

    [JsonProperty(propertyName: "still")]
    public string? Still { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "progress")]
    public object? Progress { get; set; }

    [JsonProperty(propertyName: "available")]
    public bool Available { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int TvId { get; set; }

    [JsonProperty(propertyName: "translations")]
    public IEnumerable<TranslationDto> Translations { get; set; } = [];

    [JsonProperty(propertyName: "link")]
    public Uri Link =>
        new(uriString: $"/tv/{TvId}/watch?season={SeasonNumber}&episode={EpisodeNumber}", uriKind: UriKind.Relative);

    public EpisodeDto(Episode episode)
    {
        string? title = episode.Translations.FirstOrDefault()?.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview;

        VideoFile? videoFile = episode.VideoFiles.FirstOrDefault();
        UserData? userData = videoFile?.UserData.FirstOrDefault();

        TvId = episode.TvId;
        Id = episode.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : episode.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : episode.Overview;
        EpisodeNumber = episode.EpisodeNumber;
        SeasonNumber = episode.SeasonNumber;
        AirDate = episode.AirDate;
        Still = episode.Still;
        ColorPalette = episode.ColorPalette;
        Available = episode.VideoFiles.Count != 0;
        Translations = episode.Translations.Select(selector: translation => new TranslationDto(translation: translation));

        Progress =
            userData?.UpdatedAt is not null && videoFile?.Duration is not null
                ? (int)
                    Math.Round(
                        a: (double)(100 * (userData.Time ?? 0))
                           / (videoFile.Duration?.ToSeconds() ?? 0)
                    )
                : null;
    }

    public EpisodeDto(int tvId, int seasonNumber, int episodeNumber, string language)
    {
        TmdbEpisodeClient tmdbEpisodeClient = new(id: tvId, seasonNumber: seasonNumber, episodeNumber: episodeNumber);
        TmdbEpisodeAppends? episodeData = tmdbEpisodeClient.WithAllAppends().Result;

        if (episodeData is null)
            return;

        string? overview = episodeData
            .Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso6391 == language
            )
            ?.Data.Overview;

        Id = episodeData.Id;
        Title = episodeData.Name;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : episodeData.Overview;
        EpisodeNumber = episodeData.EpisodeNumber;
        SeasonNumber = episodeData.SeasonNumber;
        AirDate = episodeData.AirDate;
        Still = episodeData.StillPath;
        ColorPalette = new();
        Available = false;

        Translations = episodeData.Translations.Translations.Select(
            selector: translation => new TranslationDto(translation: translation)
        );
    }
}

public class MissingEpisodeDto(Episode episode) : EpisodeDto(episode: episode)
{
    private readonly Episode _episode = episode;

    [JsonProperty(propertyName: "link")]
    public new Uri Link =>
        new(
            uriString: $"https://www.themoviedb.org/tv/{_episode.TvId}/season/{_episode.SeasonNumber}/episode/{_episode.EpisodeNumber}",
            uriKind: UriKind.Absolute
        );

    [JsonProperty(propertyName: "available")]
    public new bool Available => true;
}
