using System.Globalization;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.MediaProcessing.Movies;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Collections;

public class CollectionManager(
    ICollectionRepository collectionRepository,
    MovieManager movieManager,
    JobDispatcher jobDispatcher
) : BaseManager, ICollectionManager
{
    public async Task<TmdbCollectionAppends?> Add(int id, Library library)
    {
        TmdbCollectionClient collectionClient = new(id);
        TmdbCollectionAppends? collectionAppends = await collectionClient.WithAllAppends();

        if (collectionAppends is null) return null;

        string colorPalette = await MovieDbImageManager
            .MultiColorPalette([
                new("poster", collectionAppends.PosterPath),
                new("backdrop", collectionAppends.BackdropPath)
            ]);

        Collection collection = new()
        {
            Id = collectionAppends.Id,
            Title = collectionAppends.Name,
            TitleSort = collectionAppends.Name
                .TitleSort(collectionAppends.Parts
                    .MinBy(movie => movie.ReleaseDate)?.ReleaseDate),
            Backdrop = collectionAppends.BackdropPath,
            Poster = collectionAppends.PosterPath,
            Overview = collectionAppends.Overview,
            Parts = collectionAppends.Parts.Length,
            _colorPalette = colorPalette,

            LibraryId = library.Id,
        };

        await collectionRepository.Store(collection);

        Logger.MovieDb($"Collection: {collection.Title}: Added to Database", LogEventLevel.Debug);

        await StoreTranslations(collectionAppends);

        jobDispatcher.DispatchJob<AddCollectionExtraDataJob, TmdbCollectionAppends>(collectionAppends);

        Logger.MovieDb($"Collection: {collectionAppends.Name}: Added to Library {library.Title}", LogEventLevel.Debug);

        return collectionAppends;
    }

    public Task UpdateCollectionAsync(int id, Library library)
    {
        throw new NotImplementedException();
    }

    public Task RemoveCollectionAsync(int id, Library library)
    {
        throw new NotImplementedException();
    }

    private async Task StoreTranslations(TmdbCollectionAppends collection)
    {
        IEnumerable<Translation> translations = collection.Translations.Translations
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                CollectionId = collection.Id
            });

        await collectionRepository.StoreTranslations(translations);

        Logger.MovieDb($"Collection: {collection.Name}: Translations stored", LogEventLevel.Debug);
    }

    internal async Task StoreImages(TmdbCollectionAppends collection)
    {
        IEnumerable<Image> posters = collection.Images.Posters
            .Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                CollectionId = collection.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await collectionRepository.StoreImages(posters);
        Logger.MovieDb($"Movie: {collection.Name}: Posters stored", LogEventLevel.Debug);

        IEnumerable<Image> posterJobItems = posters
            .Select(x => new Image { FilePath = x.FilePath })
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .ToArray();
        if (posterJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(collection.Id, posterJobItems);

        IEnumerable<Image> backdrops = collection.Images.Backdrops
            .Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                CollectionId = collection.Id,
                Type = "backdrop",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await collectionRepository.StoreImages(backdrops);
        Logger.MovieDb($"Collection: {collection.Name}: backdrops stored", LogEventLevel.Debug);

        IEnumerable<Image> backdropJobItems = backdrops
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (backdropJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(collection.Id, backdropJobItems);

        IEnumerable<Image> logos = collection.Images.Logos.Select(
                image => new Image
                {
                    AspectRatio = image.AspectRatio,
                    FilePath = image.FilePath,
                    Height = image.Height,
                    Iso6391 = image.Iso6391,
                    VoteAverage = image.VoteAverage,
                    VoteCount = image.VoteCount,
                    Width = image.Width,
                    CollectionId = collection.Id,
                    Type = "logo",
                    Site = "https://image.tmdb.org/t/p/"
                })
            .ToArray();

        await collectionRepository.StoreImages(logos);
        Logger.MovieDb($"Collection: {collection.Name}: Logos stored", LogEventLevel.Debug);

        IEnumerable<Image> logosJobItems = logos
            .Where(x => !x.FilePath.EndsWith(".svg"))
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (logosJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(collection.Id, logosJobItems);
    }

    public async Task AddCollectionMovies(TmdbCollectionAppends collectionAppends, Library library)
    {
        List<TmdbMovieAppends> movies = [];

        await Parallel.ForEachAsync(collectionAppends.Parts, async (movie, _) =>
        {
            TmdbMovieClient movieClient = new(movie.Id);
            TmdbMovieAppends? movieAppends = await movieClient.WithAllAppends();
            if (movieAppends is null) return;

            movies.Add(movieAppends);
        });

        foreach (TmdbMovieAppends movie in movies)
        {
            await movieManager.Add(movie.Id, library);
        }

        await collectionRepository.LinkToMovies(collectionAppends);

        Logger.MovieDb($"Collection: {collectionAppends.Name}: Movies added", LogEventLevel.Debug);
    }
}
