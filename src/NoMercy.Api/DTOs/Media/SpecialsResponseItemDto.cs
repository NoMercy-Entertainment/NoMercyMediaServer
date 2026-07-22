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
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record SpecialsResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "titleSort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "genres")]
    public GenreDto[]? Genres { get; set; } = [];

    [JsonProperty(propertyName: "videoId")]
    public string? VideoId { get; set; }

    [JsonProperty(propertyName: "videos")]
    public VideoDto[]? Videos { get; set; } = [];

    [JsonProperty(propertyName: "total_duration")]
    public int TotalDuration { get; set; }

    public SpecialsResponseItemDto(SpecialItem item)
    {
        if (item.Movie is null)
            return;

        string? title = item.Movie.Translations.FirstOrDefault()?.Title;
        string? overview = item.Movie.Translations.FirstOrDefault()?.Overview;

        Id = item.Movie.Id.ToString();
        Title = !string.IsNullOrEmpty(value: title) ? title : item.Movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : item.Movie.Overview;

        Backdrop = item.Movie.Backdrop;
        Logo = item.Movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        MediaType = "item";
        Year = item.Movie.ReleaseDate.ParseYear();
        ColorPalette = item.Movie.ColorPalette;
        Poster = item.Movie.Poster;
        TitleSort = item.Movie.Title.TitleSort(date: item.Movie.ReleaseDate);
        Type = "item";
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Genres = item.Movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie)).ToArray();
        VideoId = item.Movie.Video;
        Videos = item
            .Movie.Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public SpecialsResponseItemDto(Special special)
    {
        Id = special.Id.ToString();
        Title = special.Title.OrEmpty();
        Overview = special.Overview;
        Backdrop = special.Backdrop;
        Logo = special.Logo;

        MediaType = "specials";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);
        Year = special.CreatedAt.ParseYear();

        ColorPalette = special.ColorPalette;
        Poster = special.Poster;
        TitleSort = special.Title.TitleSort();

        Type = "specials";

        NumberOfItems = special.Items.Count;

        int haveMovies = special
            .Items.Select(selector: item => item.Movie)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        int haveEpisodes = special
            .Items.Select(selector: item => item.Episode)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        HaveItems = haveMovies + haveEpisodes;

        int[] movies = special
            .Items.Where(predicate: item => item.MovieId is not null)
            .Select(selector: item => item.Movie?.VideoFiles.FirstOrDefault()?.Duration?.ToSeconds() ?? 0)
            .ToArray();

        int[] episodes = special
            .Items.Where(predicate: item => item.EpisodeId is not null)
            .Select(selector: item => item.Episode?.VideoFiles.FirstOrDefault()?.Duration?.ToSeconds() ?? 0)
            .ToArray();

        TotalDuration = movies.Sum() + episodes.Sum();

        // VideoId = special.SpecialMovies?
        //     .FirstOrDefault()
        //     ?.Movie.Video;
    }
}
