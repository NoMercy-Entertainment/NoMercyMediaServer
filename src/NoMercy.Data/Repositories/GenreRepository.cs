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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public class GenreDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
}

public class GenreWithCountsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalMovies { get; set; }
    public int TotalTvShows { get; set; }
    public int MoviesWithVideo { get; set; }
    public int TvShowsWithVideo { get; set; }
}

public class GenreRepository(MediaContext context) : IGenreRepository
{
    private static readonly string[] Letters =
    [
        "*",
        "#",
        "'",
        "\"",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "0",
    ];

    public async Task<Genre?> GetGenreAsync(
        Guid userId,
        int id,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        return await context
            .Genres.AsNoTracking()
            .Where(predicate: genre => genre.Id == id)
            .Include(navigationPropertyPath: genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: genre =>
                genre
                    .GenreMovies.Where(gm =>
                        gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                    )
                    .Where(gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
                    .OrderBy(gm => gm.MovieId)
                    .Take(take)
            )
                .ThenInclude(navigationPropertyPath: gm => gm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: genre => genre.GenreMovies)
                .ThenInclude(navigationPropertyPath: gm => gm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: genre => genre.GenreMovies)
                .ThenInclude(navigationPropertyPath: gm => gm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.Images.Where(i => i.Type == "logo")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .Include(navigationPropertyPath: genre => genre.GenreMovies)
                .ThenInclude(navigationPropertyPath: gm => gm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.CertificationMovies.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: genre =>
                genre
                    .GenreTvShows.Where(gt =>
                        gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                    )
                    .Where(gt =>
                        gt.Tv.Episodes.Any(e =>
                            (
                                e.VideoFiles.Any(v => v.Folder != null)
                                || e.Tv.Episodes.Any(o =>
                                    o.SeasonNumber == e.SeasonNumber
                                    && o.VideoFiles.Any(w =>
                                        w.Folder != null
                                        && w.LastEpisodeNumber != null
                                        && o.EpisodeNumber <= e.EpisodeNumber
                                        && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                    )
                                )
                            )
                        )
                    )
                    .OrderBy(gt => gt.TvId)
                    .Take(take)
            )
                .ThenInclude(navigationPropertyPath: gt => gt.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: genre => genre.GenreTvShows)
                .ThenInclude(navigationPropertyPath: gt => gt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.Episodes.Where(e =>
                            e.SeasonNumber > 0
                            && (
                                e.VideoFiles.Any(v => v.Folder != null)
                                || e.Tv.Episodes.Any(o =>
                                    o.SeasonNumber == e.SeasonNumber
                                    && o.VideoFiles.Any(w =>
                                        w.Folder != null
                                        && w.LastEpisodeNumber != null
                                        && o.EpisodeNumber <= e.EpisodeNumber
                                        && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                    )
                                )
                            )
                        )
                    )
                        .ThenInclude(navigationPropertyPath: e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: genre => genre.GenreTvShows)
                .ThenInclude(navigationPropertyPath: gt => gt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.Images.Where(i => i.Type == "logo")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .Include(navigationPropertyPath: genre => genre.GenreTvShows)
                .ThenInclude(navigationPropertyPath: gt => gt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.CertificationTvs.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<(
        GenreDetailDto? Genre,
        List<HomeMovieCardDto> Movies,
        List<HomeTvCardDto> TvShows
    )> GetGenreCardsAsync(
        Guid userId,
        int id,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        GenreDetailDto? genreDetail = await context
            .Genres.AsNoTracking()
            .Where(predicate: genre => genre.Id == id)
            .Select(selector: genre => new GenreDetailDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TranslatedName = genre
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (genreDetail is null)
            return (null, [], []);

        List<HomeMovieCardDto> movies = await context
            .GenreMovie.AsNoTracking()
            .Where(predicate: gm => gm.GenreId == id)
            .Where(predicate: gm => gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
            .OrderBy(keySelector: gm => gm.Movie.TitleSort)
            .ThenBy(keySelector: gm => gm.MovieId)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: gm => new HomeMovieCardDto
            {
                Id = gm.Movie.Id,
                Title = gm.Movie.Title,
                TitleSort = gm.Movie.TitleSort,
                TranslatedTitle = gm
                    .Movie.Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = gm.Movie.Overview,
                TranslatedOverview = gm
                    .Movie.Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = gm.Movie.Poster,
                Backdrop = gm.Movie.Backdrop,
                Logo = gm
                    .Movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = gm.Movie.ReleaseDate,
                CreatedAt = gm.Movie.CreatedAt,
                ColorPalette = gm.Movie._colorPalette,
                VideoFileCount = gm.Movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = gm
                    .Movie.CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = gm
                    .Movie.CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);

        List<HomeTvCardDto> tvShows = await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt => gt.GenreId == id)
            .Where(predicate: gt => gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: gt =>
                gt.Tv.Episodes.Any(e =>
                    (
                        e.VideoFiles.Any(v => v.Folder != null)
                        || e.Tv.Episodes.Any(o =>
                            o.SeasonNumber == e.SeasonNumber
                            && o.VideoFiles.Any(w =>
                                w.Folder != null
                                && w.LastEpisodeNumber != null
                                && o.EpisodeNumber <= e.EpisodeNumber
                                && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                            )
                        )
                    )
                )
            )
            .OrderBy(keySelector: gt => gt.Tv.TitleSort)
            .ThenBy(keySelector: gt => gt.TvId)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: gt => new HomeTvCardDto
            {
                Id = gt.Tv.Id,
                Title = gt.Tv.Title,
                TitleSort = gt.Tv.TitleSort,
                TranslatedTitle = gt
                    .Tv.Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = gt.Tv.Overview,
                TranslatedOverview = gt
                    .Tv.Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = gt.Tv.Poster,
                Backdrop = gt.Tv.Backdrop,
                Logo = gt
                    .Tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = gt.Tv.FirstAirDate,
                CreatedAt = gt.Tv.CreatedAt,
                ColorPalette = gt.Tv._colorPalette,
                NumberOfEpisodes = gt.Tv.NumberOfEpisodes,
                EpisodesWithVideo = gt
                    .Tv.Episodes.Where(e => e.SeasonNumber > 0)
                    .Count(e =>
                        (
                            e.VideoFiles.Any(v => v.Folder != null)
                            || e.Tv.Episodes.Any(o =>
                                o.SeasonNumber == e.SeasonNumber
                                && o.VideoFiles.Any(w =>
                                    w.Folder != null
                                    && w.LastEpisodeNumber != null
                                    && o.EpisodeNumber <= e.EpisodeNumber
                                    && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                )
                            )
                        )
                    ),
                CertificationRating = gt
                    .Tv.CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = gt
                    .Tv.CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);

        return (genreDetail, movies, tvShows);
    }

    public Task<List<Genre>> GetGenres(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        return context
            .Genres.AsNoTracking()
            .Where(predicate: genre =>
                genre.GenreMovies.Any(g =>
                    g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.GenreTvShows.Any(g =>
                    g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Include(navigationPropertyPath: genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: genre =>
                genre.GenreMovies.Where(gm =>
                    gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Include(navigationPropertyPath: genre =>
                genre.GenreTvShows.Where(gt =>
                    gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<GenreWithCountsDto>> GetGenresWithCountsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        // Two steps on purpose: computing the four counts as correlated subqueries
        // inside one projection makes SQLite scan the join tables once per genre.
        // Instead, page the genres first, then aggregate their counts with grouped
        // queries. Do not fold this back into a single projection.
        var genres = await context
            .Genres.AsNoTracking()
            .Where(predicate: genre =>
                genre.GenreMovies.Any(g =>
                    g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.GenreTvShows.Any(g =>
                    g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: genre => new
            {
                genre.Id,
                genre.Name,
                TranslatedName = genre
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);

        if (genres.Count == 0)
            return [];

        List<int> ids = genres.Select(selector: genre => genre.Id).ToList();

        Dictionary<int, int> movieTotals = await context
            .GenreMovie.AsNoTracking()
            .Where(predicate: gm =>
                ids.Contains(gm.GenreId)
                && gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(keySelector: gm => gm.GenreId)
            .Select(selector: group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(keySelector: x => x.Key, elementSelector: x => x.Count, cancellationToken: ct);

        Dictionary<int, int> movieWithVideo = await context
            .GenreMovie.AsNoTracking()
            .Where(predicate: gm =>
                ids.Contains(gm.GenreId)
                && gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                && gm.Movie.VideoFiles.Any(v => v.Folder != null)
            )
            .GroupBy(keySelector: gm => gm.GenreId)
            .Select(selector: group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(keySelector: x => x.Key, elementSelector: x => x.Count, cancellationToken: ct);

        Dictionary<int, int> tvTotals = await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt =>
                ids.Contains(gt.GenreId) && gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(keySelector: gt => gt.GenreId)
            .Select(selector: group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(keySelector: x => x.Key, elementSelector: x => x.Count, cancellationToken: ct);

        // A multi-episode video file lives on its own episode, which therefore already
        // passes the direct "has a video file" test. The old nested self-join over
        // LastEpisodeNumber never changed this TV-level count, so it is dropped.
        Dictionary<int, int> tvWithVideo = await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt =>
                ids.Contains(gt.GenreId)
                && gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                && gt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null))
            )
            .GroupBy(keySelector: gt => gt.GenreId)
            .Select(selector: group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(keySelector: x => x.Key, elementSelector: x => x.Count, cancellationToken: ct);

        return genres
            .Select(selector: genre => new GenreWithCountsDto
            {
                Id = genre.Id,
                Name = genre.TranslatedName ?? genre.Name,
                TotalMovies = movieTotals.GetValueOrDefault(key: genre.Id),
                TotalTvShows = tvTotals.GetValueOrDefault(key: genre.Id),
                MoviesWithVideo = movieWithVideo.GetValueOrDefault(key: genre.Id),
                TvShowsWithVideo = tvWithVideo.GetValueOrDefault(key: genre.Id),
            })
            .ToList();
    }

    public Task<List<MusicGenre>> GetMusicGenresAsync(Guid userId, CancellationToken ct = default)
    {
        return context
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre =>
                genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<List<MusicGenreCardDto>> GetMusicGenreCardsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return context
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre =>
                genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Select(selector: genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public Task<List<MusicGenre>> GetPaginatedMusicGenresAsync(
        Guid userId,
        string letter,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        return context
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre =>
                genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .Where(predicate: genre =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => genre.Name.StartsWith(p.ToLower()))
                    : genre.Name.StartsWith(letter.ToLower())
            )
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<List<MusicGenreCardDto>> GetPaginatedMusicGenreCardsAsync(
        Guid userId,
        string letter,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        return context
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre =>
                genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .Where(predicate: genre =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => genre.Name.StartsWith(p.ToLower()))
                    : genre.Name.StartsWith(letter.ToLower())
            )
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public Task<MusicGenre?> GetMusicGenreAsync(
        Guid userId,
        Guid genreId,
        CancellationToken ct = default
    )
    {
        return context
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre =>
                genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: genre => genre.Id == genreId)
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
                .ThenInclude(navigationPropertyPath: mgt => mgt.Track)
                    .ThenInclude(navigationPropertyPath: track => track.TrackUser.Where(tu => tu.UserId == userId))
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
                .ThenInclude(navigationPropertyPath: mgt => mgt.Track)
                    .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                        .ThenInclude(navigationPropertyPath: at => at.Artist)
                            .ThenInclude(navigationPropertyPath: artist => artist.Translations)
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
                .ThenInclude(navigationPropertyPath: mgt => mgt.Track)
                    .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                        .ThenInclude(navigationPropertyPath: at => at.Artist)
                            .ThenInclude(navigationPropertyPath: artist => artist.Images)
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
                .ThenInclude(navigationPropertyPath: mgt => mgt.Track)
                    .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                        .ThenInclude(navigationPropertyPath: at => at.Album)
                            .ThenInclude(navigationPropertyPath: album => album.Translations)
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
                .ThenInclude(navigationPropertyPath: mgt => mgt.Track)
                    .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                        .ThenInclude(navigationPropertyPath: at => at.Album)
                            .ThenInclude(navigationPropertyPath: album => album.AlbumArtist)
                                .ThenInclude(navigationPropertyPath: aa => aa.Artist)
                                    .ThenInclude(navigationPropertyPath: artist => artist.Images)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }
}
