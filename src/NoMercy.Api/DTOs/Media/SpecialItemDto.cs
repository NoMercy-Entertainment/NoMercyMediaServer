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
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record SpecialItemDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public long Year { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "rating")]
    public Certification? Rating { get; set; }

    [JsonProperty(propertyName: "videoId")]
    public string? VideoId { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int HaveItems { get; set; }

    public SpecialItemDto(SpecialItemsDto item)
    {
        Id = item.Id;
        Title = item.Title;
        TitleSort = item.TitleSort();
        Overview = item.Overview;
        Backdrop = item.Backdrop;
        Favorite = item.Favorite;
        Logo = item.Logo;
        Genres = item.Genres;
        MediaType = item.MediaType;
        ColorPalette = item.ColorPalette;
        Poster = item.Poster;
        Type = item.Type;
        Year = item.Year;
        Rating = item.Rating;
        NumberOfItems = item.NumberOfItems;
        HaveItems = item.HaveItems;
        VideoId = item.VideoId;
        Duration = item.Duration;
        Link = new(uriString: $"/{item.MediaType}/{item.Id}", uriKind: UriKind.Relative);
    }
}
