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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Artwork;

public class TvdbArtworkResponse : TvdbResponse<TvdbArtwork> { }

public class TvdbArtworkExtendedResponse : TvdbResponse<TvdbArtworkExtended> { }

public class TvdbArtworkStatusesResponse : TvdbResponse<TvdbStatus[]> { }

public class TvdbArtworkTypesResponse : TvdbResponse<TvdbArtworkType[]> { }

public class TvdbArtwork
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "image")]
    public string Image { get; set; } = string.Empty;

    [JsonProperty(propertyName: "thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;

    [JsonProperty(propertyName: "language")]
    public string? Language { get; set; }

    [JsonProperty(propertyName: "type")]
    public int Type { get; set; }

    [JsonProperty(propertyName: "score")]
    public double Score { get; set; }

    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "includesText")]
    public bool IncludesText { get; set; }
}

public class TvdbArtworkExtended : TvdbArtwork
{
    [JsonProperty(propertyName: "episodeId")]
    public long? EpisodeId { get; set; }

    [JsonProperty(propertyName: "movieId")]
    public long? MovieId { get; set; }

    [JsonProperty(propertyName: "networkId")]
    public long? NetworkId { get; set; }

    [JsonProperty(propertyName: "peopleId")]
    public long? PeopleId { get; set; }

    [JsonProperty(propertyName: "seasonId")]
    public long? SeasonId { get; set; }

    [JsonProperty(propertyName: "seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty(propertyName: "seriesPeopleId")]
    public long? SeriesPeopleId { get; set; }

    [JsonProperty(propertyName: "status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty(propertyName: "thumbnailHeight")]
    public int ThumbnailHeight { get; set; }

    [JsonProperty(propertyName: "thumbnailWidth")]
    public int ThumbnailWidth { get; set; }

    [JsonProperty(propertyName: "updatedAt")]
    public long UpdatedAt { get; set; }
}

public class TvdbArtworkType
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "recordType")]
    public string RecordType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "imageFormat")]
    public string ImageFormat { get; set; } = string.Empty;

    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "thumbWidth")]
    public int ThumbWidth { get; set; }

    [JsonProperty(propertyName: "thumbHeight")]
    public int ThumbHeight { get; set; }
}
