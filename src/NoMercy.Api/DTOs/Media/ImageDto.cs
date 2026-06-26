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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Media;

public record ImageDto
{
    [JsonProperty("height")]
    public long Height { get; set; }

    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("src")]
    public string? Src { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("width")]
    public long Width { get; set; }

    [JsonProperty("iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty("voteAverage")]
    public double VoteAverage { get; set; }

    [JsonProperty("voteCount")]
    public long VoteCount { get; set; }

    [JsonProperty("color_palette")]
    public IColorPalettes? ColorPalette { get; set; }

    public ImageDto() { }

    public ImageDto(Image media)
    {
        Id = media.Id;
        Src =
            media.Site == "https://image.tmdb.org/t/p/"
                ? new Uri(media.FilePath, UriKind.Relative).ToString()
                : new Uri($"/images/music{media.FilePath}", UriKind.Relative).ToString();
        Width = media.Width ?? 0;
        Type = media.Type;
        Height = media.Height ?? 0;
        Iso6391 = media.Iso6391;
        VoteAverage = media.VoteAverage ?? 0;
        VoteCount = media.VoteCount ?? 0;
        ColorPalette = media.ColorPalette;
    }

    public ImageDto(TmdbImage media)
    {
        Id = HashToId(media.FilePath);
        Src = media.FilePath;
        Width = media.Width;
        Height = media.Height;
        Iso6391 = media.Iso6391;
        VoteAverage = media.VoteAverage;
        VoteCount = media.VoteCount;
        Type = media.Width >= media.Height ? "backdrop" : "poster";
        ColorPalette = new();
    }

    public ImageDto(TmdbProfile image)
    {
        Id = HashToId(image.FilePath);
        Src = image.FilePath;
        Width = image.Width;
        Height = image.Height;
        Iso6391 = image.Iso6391;
        Type = "poster";
        VoteAverage = 0;
        VoteCount = 0;
        ColorPalette = new();
    }

    /// <summary>
    /// Stable surrogate id for an image without a TMDB-issued primary key.
    /// Derives a long from the file path's hash. The original implementation
    /// chained '.Replace("-", "1").TrimStart(\'0\')' which crashes when the
    /// hash is exactly 0 (TrimStart returns empty → long.Parse throws). Use
    /// math instead so 0 stays 0 and negatives flip cleanly.
    /// </summary>
    private static long HashToId(string? filePath)
    {
        int hash = (filePath ?? string.Empty).GetHashCode();
        return hash < 0 ? -(long)hash + 1_000_000_000L : hash;
    }
}
