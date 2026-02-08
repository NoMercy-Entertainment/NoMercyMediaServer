using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Tests.Repositories.Infrastructure;

public static class TestMediaContextFactory
{
    public static MediaContext CreateContext(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        SqliteConnection connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        connection.Open();

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;

        TestMediaContext context = new(options);
        context.Database.EnsureCreated();

        return context;
    }

    public static MediaContext CreateSeededContext()
    {
        MediaContext context = CreateContext();
        SeedData(context);
        return context;
    }

    public static void SeedData(MediaContext context)
    {
        User testUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "test@nomercy.tv",
            Name = "Test User",
            Owner = true,
            Allowed = true,
            Manage = true
        };
        context.Users.Add(testUser);

        Library movieLibrary = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Movies",
            Type = "movie",
            Order = 1
        };
        context.Libraries.Add(movieLibrary);

        Library tvLibrary = new()
        {
            Id = SeedConstants.TvLibraryId,
            Title = "TV Shows",
            Type = "tv",
            Order = 2
        };
        context.Libraries.Add(tvLibrary);

        context.LibraryUser.Add(new LibraryUser(SeedConstants.MovieLibraryId, SeedConstants.UserId));
        context.LibraryUser.Add(new LibraryUser(SeedConstants.TvLibraryId, SeedConstants.UserId));

        Folder movieFolder = new()
        {
            Id = SeedConstants.MovieFolderId,
            Path = "/media/movies"
        };
        context.Folders.Add(movieFolder);
        context.FolderLibrary.Add(new FolderLibrary(SeedConstants.MovieFolderId, SeedConstants.MovieLibraryId));

        Genre actionGenre = new() { Id = 28, Name = "Action" };
        Genre dramaGenre = new() { Id = 18, Name = "Drama" };
        context.Genres.AddRange(actionGenre, dramaGenre);

        Movie movie1 = new()
        {
            Id = 550,
            Title = "Fight Club",
            TitleSort = "fight club",
            Overview = "An insomniac office worker and a devil-may-care soap maker form an underground fight club.",
            Poster = "/pB8BM7pdSp6B6Ih7QZ4DrQ3PmJK.jpg",
            Backdrop = "/hZkgoQYus5dXo3H8T7Uef6DNknx.jpg",
            ReleaseDate = new DateTime(1999, 10, 15),
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.4
        };

        Movie movie2 = new()
        {
            Id = 680,
            Title = "Pulp Fiction",
            TitleSort = "pulp fiction",
            Overview = "The lives of two mob hitmen, a boxer, a gangster and his wife intertwine in four tales of violence and redemption.",
            Poster = "/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg",
            Backdrop = "/suaEOtk1N1sgg2MTM7oZd2cfVp3.jpg",
            ReleaseDate = new DateTime(1994, 9, 10),
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.5
        };
        context.Movies.AddRange(movie1, movie2);

        context.LibraryMovie.AddRange(
            new LibraryMovie(SeedConstants.MovieLibraryId, 550),
            new LibraryMovie(SeedConstants.MovieLibraryId, 680));

        VideoFile movieVideoFile1 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Fight.Club.1999.1080p.mkv",
            Folder = "/media/movies/Fight Club (1999)",
            HostFolder = "/media/movies/Fight Club (1999)",
            Languages = "en",
            Quality = "1080p",
            Share = "movies",
            MovieId = 550
        };
        VideoFile movieVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Pulp.Fiction.1994.1080p.mkv",
            Folder = "/media/movies/Pulp Fiction (1994)",
            HostFolder = "/media/movies/Pulp Fiction (1994)",
            Languages = "en",
            Quality = "1080p",
            Share = "movies",
            MovieId = 680
        };
        context.VideoFiles.AddRange(movieVideoFile1, movieVideoFile2);

        context.GenreMovie.AddRange(
            new GenreMovie { GenreId = 28, MovieId = 550 },
            new GenreMovie { GenreId = 18, MovieId = 550 },
            new GenreMovie { GenreId = 18, MovieId = 680 });

        Tv show1 = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            Overview = "A chemistry teacher diagnosed with lung cancer teams up with a former student to cook and sell crystal meth.",
            Poster = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
            Backdrop = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
            FirstAirDate = new DateTime(2008, 1, 20),
            NumberOfEpisodes = 62,
            NumberOfSeasons = 5,
            LibraryId = SeedConstants.TvLibraryId,
            VoteAverage = 8.9
        };
        context.Tvs.Add(show1);

        context.LibraryTv.Add(new LibraryTv(SeedConstants.TvLibraryId, 1399));

        Season season1 = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            EpisodeCount = 7,
            TvId = 1399
        };
        context.Seasons.Add(season1);

        Episode episode1 = new()
        {
            Id = 62085,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "Walter White, a struggling high school chemistry teacher, is diagnosed with advanced lung cancer."
        };
        Episode episode2 = new()
        {
            Id = 62086,
            Title = "Cat's in the Bag...",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "After their decaying RV breaks down, Walt and Jesse are forced to deal with a corpse and a prisoner."
        };
        context.Episodes.AddRange(episode1, episode2);

        VideoFile tvVideoFile1 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Breaking.Bad.S01E01.mkv",
            Folder = "/media/tv/Breaking Bad (2008)/Season 01",
            HostFolder = "/media/tv/Breaking Bad (2008)/Season 01",
            Languages = "en",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62085
        };
        VideoFile tvVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Breaking.Bad.S01E02.mkv",
            Folder = "/media/tv/Breaking Bad (2008)/Season 01",
            HostFolder = "/media/tv/Breaking Bad (2008)/Season 01",
            Languages = "en",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62086
        };
        context.VideoFiles.AddRange(tvVideoFile1, tvVideoFile2);

        context.GenreTv.AddRange(
            new GenreTv { GenreId = 18, TvId = 1399 });

        context.SaveChanges();
    }
}
