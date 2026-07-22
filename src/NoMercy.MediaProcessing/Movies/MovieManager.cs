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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Movies;

public class MovieManager(
    IMovieRepository movieRepository,
    JobDispatcher jobDispatcher,
    IStorageFactory storageFactory,
    ILogger<MovieManager> logger
) : BaseManager, IMovieManager
{
    public async Task<TmdbMovieAppends?> Add(int id, Library library)
    {
        logger.LogInformation(message: "Movie: {Id}: Adding to Library {Title}", args: [id, library.Title]);

        using TmdbMovieClient movieClient = new(id: id);
        TmdbMovieAppends? movieAppends = await MetadataRetry.FetchAsync(
            fetch: () => movieClient.WithAllAppends(),
            description: $"TMDB movie {id}"
        );

        if (movieAppends == null)
            return null;

        string? title = movieAppends.Title;
        if (string.IsNullOrEmpty(value: title))
        {
            logger.LogWarning(message: "Movie: {Id}: Title is null or empty, skipping.", args: id);
            return null;
        }

        string baseUrl = BaseUrl(title: title, releaseDate: movieAppends.ReleaseDate);

        DateTime folderCreatedAt = DateTime.UtcNow;

        foreach (FolderLibrary folderLibrary in library.FolderLibraries ?? [])
        {
            if (storageFactory == null)
                continue;

            IStorage folderStorage = storageFactory.For(
                folderId: folderLibrary.Folder.Id,
                driverId: folderLibrary.Folder.DriverId,
                subPath: string.Empty
            );
            string folderRoot = FolderRootPath(storage: folderStorage, path: folderLibrary.Folder.Path);
            string folderName = folderStorage.CombinePath(parent: folderRoot, child: baseUrl.Replace(oldValue: "/", newValue: ""));

            if (!folderStorage.Exists(path: folderName))
            {
                string? match = FileNameSanitizer.FindMatchingDirectory(
                    driver: folderStorage.Driver,
                    rootPath: folderRoot,
                    expectedFolderName: baseUrl.Replace(oldValue: "/", newValue: "")
                );
                if (match != null)
                    folderName = match;
            }

            if (!folderStorage.Exists(path: folderName))
                continue;

            folderCreatedAt = folderStorage.Driver.GetCreationTimeUtc(path: folderName);

            if (folderCreatedAt != DateTime.UtcNow)
                break;
        }

        Movie movie = new()
        {
            LibraryId = library.Id,
            Folder = baseUrl,

            Id = movieAppends.Id,
            Title = movieAppends.Title,
            TitleSort = movieAppends.Title.TitleSort(date: movieAppends.ReleaseDate),
            Duration = movieAppends.Runtime,
            Adult = movieAppends.Adult,
            Backdrop = movieAppends.BackdropPath,
            Budget = movieAppends.Budget,
            Homepage = movieAppends.Homepage?.ToString(),
            ImdbId = movieAppends.ImdbId,
            OriginalTitle = movieAppends.OriginalTitle,
            OriginalLanguage = movieAppends.OriginalLanguage,
            Overview = movieAppends.Overview,
            Popularity = movieAppends.Popularity,
            Poster = movieAppends.PosterPath,
            ReleaseDate = movieAppends.ReleaseDate,
            Revenue = movieAppends.Revenue,
            Runtime = movieAppends.Runtime,
            Status = movieAppends.Status,
            Tagline = movieAppends.Tagline,
            Trailer = movieAppends.Video?.ToString(),
            Video = movieAppends.Video?.ToString(),
            VoteAverage = movieAppends.VoteAverage,
            VoteCount = movieAppends.VoteCount,

            CreatedAt = folderCreatedAt,
        };

        await movieRepository.Add(movie: movie);
        logger.LogDebug(message: "Movie: {Title}: Added to Database", args: movie.Title);

        await movieRepository.LinkToLibrary(library: library, movie: movie);
        logger.LogDebug(message: "Movie: {Title}: Linked to Library {Title2}", args: [movie.Title, library.Title]);

        await Task.WhenAll(tasks: [StoreTranslations(movie: movieAppends), StoreGenres(movie: movieAppends), StoreContentRatings(movie: movieAppends)]
        );

        logger.LogInformation(
            message: "Movie: {Title}: Added to Library {Title2}", args: [movieAppends.Title, library.Title]
        );

        jobDispatcher.DispatchColorPaletteJob(entityType: "movie", entityId: movie.Id.ToString());
        jobDispatcher.DispatchJob<MovieExtrasJob, TmdbMovieAppends>(data: movieAppends);

        return movieAppends;
    }

    public Task Update(int id, Library library)
    {
        // Re-importing refreshes all metadata. Every write in Add is an
        // idempotent upsert, so re-running it updates the existing records
        // in place rather than creating duplicates.
        return Add(id: id, library: library);
    }

    public async Task Remove(int id, Library library)
    {
        logger.LogInformation(message: "Movie: {Id}: Removing from Library {Title}", args: [id, library.Title]);
        await movieRepository.Remove(id: id);
        logger.LogDebug(message: "Movie: {Id}: Removed from Database", args: id);
    }

    public async Task StoreAlternativeTitles(TmdbMovieAppends movie)
    {
        IEnumerable<AlternativeTitle> alternativeTitles = (
            movie.AlternativeTitles?.Results ?? []
        ).Select(selector: tmdbMovieAlternativeTitles => new AlternativeTitle
        {
            Iso31661 = tmdbMovieAlternativeTitles.Iso31661,
            Title = tmdbMovieAlternativeTitles.Title,
            MovieId = movie.Id,
        });

        await movieRepository.StoreAlternativeTitles(alternativeTitles: alternativeTitles);

        logger.LogDebug(message: "Movie: {Title}: AlternativeTitles stored", args: movie.Title);
    }

    public async Task StoreTranslations(TmdbMovieAppends movie)
    {
        IEnumerable<Translation> translations = (movie.Translations?.Translations ?? []).Select(
            selector: translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                MovieId = movie.Id,
            }
        );

        await movieRepository.StoreTranslations(translations: translations);

        logger.LogDebug(message: "Movie: {Title}: Translations stored", args: movie.Title);
    }

    public async Task StoreContentRatings(TmdbMovieAppends movie)
    {
        List<CertificationCriteria> certificationCriteria = (movie.ReleaseDates?.Results ?? [])
            .Select(selector: r => new CertificationCriteria
            {
                Iso31661 = r.Iso31661,
                Certification = r.ReleaseDates.FirstOrDefault()?.Certification ?? string.Empty,
            })
            .ToList();

        IEnumerable<CertificationMovie> certificationMovies =
            movieRepository.GetCertificationMovies(movie: movie, certificationCriteria: certificationCriteria);

        await movieRepository.StoreContentRatings(certifications: certificationMovies);

        logger.LogDebug(message: "Movie: {Title}: Content Ratings stored", args: movie.Title);
    }

    public async Task StoreSimilar(TmdbMovieAppends movie)
    {
        IEnumerable<Similar> similar = (movie.Similar?.Results ?? [])
            .Select(selector: tmdbMovie => new Similar
            {
                Backdrop = tmdbMovie.BackdropPath,
                Overview = tmdbMovie.Overview,
                Poster = tmdbMovie.PosterPath,
                Title = tmdbMovie.Title,
                TitleSort = tmdbMovie.Title?.TitleSort(),
                MediaId = tmdbMovie.Id,
                MovieFromId = movie.Id,
            })
            .ToArray();

        await movieRepository.StoreSimilar(similar: similar);

        logger.LogDebug(message: "Movie: {Title}: Similar stored", args: movie.Title);

        await using MediaContext db = new();
        List<int> similarIds = await db
            .Similar.AsNoTracking()
            .Where(predicate: s =>
                s.MovieFromId == movie.Id && (s._colorPalette == null || s._colorPalette == "")
            )
            .Select(selector: s => s.Id)
            .ToListAsync();

        foreach (int id in similarIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "similar", entityId: id.ToString());
    }

    public async Task StoreRecommendations(TmdbMovieAppends movie)
    {
        IEnumerable<Recommendation> recommendations = (movie.Recommendations?.Results ?? [])
            .Select(selector: tmdbMovie => new Recommendation
            {
                Backdrop = tmdbMovie.BackdropPath,
                Overview = tmdbMovie.Overview,
                Poster = tmdbMovie.PosterPath,
                Title = tmdbMovie.Title,
                TitleSort = tmdbMovie.Title?.TitleSort(),
                MediaId = tmdbMovie.Id,
                MovieFromId = movie.Id,
            })
            .ToArray();

        await movieRepository.StoreRecommendations(recommendations: recommendations);

        logger.LogDebug(message: "Movie: {Title}: Recommendations stored", args: movie.Title);

        await using MediaContext db = new();
        List<int> recommendationIds = await db
            .Recommendations.AsNoTracking()
            .Where(predicate: r =>
                r.MovieFromId == movie.Id && (r._colorPalette == null || r._colorPalette == "")
            )
            .Select(selector: r => r.Id)
            .ToListAsync();

        foreach (int id in recommendationIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "recommendation", entityId: id.ToString());
    }

    public async Task StoreVideos(TmdbMovieAppends movie)
    {
        IEnumerable<Media> videos = (movie.Videos?.Results ?? []).Select(selector: media => new Media
        {
            Id = Ulid.NewUlid(),
            Iso6391 = media.Iso6391,
            Name = media.Name,
            Site = media.Site,
            Size = media.Size,
            Src = media.Key,
            Type = media.Type,
            MovieId = movie.Id,
        });

        await movieRepository.StoreVideos(videos: videos);
        logger.LogDebug(message: "Movie: {Title}: Videos stored", args: movie.Title);
    }

    public async Task StoreImages(TmdbMovieAppends movie)
    {
        IEnumerable<Image> posters = (movie.Images?.Posters ?? [])
            .Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await movieRepository.StoreImages(images: posters);
        logger.LogDebug(message: "Movie: {Title}: Posters stored", args: movie.Title);

        IEnumerable<Image> backdrops = (movie.Images?.Backdrops ?? [])
            .Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "backdrop",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await movieRepository.StoreImages(images: backdrops);
        logger.LogDebug(message: "Movie: {Title}: backdrops stored", args: movie.Title);

        IEnumerable<Image> logos = (movie.Images?.Logos ?? [])
            .Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "logo",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await movieRepository.StoreImages(images: logos);
        logger.LogDebug(message: "Movie: {Title}: Logos stored", args: movie.Title);

        await using MediaContext db = new();
        List<int> imageIds = await db
            .Images.AsNoTracking()
            .Where(predicate: i => i.MovieId == movie.Id && (i._colorPalette == null || i._colorPalette == ""))
            .Select(selector: i => i.Id)
            .ToListAsync();

        foreach (int id in imageIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "image", entityId: id.ToString());
    }

    public async Task StoreKeywords(TmdbMovieAppends movie)
    {
        IEnumerable<Keyword> keywords = (movie.Keywords?.Results ?? []).Select(
            selector: keyword => new Keyword { Id = keyword.Id, Name = keyword.Name }
        );

        await movieRepository.StoreKeywords(keywords: keywords);
        logger.LogDebug(message: "Movie: {Title}: Keywords stored", args: movie.Title);

        IEnumerable<KeywordMovie> keywordMovies = (movie.Keywords?.Results ?? []).Select(
            selector: keyword => new KeywordMovie { KeywordId = keyword.Id, MovieId = movie.Id }
        );

        await movieRepository.LinkKeywordsToMovie(keywordMovies: keywordMovies);
        logger.LogDebug(message: "Movie: {Title}: Keywords linked to Movie", args: movie.Title);
    }

    public async Task StoreGenres(TmdbMovieAppends movie)
    {
        IEnumerable<GenreMovie> genreMovies = (movie.Genres ?? []).Select(selector: genre => new GenreMovie
        {
            GenreId = genre.Id,
            MovieId = movie.Id,
        });

        await movieRepository.StoreGenres(genreMovies: genreMovies);
        logger.LogDebug(message: "Movie: {Title}: Genres stored", args: movie.Title);
    }

    public async Task StoreWatchProviders(TmdbMovieAppends movie)
    {
        List<WatchProvider> watchProviders = [];
        List<WatchProviderMedia> watchProviderMedias = [];

        foreach (
            (
                string countryCode,
                string providerType,
                TmdbPaymentDetails provider,
                string? link
            ) in TmdbWatchProviders.ExtractProviders(
                results: movie.WatchProviders?.TmdbWatchProviderResults ?? new()
            )
        )
        {
            if (watchProviders.All(predicate: wp => wp.Id != provider.ProviderId))
            {
                watchProviders.Add(
                    item: new()
                    {
                        Id = provider.ProviderId,
                        Name = provider.ProviderName,
                        Logo = provider.LogoPath,
                        DisplayPriority = provider.DisplayPriority,
                    }
                );
            }

            watchProviderMedias.Add(
                item: new()
                {
                    WatchProviderId = provider.ProviderId,
                    MovieId = movie.Id,
                    CountryCode = countryCode,
                    ProviderType = providerType,
                    Link = link,
                }
            );
        }

        if (watchProviders.Count != 0)
            await movieRepository.StoreWatchProviders(watchProviders: watchProviders);

        if (watchProviderMedias.Count != 0)
            await movieRepository.StoreWatchProviderMedias(watchProviderMedias: watchProviderMedias);

        logger.LogDebug(message: "Show {Title}: WatchProviders stored", args: movie.Title);
    }

    public async Task StoreCompanies(TmdbMovieAppends movie)
    {
        if (movie.ProductionCompanies == null || movie.ProductionCompanies.Length == 0)
        {
            logger.LogDebug(message: "Movie: {Title}: No production companies found", args: movie.Title);
            return;
        }

        TmdbMovieClient movieClient = new(id: movie.Id);

        ConcurrentDictionary<int, Company> companiesDict = new();

        await Parallel.ForEachAsync(
            source: movie.ProductionCompanies,
            parallelOptions: SystemParallelism.Options,
            body: async (productionCompany, _) =>
            {
                TmdbTmdbNetworkDetails? nw = await movieClient.CompanyDetails(id: productionCompany.Id);
                if (nw == null)
                    return;

                companiesDict.TryAdd(
                    key: nw.Id,
                    value: new()
                    {
                        Id = nw.Id,
                        Name = nw.Name,
                        Logo = nw.LogoPath,
                        OriginCountry = nw.OriginCountry,
                        Headquarters = nw.Headquarters,
                        Homepage = nw.Homepage,
                    }
                );
            }
        );

        List<Company> companies = companiesDict.Values.ToList();

        List<CompanyMovie> companyMovies = companies
            .Select(selector: company => new CompanyMovie { CompanyId = company.Id, MovieId = movie.Id })
            .ToList();

        if (companies.Count != 0)
            await movieRepository.StoreCompanies(companies: companies);

        if (companyMovies.Count != 0)
            await movieRepository.StoreCompanyMovies(companyMovies: companyMovies);

        logger.LogDebug(message: "Movie: {Title}: Companies stored", args: movie.Title);
    }
}
