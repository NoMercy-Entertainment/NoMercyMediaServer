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
using NoMercy.Data.DTOs.Specials;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record SpecialResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "collection")]
    public IEnumerable<SpecialItemDto>? Special { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; }

    [JsonProperty(propertyName: "total_duration")]
    public int TotalDuration { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "cast")]
    public IEnumerable<PeopleDto> Cast { get; set; }

    [JsonProperty(propertyName: "crew")]
    public IEnumerable<PeopleDto> Crew { get; set; }

    [JsonProperty(propertyName: "backdrops")]
    public IEnumerable<ImageDto> Backdrops { get; set; }

    [JsonProperty(propertyName: "posters")]
    public IEnumerable<ImageDto> Posters { get; set; }

    [JsonProperty(propertyName: "content_ratings")]
    public IEnumerable<Certification?> ContentRatings { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    public SpecialResponseItemDto(Special special, List<SpecialItemsDto> items)
    {
        List<SpecialItemDto> specialItems = [];
        foreach (SpecialItem specialItem in special.Items)
            if (specialItem.MovieId is not null)
            {
                SpecialItemsDto? newItem = items.Find(match: i => i.Id == specialItem.MovieId);
                if (newItem is null)
                    continue;

                SpecialItemDto item = new(item: newItem);
                specialItems.Add(item: item);
            }
            else
            {
                SpecialItemsDto? newItem = items.FirstOrDefault(predicate: i =>
                    i.EpisodeIds.Contains(value: specialItem.EpisodeId ?? 0)
                );
                if (newItem is null)
                    continue;

                SpecialItemDto item = new(item: newItem);
                specialItems.Add(item: item);
            }

        IEnumerable<PeopleDto> cast = items
            .SelectMany(selector: tv => tv.Cast)
            .DistinctBy(keySelector: people => people.Id)
            .ToList();

        IEnumerable<PeopleDto> crew = items
            .SelectMany(selector: item => item.Crew)
            .DistinctBy(keySelector: people => people.Id)
            .ToList();

        IEnumerable<ImageDto> posters = items.SelectMany(selector: item => item.Posters).ToList();

        IEnumerable<ImageDto> backdrops = items.SelectMany(selector: item => item.Backdrops).ToList();

        IEnumerable<GenreDto> genres = items
            .SelectMany(selector: item => item.Genres)
            .DistinctBy(keySelector: genre => genre.Id)
            .ToList();

        foreach (SpecialItemsDto item in items)
        {
            item.Posters = [];
            item.Backdrops = [];
            item.Cast = [];
            item.Crew = [];
            item.Genres = [];
        }

        Id = special.Id;
        Title = special.Title.OrEmpty();
        Overview = special.Overview;
        Backdrop = special.Backdrop?.Replace(oldValue: "https://storage.nomercy.tv/laravel", newValue: "");
        Poster = special.Poster;
        Logo = special.Logo;
        TitleSort = special.Title.TitleSort();
        Type = "specials";
        MediaType = "specials";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);
        ColorPalette = special.ColorPalette;
        Backdrops = backdrops;
        Posters = posters;
        Cast = cast;
        Crew = crew;
        Genres = genres;

        Favorite = special.SpecialUser.Count != 0;
        Watched =
            special.Items.Count(predicate: specialItem => specialItem.UserData.Count > 0)
            == special.Items.Count;

        NumberOfItems = special.Items.Count;

        int haveMovies = special.Items.Count(predicate: item =>
            item.MovieId is not null && item.Movie?.VideoFiles.Count > 0
        );
        int haveEpisodes = special.Items.Count(predicate: item =>
            item.EpisodeId is not null && item.Episode?.VideoFiles.Count > 0
        );
        HaveItems = haveMovies + haveEpisodes;

        TotalDuration = items.Sum(selector: item => item.TotalDuration);

        VoteAverage =
            items.Where(predicate: item => item.VoteAverage != null).Select(selector: item => item.VoteAverage).Average()
            ?? 0;

        ContentRatings = items
            .Select(selector: specialItem => specialItem.Rating)
            .DistinctBy(keySelector: rating => rating.Iso31661);

        Special = specialItems.DistinctBy(keySelector: si => si.Id);
    }

    public SpecialResponseItemDto(Special special)
    {
        Id = special.Id;
        Title = special.Title.OrEmpty();
        Overview = special.Overview;
        Backdrop = special.Backdrop?.Replace(oldValue: "https://storage.nomercy.tv/laravel", newValue: "");
        Logo = special.Logo;
        Poster = special.Poster;
        TitleSort = special.Title.TitleSort();
        Type = "specials";
        MediaType = "specials";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);
        ColorPalette = special.ColorPalette;
        Favorite = special.SpecialUser.Count != 0;
        NumberOfItems = special.Items.Count;

        int movies = special.Items.Count(predicate: item =>
            item.MovieId is not null && item.Movie?.VideoFiles.Count > 0
        );
        int episodes = special.Items.Count(predicate: item =>
            item.EpisodeId is not null && item.Episode?.VideoFiles.Count > 0
        );

        HaveItems = movies + episodes;

        Cast = [];
        Crew = [];
        Backdrops = [];
        Posters = [];
        Genres = [];

        TotalDuration = special.Items.Sum(selector: item => item.Movie?.Runtime ?? 0);

        VoteAverage =
            special
                .Items.Where(predicate: item => item.Movie?.VoteAverage != null)
                .Select(selector: item => item.Movie?.VoteAverage)
                .Average()
            ?? 0;

        ContentRatings = special
            .Items.Select(selector: specialItem =>
                specialItem
                    .Movie?.CertificationMovies.Select(selector: certification => certification.Certification)
                    .FirstOrDefault()
            )
            .DistinctBy(keySelector: rating => rating?.Iso31661);
    }

    public SpecialResponseItemDto(SpecialDetailDto detail, List<SpecialItemsDto> items)
    {
        List<SpecialItemDto> specialItems = [];
        foreach (SpecialItemRefDto itemRef in detail.Items)
            if (itemRef.MovieId is not null)
            {
                SpecialItemsDto? newItem = items.Find(match: i => i.Id == itemRef.MovieId);
                if (newItem is null)
                    continue;

                SpecialItemDto item = new(item: newItem);
                specialItems.Add(item: item);
            }
            else
            {
                SpecialItemsDto? newItem = items.FirstOrDefault(predicate: i =>
                    i.EpisodeIds.Contains(value: itemRef.EpisodeId ?? 0)
                );
                if (newItem is null)
                    continue;

                SpecialItemDto item = new(item: newItem);
                specialItems.Add(item: item);
            }

        IEnumerable<PeopleDto> cast = items
            .SelectMany(selector: tv => tv.Cast)
            .DistinctBy(keySelector: people => people.Id)
            .ToList();

        IEnumerable<PeopleDto> crew = items
            .SelectMany(selector: item => item.Crew)
            .DistinctBy(keySelector: people => people.Id)
            .ToList();

        IEnumerable<ImageDto> posters = items.SelectMany(selector: item => item.Posters).ToList();

        IEnumerable<ImageDto> backdrops = items.SelectMany(selector: item => item.Backdrops).ToList();

        IEnumerable<GenreDto> genres = items
            .SelectMany(selector: item => item.Genres)
            .DistinctBy(keySelector: genre => genre.Id)
            .ToList();

        foreach (SpecialItemsDto item in items)
        {
            item.Posters = [];
            item.Backdrops = [];
            item.Cast = [];
            item.Crew = [];
            item.Genres = [];
        }

        Id = detail.Id;
        Title = detail.Title;
        Overview = detail.Overview;
        Backdrop = detail.Backdrop?.Replace(oldValue: "https://storage.nomercy.tv/laravel", newValue: "");
        Poster = detail.Poster;
        Logo = detail.Logo;
        TitleSort = detail.Title.TitleSort();
        Type = "specials";
        MediaType = "specials";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);
        ColorPalette = ColorPalette.FromJsonOrNull(json: detail.ColorPalette);
        Backdrops = backdrops;
        Posters = posters;
        Cast = cast;
        Crew = crew;
        Genres = genres;

        Favorite = detail.Favorite;

        NumberOfItems = detail.NumberOfItems;
        HaveItems = detail.HaveMovies + detail.HaveEpisodes;

        TotalDuration = items.Sum(selector: item => item.TotalDuration);

        VoteAverage =
            items.Where(predicate: item => item.VoteAverage != null).Select(selector: item => item.VoteAverage).Average()
            ?? 0;

        ContentRatings = items
            .Select(selector: specialItem => specialItem.Rating)
            .DistinctBy(keySelector: rating => rating.Iso31661);

        Special = specialItems.DistinctBy(keySelector: si => si.Id);
    }
}
