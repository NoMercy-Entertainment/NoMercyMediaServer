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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMHomeCard component - featured home page card with video trailer support.
/// </summary>
public record HomeCardData
{
    [JsonProperty(propertyName: "id")]
    public dynamic? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "rating", NullValueHandling = NullValueHandling.Ignore)]
    public RatingClass? Rating { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string? MediaType { get; set; }

    [JsonProperty(propertyName: "videos")]
    public IEnumerable<VideoInfo> Videos { get; set; } = [];

    [JsonProperty(propertyName: "videoID")]
    public string? VideoId { get; set; }

    public HomeCardData() { }

    public HomeCardData(Movie movie, string country)
    {
        string? title = movie.Translations.FirstOrDefault()?.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
        Year = movie.ReleaseDate.ParseYear();
        MediaType = "movie";
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count(predicate: v => v.Folder != null);
        ColorPalette = movie.ColorPalette;

        Videos = movie
            .Media.Where(predicate: m => m.Site == "YouTube")
            .Select(selector: m => new VideoInfo
            {
                Id = m.Src,
                Name = m.Name,
                Site = m.Site,
                Type = m.Type,
            });
        VideoId = Videos.FirstOrDefault()?.Id;

        Rating = movie
            .CertificationMovies.Where(predicate: cm =>
                cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country
            )
            .Select(selector: cm => new RatingClass
            {
                Rating = cm.Certification.Rating,
                Iso31661 = cm.Certification.Iso31661,
                Image = new(
                    value: $"/{cm.Certification.Iso31661}/{cm.Certification.Iso31661}_{cm.Certification.Rating}.svg"
                ),
            })
            .FirstOrDefault();
    }

    public HomeCardData(Tv tv, string country)
    {
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tv.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
        Year = tv.FirstAirDate.ParseYear();
        MediaType = "tv";
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null));
        ColorPalette = tv.ColorPalette;

        Videos = tv
            .Media.Where(predicate: m => m.Site == "YouTube")
            .Select(selector: m => new VideoInfo
            {
                Id = m.Src,
                Name = m.Name,
                Site = m.Site,
                Type = m.Type,
            });
        VideoId = Videos.FirstOrDefault()?.Id;

        Rating = tv
            .CertificationTvs.Where(predicate: ct =>
                ct.Certification.Iso31661 == "US" || ct.Certification.Iso31661 == country
            )
            .Select(selector: ct => new RatingClass
            {
                Rating = ct.Certification.Rating,
                Iso31661 = ct.Certification.Iso31661,
                Image = new(
                    value: $"/{ct.Certification.Iso31661}/{ct.Certification.Iso31661}_{ct.Certification.Rating}.svg"
                ),
            })
            .FirstOrDefault();
    }

    public HomeCardData(NmCardDto cardDto)
    {
        Id = cardDto.Id;
        Title = cardDto.Title;
        Overview = cardDto.Overview;
        Poster = cardDto.Poster;
        Backdrop = cardDto.Backdrop;
        Logo = cardDto.Logo;
        Year = cardDto.Year;
        Duration = cardDto.Duration;
        Link = cardDto.Link;
        Rating = cardDto.Rating;
        ColorPalette = cardDto.ColorPalette;
        HaveItems = cardDto.HaveItems;
        NumberOfItems = cardDto.NumberOfItems;
        MediaType = cardDto.Type;
    }
}

public record VideoInfo
{
    [JsonProperty(propertyName: "id")]
    public string? Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "site")]
    public string? Site { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }
}
