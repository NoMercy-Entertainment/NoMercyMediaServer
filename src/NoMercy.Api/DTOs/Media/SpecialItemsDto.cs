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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record SpecialItemsDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "episode_ids")]
    public int[] EpisodeIds { get; set; }

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

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public long Year { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; }

    [JsonProperty(propertyName: "backdrops")]
    public IEnumerable<ImageDto> Backdrops { get; set; }

    [JsonProperty(propertyName: "posters")]
    public IEnumerable<ImageDto> Posters { get; set; }

    [JsonProperty(propertyName: "cast")]
    public IEnumerable<PeopleDto> Cast { get; set; }

    [JsonProperty(propertyName: "crew")]
    public IEnumerable<PeopleDto> Crew { get; set; }

    [JsonProperty(propertyName: "rating")]
    public Certification Rating { get; set; }

    [JsonProperty(propertyName: "videoId")]
    public string? VideoId { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int HaveItems { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "total_duration")]
    public int TotalDuration { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double? VoteAverage { get; set; }

    public SpecialItemsDto(Movie movie)
    {
        Id = movie.Id;
        EpisodeIds = [];
        Title = movie.Title;
        Overview = movie.Overview;

        Backdrop = movie.Backdrop;
        // Watched = movie.Watched;
        Logo = movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;

        Backdrops = movie
            .Images.Where(predicate: media => media.Type == "backdrop")
            .Take(count: 2)
            .Select(selector: media => new ImageDto(media: media));

        Posters = movie
            .Images.Where(predicate: media => media.Type == "poster")
            .Take(count: 2)
            .Select(selector: media => new ImageDto(media: media));

        MediaType = MediaTypes.MovieMediaType;
        ColorPalette = movie.ColorPalette;
        Poster = movie.Poster;
        Type = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Year = movie.ReleaseDate.ParseYear();
        Duration = movie.Runtime * 60 ?? 0;

        TotalDuration = movie.Runtime * 60 ?? 0;

        Genres = movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie.Genre));

        Rating =
            movie
                .CertificationMovies.Select(selector: certificationMovie => certificationMovie.Certification)
                .FirstOrDefault()
            ?? new Certification();

        VoteAverage = movie.VoteAverage;

        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count > 0 ? 1 : 0;

        VideoId = movie.Video;

        Cast = movie.Cast.Take(count: 15).Select(selector: cast => new PeopleDto(cast: cast));

        Crew = movie.Crew.Take(count: 15).Select(selector: crew => new PeopleDto(crew: crew));
    }

    public SpecialItemsDto(Tv tv)
    {
        Id = tv.Id;
        EpisodeIds = tv.Episodes.Select(selector: episode => episode.Id).ToArray();

        Title = tv.Title;
        Overview = tv.Overview;

        Backdrop = tv.Backdrop;
        // Watched = tv.Watched;
        Logo = tv.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;

        Backdrops = tv
            .Images.Where(predicate: media => media.Type == "backdrop")
            .Take(count: 2)
            .Select(selector: media => new ImageDto(media: media));

        Posters = tv
            .Images.Where(predicate: media => media.Type == "poster")
            .Take(count: 2)
            .Select(selector: media => new ImageDto(media: media));

        MediaType = "tv";
        ColorPalette = tv.ColorPalette;
        Poster = tv.Poster;
        Type = "tv";
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        Year = tv.FirstAirDate.ParseYear();

        VoteAverage = tv.VoteAverage;

        Genres = tv.GenreTvs.Select(selector: genreTv => new GenreDto(genreMovie: genreTv.Genre));

        Rating =
            tv.CertificationTvs.Select(selector: certificationTv => certificationTv.Certification)
                .FirstOrDefault()
            ?? new Certification();

        NumberOfItems = tv.Episodes.Count(predicate: e => e.SeasonNumber > 0);
        int have = tv
            .Episodes.Where(predicate: e => e.SeasonNumber > 0)
            .Count(predicate: episode => episode.VideoFiles.Count != 0);

        HaveItems = have;

        Duration = tv.Duration * have * 60 ?? 0;

        TotalDuration = tv.Episodes.Sum(selector: item =>
            item.VideoFiles.FirstOrDefault()?.Duration?.ToSeconds() ?? 0
        );

        // Watched = tv.Episodes
        //     .SelectMany(episode => episode!.VideoFiles
        //         .Where(videoFile => videoFile.UserData.Any(userData => userData.UserId.Equals(userId)))
        //     .Count();

        VideoId = tv.Trailer;

        Cast = tv.Cast.Take(count: 15).Select(selector: cast => new PeopleDto(cast: cast));

        Crew = tv.Crew.Take(count: 15).Select(selector: crew => new PeopleDto(crew: crew));
    }

    public SpecialItemsDto(SpecialMovieProjection movie)
    {
        Id = movie.Id;
        EpisodeIds = [];
        Title = movie.Title;
        Overview = movie.Overview;
        Backdrop = movie.Backdrop;
        Logo = movie.Logo;

        Backdrops = movie.Backdrops.Select(selector: i => new ImageDto
        {
            Id = i.Id,
            Src =
                i.Site == "https://image.tmdb.org/t/p/"
                    ? new Uri(uriString: i.FilePath!, uriKind: UriKind.Relative).ToString()
                    : new Uri(uriString: $"/images/music{i.FilePath}", uriKind: UriKind.Relative).ToString(),
            Width = i.Width,
            Type = i.Type,
            Height = i.Height,
            Iso6391 = i.Iso6391,
            VoteAverage = i.VoteAverage,
            VoteCount = i.VoteCount,
            ColorPalette = ColorPalette.FromJsonOrNull(json: i.ColorPalette),
        });

        Posters = movie.Posters.Select(selector: i => new ImageDto
        {
            Id = i.Id,
            Src =
                i.Site == "https://image.tmdb.org/t/p/"
                    ? new Uri(uriString: i.FilePath!, uriKind: UriKind.Relative).ToString()
                    : new Uri(uriString: $"/images/music{i.FilePath}", uriKind: UriKind.Relative).ToString(),
            Width = i.Width,
            Type = i.Type,
            Height = i.Height,
            Iso6391 = i.Iso6391,
            VoteAverage = i.VoteAverage,
            VoteCount = i.VoteCount,
            ColorPalette = ColorPalette.FromJsonOrNull(json: i.ColorPalette),
        });

        MediaType = MediaTypes.MovieMediaType;
        ColorPalette = ColorPalette.FromJsonOrNull(json: movie.ColorPalette);
        Poster = movie.Poster;
        Type = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{movie.Id}", uriKind: UriKind.Relative);
        Year = movie.ReleaseDate.ParseYear();
        Duration = movie.Runtime * 60 ?? 0;
        TotalDuration = movie.Runtime * 60 ?? 0;
        VoteAverage = movie.VoteAverage;

        Genres = movie.Genres.Select(selector: g => new GenreDto
        {
            Id = g.Id,
            Name = g.Name,
            Link = new(uriString: $"/genres/{g.Id}", uriKind: UriKind.Relative),
        });

        Rating = new()
        {
            Rating = movie.CertificationRating.OrEmpty(),
            Iso31661 = movie.CertificationCountry.OrEmpty(),
        };

        NumberOfItems = 1;
        HaveItems = movie.VideoFileCount > 0 ? 1 : 0;
        VideoId = movie.Video;

        Cast = movie.Cast.Select(selector: c => new PeopleDto
        {
            Id = c.PersonId,
            Name = c.PersonName,
            ProfilePath = c.PersonProfile,
            KnownForDepartment = c.PersonKnownForDepartment,
            ColorPalette = ColorPalette.FromJsonOrNull(json: c.PersonColorPalette),
            DeathDay = c.PersonDeathDay,
            Gender = c.PersonGender,
            Character = c.Character,
            Order = c.Order,
            Link = new(uriString: $"/person/{c.PersonId}", uriKind: UriKind.Relative),
            Translations = [],
        });

        Crew = movie.Crew.Select(selector: c => new PeopleDto
        {
            Id = c.PersonId,
            Name = c.PersonName,
            ProfilePath = c.PersonProfile,
            KnownForDepartment = c.PersonKnownForDepartment,
            ColorPalette = ColorPalette.FromJsonOrNull(json: c.PersonColorPalette),
            DeathDay = c.PersonDeathDay,
            Gender = c.PersonGender,
            Job = c.Task,
            Order = c.Order,
            Link = new(uriString: $"/person/{c.PersonId}", uriKind: UriKind.Relative),
            Translations = [],
        });
    }

    public SpecialItemsDto(SpecialTvProjection tv)
    {
        Id = tv.Id;
        EpisodeIds = tv.EpisodeIds;
        Title = tv.Title;
        Overview = tv.Overview;
        Backdrop = tv.Backdrop;
        Logo = tv.Logo;

        Backdrops = tv.Backdrops.Select(selector: i => new ImageDto
        {
            Id = i.Id,
            Src =
                i.Site == "https://image.tmdb.org/t/p/"
                    ? new Uri(uriString: i.FilePath!, uriKind: UriKind.Relative).ToString()
                    : new Uri(uriString: $"/images/music{i.FilePath}", uriKind: UriKind.Relative).ToString(),
            Width = i.Width,
            Type = i.Type,
            Height = i.Height,
            Iso6391 = i.Iso6391,
            VoteAverage = i.VoteAverage,
            VoteCount = i.VoteCount,
            ColorPalette = ColorPalette.FromJsonOrNull(json: i.ColorPalette),
        });

        Posters = tv.Posters.Select(selector: i => new ImageDto
        {
            Id = i.Id,
            Src =
                i.Site == "https://image.tmdb.org/t/p/"
                    ? new Uri(uriString: i.FilePath!, uriKind: UriKind.Relative).ToString()
                    : new Uri(uriString: $"/images/music{i.FilePath}", uriKind: UriKind.Relative).ToString(),
            Width = i.Width,
            Type = i.Type,
            Height = i.Height,
            Iso6391 = i.Iso6391,
            VoteAverage = i.VoteAverage,
            VoteCount = i.VoteCount,
            ColorPalette = ColorPalette.FromJsonOrNull(json: i.ColorPalette),
        });

        MediaType = "tv";
        ColorPalette = ColorPalette.FromJsonOrNull(json: tv.ColorPalette);
        Poster = tv.Poster;
        Type = "tv";
        Link = new(uriString: $"/tv/{tv.Id}", uriKind: UriKind.Relative);
        Year = tv.FirstAirDate.ParseYear();
        VoteAverage = tv.VoteAverage;

        Genres = tv.Genres.Select(selector: g => new GenreDto
        {
            Id = g.Id,
            Name = g.Name,
            Link = new(uriString: $"/genres/{g.Id}", uriKind: UriKind.Relative),
        });

        Rating = new()
        {
            Rating = tv.CertificationRating.OrEmpty(),
            Iso31661 = tv.CertificationCountry.OrEmpty(),
        };

        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.HaveEpisodes;
        Duration = tv.Duration * tv.HaveEpisodes * 60 ?? 0;
        TotalDuration = tv.EpisodeDurations.Sum(selector: d => d?.ToSeconds() ?? 0);
        VideoId = tv.Trailer;

        Cast = tv.Cast.Select(selector: c => new PeopleDto
        {
            Id = c.PersonId,
            Name = c.PersonName,
            ProfilePath = c.PersonProfile,
            KnownForDepartment = c.PersonKnownForDepartment,
            ColorPalette = ColorPalette.FromJsonOrNull(json: c.PersonColorPalette),
            DeathDay = c.PersonDeathDay,
            Gender = c.PersonGender,
            Character = c.Character,
            Order = c.Order,
            Link = new(uriString: $"/person/{c.PersonId}", uriKind: UriKind.Relative),
            Translations = [],
        });

        Crew = tv.Crew.Select(selector: c => new PeopleDto
        {
            Id = c.PersonId,
            Name = c.PersonName,
            ProfilePath = c.PersonProfile,
            KnownForDepartment = c.PersonKnownForDepartment,
            ColorPalette = ColorPalette.FromJsonOrNull(json: c.PersonColorPalette),
            DeathDay = c.PersonDeathDay,
            Gender = c.PersonGender,
            Job = c.Task,
            Order = c.Order,
            Link = new(uriString: $"/person/{c.PersonId}", uriKind: UriKind.Relative),
            Translations = [],
        });
    }
}
