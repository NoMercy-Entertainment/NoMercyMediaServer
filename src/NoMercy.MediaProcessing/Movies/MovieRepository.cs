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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Movies;

public class MovieRepository(MediaContext context) : IMovieRepository
{
    public async Task Add(Movie movie)
    {
        await context
            .Movies.Upsert(entity: movie)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Backdrop = ti.Backdrop,
                        Duration = ti.Duration,
                        ReleaseDate = ti.ReleaseDate,
                        Homepage = ti.Homepage,
                        ImdbId = ti.ImdbId,
                        OriginalLanguage = ti.OriginalLanguage,
                        Overview = ti.Overview,
                        Popularity = ti.Popularity,
                        Poster = ti.Poster,
                        Status = ti.Status,
                        Tagline = ti.Tagline,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Trailer = ti.Trailer,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Folder = ti.Folder,
                        LibraryId = ti.LibraryId,
                    }
            )
            .RunAsync();

        await context
            .Movies.Where(predicate: m => m.Id == movie.Id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: t => t.CreatedAt, valueExpression: t => movie.CreatedAt));

        await context.SaveChangesAsync();

        // Link any existing recommendation/similar rows that reference this movie as their target
        await context
            .Recommendations.Where(predicate: r => r.MediaId == movie.Id && r.MovieToId == null)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: r => r.MovieToId, valueExpression: movie.Id));

        await context
            .Similar.Where(predicate: r => r.MediaId == movie.Id && r.MovieToId == null)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: r => r.MovieToId, valueExpression: movie.Id));
    }

    public async Task Remove(int id)
    {
        // SQLite schema uses DeleteBehavior.Restrict globally. Disable FK
        // enforcement on this connection so the movie and all its dependents
        // are removed atomically, mirroring
        // Data.Repositories.MovieRepository.DeleteAsync.
        bool ownsConnection =
            context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (ownsConnection)
            await context.Database.OpenConnectionAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = OFF");
            try
            {
                await context.Movies.Where(predicate: movie => movie.Id == id).ExecuteDeleteAsync();
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = ON");
            }
        }
        finally
        {
            if (ownsConnection)
                await context.Database.CloseConnectionAsync();
        }
    }

    public Task LinkToLibrary(Library library, Movie movie)
    {
        return context
            .LibraryMovie.Upsert(entity: new(libraryId: library.Id, movieId: movie.Id))
            .On(match: v => new { v.LibraryId, v.MovieId })
            .WhenMatched(updater: (lts, lti) => new() { LibraryId = lti.LibraryId, MovieId = lti.MovieId })
            .RunAsync();
    }

    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles)
    {
        return context
            .AlternativeTitles.UpsertRange(entities: alternativeTitles)
            .On(match: a => new { a.Title, a.MovieId })
            .WhenMatched(
                updater: (ats, ati) =>
                    new()
                    {
                        Title = ati.Title,
                        Iso31661 = ati.Iso31661,
                        MovieId = ati.MovieId,
                    }
            )
            .RunAsync();
    }

    public Task StoreTranslations(IEnumerable<Translation> translations)
    {
        return context
            .Translations.UpsertRange(
                entities: translations.Where(predicate: translation =>
                    translation.Title != "" || translation.Overview != ""
                )
            )
            .On(match: t => new
            {
                t.Iso31661,
                t.Iso6391,
                t.MovieId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Iso31661 = ti.Iso31661,
                        Iso6391 = ti.Iso6391,
                        Title = ti.Title,
                        EnglishName = ti.EnglishName,
                        Name = ti.Name,
                        Overview = ti.Overview,
                        Homepage = ti.Homepage,
                        Biography = ti.Biography,
                        MovieId = ti.MovieId,
                        SeasonId = ti.SeasonId,
                        EpisodeId = ti.EpisodeId,
                        CollectionId = ti.CollectionId,
                        PersonId = ti.PersonId,
                    }
            )
            .RunAsync();
    }

    public IEnumerable<CertificationMovie> GetCertificationMovies(
        TmdbMovieAppends movie,
        IEnumerable<CertificationCriteria> certificationCriteria
    )
    {
        return context
            .Certifications.AsEnumerable()
            .Where(predicate: c =>
                certificationCriteria.Any(predicate: cc =>
                    cc.Iso31661 == c.Iso31661 && cc.Certification == c.Rating
                )
            )
            .Select(selector: c => new CertificationMovie { CertificationId = c.Id, MovieId = movie.Id });
    }

    public Task StoreContentRatings(IEnumerable<CertificationMovie> certifications)
    {
        return context
            .CertificationMovie.UpsertRange(entities: certifications)
            .On(match: v => new { v.CertificationId, v.MovieId })
            .WhenMatched(
                updater: (ts, ti) => new() { CertificationId = ti.CertificationId, MovieId = ti.MovieId }
            )
            .RunAsync();
    }

    public Task StoreSimilar(IEnumerable<Similar> similar)
    {
        return context
            .Similar.UpsertRange(entities: similar)
            .On(match: v => new { v.MediaId, v.MovieFromId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        MovieToId = ti.MovieToId,
                        MovieFromId = ti.MovieFromId,
                        Overview = ti.Overview,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Backdrop = ti.Backdrop,
                        Poster = ti.Poster,
                        MediaId = ti.MediaId,
                    }
            )
            .RunAsync();
    }

    public Task StoreRecommendations(IEnumerable<Recommendation> recommendations)
    {
        return context
            .Recommendations.UpsertRange(entities: recommendations)
            .On(match: v => new { v.MediaId, v.MovieFromId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        MovieToId = ti.MovieToId,
                        MovieFromId = ti.MovieFromId,
                        Overview = ti.Overview,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Backdrop = ti.Backdrop,
                        Poster = ti.Poster,
                        MediaId = ti.MediaId,
                    }
            )
            .RunAsync();
    }

    public Task StoreVideos(IEnumerable<Media> videos)
    {
        return context
            .Medias.UpsertRange(entities: videos)
            .On(match: v => new { v.Src, v.MovieId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Src = ti.Src,
                        Iso6391 = ti.Iso6391,
                        Type = ti.Type,
                        MovieId = ti.MovieId,
                        Name = ti.Name,
                        Site = ti.Site,
                        Size = ti.Size,
                    }
            )
            .RunAsync();
    }

    public Task StoreImages(IEnumerable<Image> images)
    {
        return context
            .Images.UpsertRange(entities: images)
            .On(match: v => new { v.FilePath, v.MovieId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        AspectRatio = ti.AspectRatio,
                        FilePath = ti.FilePath,
                        Height = ti.Height,
                        Iso6391 = ti.Iso6391,
                        Site = ti.Site,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Width = ti.Width,
                        Type = ti.Type,
                        MovieId = ti.MovieId,
                    }
            )
            .RunAsync();
    }

    public Task StoreKeywords(IEnumerable<Keyword> keywords)
    {
        return context
            .Keywords.UpsertRange(entities: keywords)
            .On(match: v => new { v.Id })
            .WhenMatched(updater: (ts, ti) => new() { Id = ti.Id, Name = ti.Name })
            .RunAsync();
    }

    public Task LinkKeywordsToMovie(IEnumerable<KeywordMovie> keywordMovies)
    {
        return context
            .KeywordMovie.UpsertRange(entities: keywordMovies)
            .On(match: v => new { v.KeywordId, v.MovieId })
            .WhenMatched(updater: (ts, ti) => new() { KeywordId = ti.KeywordId, MovieId = ti.MovieId })
            .RunAsync();
    }

    public Task StoreGenres(IEnumerable<GenreMovie> genreMovies)
    {
        return context
            .GenreMovie.UpsertRange(entities: genreMovies)
            .On(match: v => new { v.GenreId, v.MovieId })
            .WhenMatched(updater: (ts, ti) => new() { GenreId = ti.GenreId, MovieId = ti.MovieId })
            .RunAsync();
    }

    public Task StoreCompanies(List<Company> companies)
    {
        return context
            .Companies.UpsertRange(entities: companies)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Name = ti.Name,
                        Description = ti.Description,
                        Headquarters = ti.Headquarters,
                        Homepage = ti.Homepage,
                        Logo = ti.Logo,
                        OriginCountry = ti.OriginCountry,
                        ParentCompany = ti.ParentCompany,
                    }
            )
            .RunAsync();
    }

    public Task StoreCompanyMovies(List<CompanyMovie> companyMovies)
    {
        return context
            .CompanyMovie.UpsertRange(entities: companyMovies)
            .On(match: v => new { v.CompanyId, v.MovieId })
            .WhenMatched(updater: (ts, ti) => new() { CompanyId = ti.CompanyId, MovieId = ti.MovieId })
            .RunAsync();
    }

    public Task StoreWatchProviders(List<WatchProvider> watchProviders)
    {
        return context
            .WatchProviders.UpsertRange(entities: watchProviders)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Name = ti.Name,
                        Logo = ti.Logo,
                        DisplayPriority = ti.DisplayPriority,
                    }
            )
            .RunAsync();
    }

    public Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias)
    {
        return context
            .WatchProviderMedia.UpsertRange(entities: watchProviderMedias)
            .On(match: v => new
            {
                v.WatchProviderId,
                v.CountryCode,
                v.ProviderType,
                v.MovieId,
                v.TvId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        WatchProviderId = ti.WatchProviderId,
                        MovieId = ti.MovieId,
                        CountryCode = ti.CountryCode,
                        ProviderType = ti.ProviderType,
                    }
            )
            .RunAsync();
    }
}
