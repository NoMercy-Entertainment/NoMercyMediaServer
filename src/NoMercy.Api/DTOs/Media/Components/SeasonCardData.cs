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

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMSeasonCard component - displays an episode in a season.
/// </summary>
public record SeasonCardData
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

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    public SeasonCardData() { }

    public SeasonCardData(Episode episode)
    {
        string? title = episode.Translations.FirstOrDefault()?.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview;

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
        Link = new(
            uriString: $"/tv/{TvId}/watch?season={SeasonNumber}&episode={EpisodeNumber}",
            uriKind: UriKind.Relative
        );
    }

    public SeasonCardData(EpisodeDto dto)
    {
        Id = dto.Id;
        EpisodeNumber = dto.EpisodeNumber;
        SeasonNumber = dto.SeasonNumber;
        Title = dto.Title;
        Overview = dto.Overview;
        AirDate = dto.AirDate;
        Still = dto.Still;
        ColorPalette = dto.ColorPalette;
        Progress = dto.Progress;
        Available = dto.Available;
        TvId = dto.TvId;
        Link = dto.Link;
    }

    public SeasonCardData(MissingEpisodeDto dto)
    {
        Id = dto.Id;
        EpisodeNumber = dto.EpisodeNumber;
        SeasonNumber = dto.SeasonNumber;
        Title = dto.Title;
        Overview = dto.Overview;
        AirDate = dto.AirDate;
        Still = dto.Still;
        ColorPalette = dto.ColorPalette;
        Progress = dto.Progress;
        Available = dto.Available;
        TvId = dto.TvId;
        Link = dto.Link;
    }
}

/// <summary>
/// Data for NMSeasonTitle component - displays a season header.
/// </summary>
public record SeasonTitleData
{
    [JsonProperty(propertyName: "seasonNumber")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "episodeCount")]
    public int EpisodeCount { get; set; }

    public SeasonTitleData() { }

    public SeasonTitleData(int seasonNumber, int episodeCount)
    {
        SeasonNumber = seasonNumber;
        Title = $"Season {seasonNumber}";
        EpisodeCount = episodeCount;
    }
}
