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
    public ColorPalette? ColorPalette { get; set; }

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
    /// Derives a long from a hash of the file path. Must not use
    /// string.GetHashCode() — .NET randomizes it per process for DoS
    /// hardening, so the same path would mint a different id after every
    /// server restart. FNV-1a is process-stable so the id survives restarts.
    /// </summary>
    private static long HashToId(string? filePath)
    {
        int hash = Fnv1AHash(filePath ?? string.Empty);
        return hash < 0 ? -(long)hash + 1_000_000_000L : hash;
    }

    private static int Fnv1AHash(string value)
    {
        unchecked
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffsetBasis;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return (int)hash;
        }
    }
}
