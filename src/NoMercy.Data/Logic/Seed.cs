using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Logic.Seeds;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Certifications;
using Serilog.Events;
using Certification = NoMercy.Database.Models.Certification;
using File = System.IO.File;

namespace NoMercy.Data.Logic;

public class Seed : IDisposable, IAsyncDisposable
{
    private static TmdbConfigClient TmdbConfigClient { get; set; } = new();
    private static TmdbMovieClient TmdbMovieClient { get; set; } = new();
    private static TmdbTvClient TmdbTvClient { get; set; } = new();
    private static readonly MediaContext MediaContext = new();
    private static readonly QueueContext QueueContext = new();
    private static Folder[] _folders = [];
    private static User[] _users = [];
    private static bool ShouldSeedMarvel { get; set; }
    private static Language[] _languages = [];

    public static async Task Init(bool shouldSeedMarvel)
    {
        ShouldSeedMarvel = shouldSeedMarvel;
        await CreateDatabase();
        await SeedDatabase();
    }

    private static async Task CreateDatabase()
    {
        await EnsureDatabaseCreated(MediaContext);
        await EnsureDatabaseCreated(QueueContext);
    }

    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }

    private static async Task SeedDatabase()
    {
        try
        {
            await AddLanguages();
            await AddCountries();
            await AddGenres();
            await AddCertifications();
            await AddMusicGenres();
            await AddFolderRoots();
            await AddEncoderProfiles();
            await AddLibraries();
            await Users();

            if (ShouldSeedMarvel)
            {
                Thread thread = new(() => _ = SpecialSeed.AddSpecial(MediaContext));
                thread.Start();
            }
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }

    private static async Task Users()
    {
        Logger.Setup("Adding Users");

        HttpClient client = new();
        client.BaseAddress = new(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Auth.AccessToken);

        IDictionary<string, string?> query = new Dictionary<string, string?>();
        query.Add("server_id", Info.DeviceId.ToString());

        string newUrl = QueryHelpers.AddQueryString("users", query);

        HttpResponseMessage response = await client.GetAsync(newUrl);
        string? content = await response.Content.ReadAsStringAsync();

        if (content == null) throw new("Failed to get Server info");

        ServerUserDto[] serverUsers = content.FromJson<ServerUserDto[]>() ?? [];

        Logger.Setup($"Found {serverUsers.Length} users");

        _users = serverUsers.Select(serverUser => new User
            {
                Id = Guid.Parse(serverUser.UserId),
                Email = serverUser.Email,
                Name = serverUser.Name,
                Allowed = true,
                Manage = serverUser.Enabled,
                AudioTranscoding = serverUser.Enabled,
                NoTranscoding = serverUser.Enabled,
                VideoTranscoding = serverUser.Enabled,
                Owner = serverUser.IsOwner,
                UpdatedAt = DateTime.Now
            })
            .ToArray();

        await MediaContext.Users
            .UpsertRange(_users)
            .On(v => new { v.Id })
            .WhenMatched((us, ui) => new()
            {
                Id = ui.Id,
                Email = ui.Email,
                Name = ui.Name,
                Allowed = ui.Allowed,
                Manage = ui.Manage,
                AudioTranscoding = ui.AudioTranscoding,
                NoTranscoding = ui.NoTranscoding,
                VideoTranscoding = ui.VideoTranscoding,
                Owner = ui.Owner,
                UpdatedAt = ui.UpdatedAt
            })
            .RunAsync();

        if (!File.Exists(AppFiles.LibrariesSeedFile)) return;

        Library[] libraries = File.ReadAllTextAsync(AppFiles.LibrariesSeedFile)
            .Result.FromJson<Library[]>() ?? [];

        List<LibraryUser> libraryUsers = [];

        foreach (User user in _users.ToList())
        foreach (Library library in libraries.ToList())
            libraryUsers.Add(new()
            {
                LibraryId = library.Id,
                UserId = user.Id
            });

        await MediaContext.LibraryUser
            .UpsertRange(libraryUsers)
            .On(v => new { v.LibraryId, v.UserId })
            .WhenMatched((lus, lui) => new()
            {
                LibraryId = lui.LibraryId,
                UserId = lui.UserId
            })
            .RunAsync();
    }

    private static async Task AddGenres()
    {
        bool hasGenres = await MediaContext.Genres.AnyAsync();
        if (hasGenres) return;

        Logger.Setup("Adding Genres");

        List<Genre> genres = [];
        List<Genre>? movieGenres = (await TmdbMovieClient.Genres())?
            .Genres.Select(genre => new Genre
            {
                Id = genre.Id,
                Name = genre.Name ?? string.Empty,
            })
            .ToList();
        genres.AddRange(movieGenres ?? []);

        List<Genre>? tvGenres = (await TmdbTvClient.Genres())?
            .Genres.Select(genre => new Genre
            {
                Id = genre.Id,
                Name = genre.Name ?? string.Empty,
            })
            .ToList();
        genres.AddRange(tvGenres ?? []);

        await MediaContext.Genres.UpsertRange(genres)
            .On(v => new { v.Id })
            .WhenMatched(v => new()
            {
                Id = v.Id,
                Name = v.Name
            })
            .RunAsync();

        List<Translation> translations = [];

        await Parallel.ForEachAsync(_languages.Where(g => g.Iso6391 != "en"), async (language, _) =>
        {
            Logger.Setup($"Adding Genres for {language.Name}");

            List<Translation>? mg = (await TmdbMovieClient.Genres(language.Iso6391))?.Genres
                .Where(g => g.Name != null)
                .Select(genre => new Translation
                {
                    GenreId = genre.Id,
                    Name = genre.Name ?? string.Empty,
                    Iso6391 = language.Iso6391,
                })
                .ToList();

            translations.AddRange(mg ?? []);

            List<Translation>? tg = (await TmdbTvClient.Genres(language.Iso6391))?.Genres
                .Where(g => g.Name != null)
                .Select(genre => new Translation
                {
                    GenreId = genre.Id,
                    Name = genre.Name ?? string.Empty,
                    Iso6391 = language.Iso6391,
                })
                .ToList();
            
            translations.AddRange(tg ?? []);
        });

        Logger.Setup($"Adding {translations.Count} genre translations");

        await MediaContext.Translations.UpsertRange(translations.Where(genre => genre.Name != null))
            .On(v => new { v.GenreId, v.Iso6391 })
            .WhenMatched(v => new()
            {
                GenreId = v.GenreId,
                Name = v.Name,
                Iso6391 = v.Iso6391
            })
            .RunAsync();
    }

    private static async Task AddCertifications()
    {
        bool hasCertifications = await MediaContext.Certifications.AnyAsync();
        if (hasCertifications) return;

        Logger.Setup("Adding Certifications");

        List<Certification> certifications = [];

        foreach ((string key, TmdbMovieCertification[] value) in (await TmdbMovieClient.Certifications())
                 ?.Certifications ?? [])
        foreach (TmdbMovieCertification certification in value)
            certifications.Add(new()
            {
                Iso31661 = key,
                Rating = certification.Rating,
                Meaning = certification.Meaning,
                Order = certification.Order
            });

        foreach ((string key, TmdbTvShowCertification[] value) in (await TmdbTvClient.Certifications())
                 ?.Certifications ?? [])
        foreach (TmdbTvShowCertification certification in value)
            certifications.Add(new()
            {
                Iso31661 = key,
                Rating = certification.Rating,
                Meaning = certification.Meaning,
                Order = certification.Order
            });

        await MediaContext.Certifications.UpsertRange(certifications)
            .On(v => new { v.Iso31661, v.Rating })
            .WhenMatched(v => new()
            {
                Iso31661 = v.Iso31661,
                Rating = v.Rating,
                Meaning = v.Meaning,
                Order = v.Order
            })
            .RunAsync();
    }

    private static async Task AddLanguages()
    {
        bool hasLanguages = await MediaContext.Languages.AnyAsync();
        if (hasLanguages) return;

        Logger.Setup("Adding Languages");

        _languages = (await TmdbConfigClient.Languages())?.ToList()
            .ConvertAll<Language>(language => new()
            {
                Iso6391 = language.Iso6391,
                EnglishName = language.EnglishName,
                Name = language.Name
            }).ToArray() ?? [];

        await MediaContext.Languages.UpsertRange(_languages)
            .On(v => new { v.Iso6391 })
            .WhenMatched(v => new()
            {
                Iso6391 = v.Iso6391,
                Name = v.Name,
                EnglishName = v.EnglishName
            })
            .RunAsync();
    }

    private static async Task AddCountries()
    {
        bool hasCountries = await MediaContext.Countries.AnyAsync();
        if (hasCountries) return;

        Logger.Setup("Adding Countries");

        Country[] countries = (await TmdbConfigClient.Countries())?.ToList()
            .ConvertAll<Country>(country => new()
            {
                Iso31661 = country.Iso31661,
                EnglishName = country.EnglishName,
                NativeName = country.NativeName
            }).ToArray() ?? [];

        await MediaContext.Countries.UpsertRange(countries)
            .On(v => new { v.Iso31661 })
            .WhenMatched(v => new()
            {
                Iso31661 = v.Iso31661,
                NativeName = v.NativeName,
                EnglishName = v.EnglishName
            })
            .RunAsync();
    }

    private static async Task AddMusicGenres()
    {
        bool hasMusicGenres = await MediaContext.MusicGenres.AnyAsync();
        if (hasMusicGenres) return;

        Logger.Setup("Adding Music Genres");

        MusicBrainzGenreClient musicBrainzGenreClient = new();

        MusicGenre[] genres = (await musicBrainzGenreClient.All()).ToList()
            .ConvertAll<MusicGenre>(genre => new()
            {
                Id = genre.Id,
                Name = genre.Name
            }).ToArray();

        await MediaContext.MusicGenres.UpsertRange(genres)
            .On(v => new { v.Id })
            .WhenMatched(v => new()
            {
                Id = v.Id,
                Name = v.Name
            })
            .RunAsync();

        await Task.CompletedTask;
    }

    private static async Task AddEncoderProfiles()
    {
        Logger.Setup("Adding Encoder Profiles");

        List<EncoderProfile> encoderProfiles;
        if (File.Exists(AppFiles.EncoderProfilesSeedFile))
        {
            encoderProfiles = File.ReadAllTextAsync(AppFiles.EncoderProfilesSeedFile).Result
                .FromJson<List<EncoderProfile>>()!;
        }
        else
        {
            encoderProfiles = EncoderProfileSeedData.GetEncoderProfiles();
        }

        await File.WriteAllTextAsync(AppFiles.EncoderProfilesSeedFile, encoderProfiles.ToJson());

        await MediaContext.EncoderProfiles.UpsertRange(encoderProfiles)
            .On(v => new { v.Id })
            .WhenMatched((vs, vi) => new()
            {
                Id = vi.Id,
                Name = vi.Name,
                Container = vi.Container,
                Param = vi.Param,
                _videoProfiles = vi._videoProfiles,
                _audioProfiles = vi._audioProfiles,
                _subtitleProfiles = vi._subtitleProfiles,
                UpdatedAt = vi.UpdatedAt,
            })
            .RunAsync();

        List<EncoderProfileFolder> encoderProfileFolders = [];
        foreach (EncoderProfile? encoderProfile in encoderProfiles)
        {
            encoderProfileFolders.AddRange(encoderProfile.EncoderProfileFolder.ToList()
                .Select(encoderProfileFolder => new EncoderProfileFolder
                {
                    EncoderProfileId = encoderProfile.Id,
                    FolderId = encoderProfileFolder.FolderId
                }));
        }

        await MediaContext.EncoderProfileFolder
            .UpsertRange(encoderProfileFolders)
            .On(v => new { v.FolderId, v.EncoderProfileId })
            .WhenMatched((vs, vi) => new()
            {
                FolderId = vi.FolderId,
                EncoderProfileId = vi.EncoderProfileId
            })
            .RunAsync();
    }

    private static async Task AddFolderRoots()
    {
        try
        {
            if (!File.Exists(AppFiles.FolderRootsSeedFile)) return;

            Logger.Setup("Adding Folder Roots");

            _folders = File.ReadAllTextAsync(AppFiles.FolderRootsSeedFile)
                .Result.FromJson<Folder[]>() ?? [];

            await MediaContext.Folders.UpsertRange(_folders)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new()
                {
                    Id = vi.Id,
                    Path = vi.Path
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }

    private static async Task AddLibraries()
    {
        try
        {
            if (!File.Exists(AppFiles.LibrariesSeedFile)) return;

            Logger.Setup("Adding Libraries");

            LibrarySeedDto[] librarySeed = File.ReadAllTextAsync(AppFiles.LibrariesSeedFile)
                .Result.FromJson<LibrarySeedDto[]>() ?? [];

            List<Library> libraries = librarySeed.Select(librarySeedDto => new Library()
            {
                Id = librarySeedDto.Id,
                AutoRefreshInterval = librarySeedDto.AutoRefreshInterval,
                ChapterImages = librarySeedDto.ChapterImages,
                ExtractChapters = librarySeedDto.ExtractChapters,
                ExtractChaptersDuring = librarySeedDto.ExtractChaptersDuring,
                Image = librarySeedDto.Image,
                PerfectSubtitleMatch = librarySeedDto.PerfectSubtitleMatch,
                Realtime = librarySeedDto.Realtime,
                SpecialSeasonName = librarySeedDto.SpecialSeasonName,
                Title = librarySeedDto.Title,
                Type = librarySeedDto.Type,
                Order = librarySeedDto.Order
            }).ToList();

            await MediaContext.Libraries.UpsertRange(libraries)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new()
                {
                    Id = vi.Id,
                    AutoRefreshInterval = vi.AutoRefreshInterval,
                    ChapterImages = vi.ChapterImages,
                    ExtractChapters = vi.ExtractChapters,
                    ExtractChaptersDuring = vi.ExtractChaptersDuring,
                    Image = vi.Image,
                    PerfectSubtitleMatch = vi.PerfectSubtitleMatch,
                    Realtime = vi.Realtime,
                    SpecialSeasonName = vi.SpecialSeasonName,
                    Title = vi.Title,
                    Type = vi.Type,
                    Order = vi.Order
                })
                .RunAsync();

            List<FolderLibrary> libraryFolders = [];

            foreach (LibrarySeedDto library in librarySeed.ToList())
            foreach (FolderDto folder in library.Folders.ToList())
                libraryFolders.Add(new(folder.Id, library.Id));

            await MediaContext.FolderLibrary
                .UpsertRange(libraryFolders)
                .On(v => new { v.FolderId, v.LibraryId })
                .WhenMatched((vs, vi) => new()
                {
                    FolderId = vi.FolderId,
                    LibraryId = vi.LibraryId
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }

    public ValueTask DisposeAsync()
    {
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
        return ValueTask.CompletedTask;
    }
}
