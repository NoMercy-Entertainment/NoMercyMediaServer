using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MovieFileLibrary;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class FileRepository(MediaContext context) : IFileRepository
{
    private readonly MediaContext _context = context;

    public Task StoreVideoFile(VideoFile videoFile)
    {
        return _context.VideoFiles.Upsert(videoFile)
            .On(vf => vf.Filename)
            .WhenMatched((vs, vi) => new()
            {
                EpisodeId = vi.EpisodeId,
                MovieId = vi.MovieId,
                Folder = vi.Folder,
                HostFolder = vi.HostFolder,
                Filename = vi.Filename,
                Share = vi.Share,
                Duration = vi.Duration,
                Chapters = vi.Chapters,
                Languages = vi.Languages,
                Quality = vi.Quality,
                Subtitles = vi.Subtitles,
                _tracks = vi._tracks,
                MetadataId = vi.MetadataId
            })
            .RunAsync();
    }

    public async Task<Ulid> StoreMetadata(Metadata metadata)
    {
        await _context.Metadata.Upsert(metadata)
            .On(mf => new { mf.Filename, mf.HostFolder })
            .WhenMatched((ms, mi) => new()
            {
                AudioTrackId = mi.AudioTrackId,
                Duration = mi.Duration,
                Filename = mi.Filename,
                Folder = mi.Folder,
                FolderSize = mi.FolderSize,
                HostFolder = mi.HostFolder,
                Type = mi.Type,
                _audio = mi._audio,
                _chapters = mi._chapters,
                _chapters_file = mi._chapters_file,
                _fonts = mi._fonts,
                _fonts_file = mi._fonts_file,
                _previews = mi._previews,
                _subtitles = mi._subtitles,
                _video = mi._video
            })
            .RunAsync();

        return _context.Metadata
            .Where(m => m.Filename == metadata.Filename)
            .Where(m => m.HostFolder == metadata.HostFolder)
            .Select(m => m.Id)
            .FirstOrDefault();
    }

    public async Task<Episode?> GetEpisode(int? showId, MediaFile item)
    {
        if (item.Parsed == null) return null;

        return await _context.Episodes
            .Where(e => e.TvId == showId)
            .Where(e => e.SeasonNumber == item.Parsed!.Season)
            .Where(e => e.EpisodeNumber == item.Parsed!.Episode)
            .FirstOrDefaultAsync();
    }

    public async Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library)
    {
        Movie? movie = null;
        Tv? show = null;
        string type = "";

        switch (library.Type)
        {
            case Config.MovieMediaType:
                movie = await _context.Movies
                    .Where(m => m.Id == id)
                    .FirstOrDefaultAsync();
                type = library.Type;
                break;
            case Config.TvMediaType:
            case Config.AnimeMediaType:
                show = await _context.Tvs
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (show == null)
                {
                    Episode? episode = await _context.Episodes
                        .Where(e => e.Id == id)
                        .FirstOrDefaultAsync();

                    if (episode != null)
                    {
                        show = await _context.Tvs
                            .Where(t => t.Id == episode.TvId)
                            .FirstOrDefaultAsync();
                    }
                }
                
                type = library.Type;
                break;
        }

        return (movie, show, type);
    }

    public FileInfo[] GetVideoFilesInDirectory(string directoryPath)
    {
        DirectoryInfo directoryInfo = new(directoryPath);
        return directoryInfo.GetFiles()
            .Where(file => file.Extension is ".mkv" or ".mp4" or ".avi" or ".webm" or ".flv")
            .ToArray();
    }

    public FileInfo[] GetAudioFilesInDirectory(string directoryPath)
    {
        DirectoryInfo directoryInfo = new(directoryPath);
        return directoryInfo.GetFiles()
            .Where(file => file.Extension is ".mp3" or ".flac" or ".wav" or ".m4a")
            .ToArray();
    }

    public async Task<List<FileItem>> GetFilesInDirectory(string directoryPath, string libraryType)
    {
        DirectoryInfo directoryInfo = new(directoryPath);

        FileInfo[] videoFiles = GetVideoFilesInDirectory(directoryPath);

        FileInfo[] audioFiles = GetAudioFilesInDirectory(directoryPath);
        
        ConcurrentBag<FileItem> fileList = [];
        if (videoFiles.Length == 0 && audioFiles.Length == 0)
            return fileList.ToList();

        if (audioFiles.Length > 0 && videoFiles.Length == 0)
        {
            const string pattern =
                @"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]|\[(?<releaseType>Singles)\])\s?(?<album>.*)?";
            Match match = Regex.Match(directoryPath, pattern);

            int year = match.Groups["year"].Success ? Convert.ToInt32(match.Groups["year"].Value) : 0;
            string albumName = match.Groups["album"].Success
                ? match.Groups["album"].Value
                : Regex.Replace(directoryInfo.Name, @"\[\d{4}\]\s?", "");

            await Parallel.ForEachAsync(audioFiles, Config.ParallelOptions, (file, _) =>
            {
                fileList.Add(new()
                {
                    Size = file.Length,
                    Mode = (int)file.Attributes,
                    Name = Path.Combine(directoryPath, file.Name),
                    Parent = directoryPath,
                    Parsed = new(directoryPath)
                    {
                        Title = albumName + " - " + Path.GetFileNameWithoutExtension(file.Name),
                        Year = year.ToString(),
                        IsSeries = false,
                        IsSuccess = true
                    },
                    Match = new()
                    {
                        Title = albumName
                    },
                    Path = Path.Combine(directoryPath, file.FullName)
                });
                return ValueTask.CompletedTask;
            });
        }
        else if (videoFiles.Length > 0)
        {
            // Process video files sequentially - each file may add shows/movies to the DB,
            // and parallel processing causes race conditions when the show isn't known yet
            // (multiple iterations see "not found" simultaneously, all try to add, some fail)
            foreach (FileInfo file in videoFiles)
            {
                try
                {
                    await ProcessVideoFileInfo(_context, libraryType, file, fileList);
                }
                catch (Exception e)
                {
                    Logger.App(e.Message, LogEventLevel.Error);
                }
            }
        }

        return fileList.OrderBy(file => file.Name).ToList();
    }

    private async Task<bool> ProcessVideoFileInfo(MediaContext ctx, string libraryType, FileInfo file,
        ConcurrentBag<FileItem> fileList)
    {
        string title = file.FullName.Replace("v2", "");
        title = Str.RemoveBracketedString().Replace(title, string.Empty);

        Ffprobe ffprobeData = await new Ffprobe(file.FullName).GetStreamData();
        MovieFile parsed = ParseVideoFileName(file, title);

        parsed.Year ??= title.TryGetYear();
        if (parsed.Title == null) return true;

        if (parsed.Episode.HasValue && !parsed.Season.HasValue)
            parsed.Season = 1;

        if (!parsed.Season.HasValue && !parsed.Episode.HasValue)
        {
            Regex regex = Str.MatchNumbers();
            Match numberMatch = regex.Match(parsed.Title);
            if (numberMatch.Success)
            {
                parsed.Season = 1;
                parsed.Episode = int.Parse(numberMatch.Value);
                parsed.Title = regex.Split(parsed.Title).FirstOrDefault();
            }
        }

        (MovieOrEpisode episodeMatch, string? imdbId)? result = libraryType switch
        {
            Config.AnimeMediaType or Config.TvMediaType =>
                await ResolveShowEpisodeAsync(ctx, libraryType, parsed, ffprobeData.Format.Duration),
            Config.MovieMediaType =>
                await ResolveMovieMatchAsync(ctx, libraryType, parsed, ffprobeData.Format.Duration),
            _ => null
        };

        if (result == null) return true;

        parsed.ImdbId = result.Value.imdbId;
        fileList.Add(BuildFileItem(file, parsed, result.Value.episodeMatch, ffprobeData));
        return false;
    }

    private static MovieFile ParseVideoFileName(FileInfo file, string title)
    {
        string cleanedFileName = Str.RemoveBracketedString()
            .Replace(Path.GetFileNameWithoutExtension(file.Name), string.Empty).Trim();

        // S##E## at start of filename (e.g. "S01E01-some.title.mkv")
        Match epMatch = Str.MatchEpisodePrefix().Match(cleanedFileName);
        if (epMatch.Success)
        {
            return new MovieFile(title)
            {
                Title = ExtractTitleFromFolder(file),
                Season = int.Parse(epMatch.Groups[1].Value),
                Episode = int.Parse(epMatch.Groups[2].Value),
                IsSeries = true,
                IsSuccess = true
            };
        }

        // "Episode XX" pattern (e.g. "Blade - Episode 02 - title.mp4")
        string fileNameNoParens = Str.RemoveParenthesizedString()
            .Replace(cleanedFileName, string.Empty).Trim();
        Match episodeWordMatch = Str.MatchEpisodeWord().Match(fileNameNoParens);
        if (episodeWordMatch.Success)
        {
            int episodeNumber = int.Parse(episodeWordMatch.Groups[1].Value);
            string showTitle = fileNameNoParens[..episodeWordMatch.Index]
                .TrimEnd('-', '.', '_', ' ');

            if (string.IsNullOrWhiteSpace(showTitle) || showTitle.Length <= 1)
                showTitle = ExtractTitleFromFolder(file);

            return new MovieFile(title)
            {
                Title = showTitle,
                Season = 1,
                Episode = episodeNumber,
                IsSeries = true,
                IsSuccess = true
            };
        }

        // S##E#### anywhere in filename (e.g. "One.Piece.S01E1109.Title.mkv")
        Match seasonEpMatch = Str.MatchSeasonEpisode().Match(cleanedFileName);
        if (seasonEpMatch.Success)
        {
            string showTitle = cleanedFileName[..seasonEpMatch.Index]
                .Replace('.', ' ').Replace('_', ' ')
                .TrimEnd('-', ' ').Trim();

            if (string.IsNullOrWhiteSpace(showTitle) || showTitle.Length <= 1)
                showTitle = ExtractTitleFromFolder(file);

            return new MovieFile(title)
            {
                Title = showTitle,
                Season = int.Parse(seasonEpMatch.Groups[1].Value),
                Episode = int.Parse(seasonEpMatch.Groups[2].Value),
                IsSeries = true,
                IsSuccess = true
            };
        }

        // Fallback to MovieDetector library
        MovieDetector movieDetector = new();
        return movieDetector.GetInfo(title);
    }

    private static string ExtractTitleFromFolder(FileInfo file)
    {
        string? folderName = Path.GetFileName(file.DirectoryName);
        if (string.IsNullOrWhiteSpace(folderName)) return "";

        string cleaned = Str.RemoveBracketedString().Replace(folderName, string.Empty);
        cleaned = Str.RemoveParenthesizedString().Replace(cleaned, string.Empty);

        Match seasonTag = Str.MatchSeasonTag().Match(cleaned);
        if (seasonTag.Success && seasonTag.Index > 0)
            cleaned = cleaned[..seasonTag.Index];

        return cleaned.Replace('.', ' ').Replace('_', ' ')
            .TrimEnd('-', '.', '_', ' ').Trim();
    }

    private static async Task<(MovieOrEpisode match, string? imdbId)?> ResolveShowEpisodeAsync(
        MediaContext ctx, string libraryType, MovieFile parsed, TimeSpan? duration)
    {
        TmdbSearchClient searchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? shows =
            await searchClient.TvShow(parsed.Title ?? "", parsed.Year ?? "", true);
        TmdbTvShow? show = shows?.Results.FirstOrDefault();
        if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) return null;

        Ulid libraryId = await ctx.Libraries
            .Where(item => item.Type == libraryType)
            .Select(item => item.Id)
            .FirstOrDefaultAsync();

        await EnsureShowInLibraryAsync(ctx, show.Id, show.Name, libraryId);

        Episode? episode = ctx.Episodes
            .Where(item => item.TvId == show.Id)
            .Where(item => item.SeasonNumber == parsed.Season)
            .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

        if (episode == null)
        {
            List<Episode> episodes = ctx.Episodes
                .Where(item => item.TvId == show.Id)
                .Where(item => item.SeasonNumber > 0)
                .OrderBy(item => item.SeasonNumber)
                .ThenBy(item => item.EpisodeNumber)
                .ToList();

            episode = episodes.ElementAtOrDefault(parsed.Episode.Value - 1);
        }

        if (episode == null)
            episode = await ResolveAbsoluteEpisodeAsync(ctx, show.Id, parsed.Episode.Value);

        // Try alternate search results (e.g. TMDB may rank live-action above anime)
        if (episode == null && shows!.Results.Count > 1)
        {
            foreach (TmdbTvShow altShow in shows.Results.Skip(1).Take(4))
            {
                TmdbTvClient altTvClient = new(altShow.Id);
                TmdbTvEpisodeGroups? altGroups = await altTvClient.EpisodeGroups(true);
                if (altGroups?.Results.Any(g => g.Type == 2) != true) continue;

                await EnsureShowInLibraryAsync(ctx, altShow.Id, altShow.Name, libraryId);
                episode = await ResolveAbsoluteEpisodeAsync(ctx, altShow.Id, parsed.Episode.Value);
                if (episode != null) break;
            }
        }

        if (episode == null)
        {
            TmdbEpisodeClient episodeClient = new(show.Id, parsed.Season.Value, parsed.Episode.Value);
            TmdbEpisodeDetails? details = await episodeClient.Details(true);
            if (details == null) return null;

            Season? season = await ctx.Seasons
                .FirstOrDefaultAsync(s =>
                    s.TvId == show.Id && s.SeasonNumber == details.SeasonNumber);

            episode = new()
            {
                Id = details.Id,
                TvId = show.Id,
                SeasonNumber = details.SeasonNumber,
                EpisodeNumber = details.EpisodeNumber,
                Title = details.Name,
                Overview = details.Overview,
                Still = details.StillPath,
                VoteAverage = details.VoteAverage,
                VoteCount = details.VoteCount,
                AirDate = details.AirDate,
                SeasonId = season?.Id ?? 0,
            };

            ctx.Episodes.Add(episode);
            await ctx.SaveChangesAsync();
        }

        MovieOrEpisode match = new()
        {
            Id = episode.Id,
            Title = episode.Title ?? "",
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Still = episode.Still,
            Duration = duration,
            Overview = episode.Overview
        };

        return (match, episode.ImdbId);
    }

    private static async Task<(MovieOrEpisode match, string? imdbId)?> ResolveMovieMatchAsync(
        MediaContext ctx, string libraryType, MovieFile parsed, TimeSpan? duration)
    {
        TmdbSearchClient searchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? movies =
            await searchClient.Movie(parsed.Title ?? "", parsed.Year ?? "", true);
        TmdbMovie? movie = movies?.Results.FirstOrDefault();
        if (movie == null) return null;

        Movie? movieItem = ctx.Movies
            .FirstOrDefault(item => item.Id == movie.Id);

        if (movieItem == null)
        {
            TmdbMovieClient movieClient = new(movie.Id);
            TmdbMovieDetails? details = await movieClient.Details(true);
            if (details == null) return null;

            bool hasMovie = ctx.Movies.Any(item => item.Id == movie.Id);

            Ulid libraryId = await ctx.Libraries
                .Where(item => item.Type == libraryType)
                .Select(item => item.Id)
                .FirstOrDefaultAsync();

            if (!hasMovie)
            {
                Networking.Networking.SendToAll("Notify", "videoHub", new NotifyDto
                {
                    Title = "Movie not found",
                    Message = $"Movie {movie.Title} not found in library, adding now",
                    Type = "info"
                });
                AddMovieJob job = new()
                {
                    LibraryId = libraryId,
                    Id = movie.Id
                };
                await job.Handle();
            }

            movieItem = new()
            {
                Id = details.Id,
                Title = details.Title,
                Overview = details.Overview,
                Poster = details.PosterPath
            };
        }

        MovieOrEpisode match = new()
        {
            Id = movieItem.Id,
            Title = movieItem.Title,
            Still = movieItem.Poster,
            Duration = duration,
            Overview = movieItem.Overview
        };

        return (match, movieItem.ImdbId);
    }

    private static async Task EnsureShowInLibraryAsync(MediaContext ctx, int showId, string showName, Ulid libraryId)
    {
        bool hasShow = ctx.Tvs.Any(item => item.Id == showId);
        if (hasShow) return;

        Networking.Networking.SendToAll("Notify", "videoHub", new NotifyDto
        {
            Title = "Show not found",
            Message = $"Show {showName} not found in library, adding now",
            Type = "info"
        });

        AddShowJob job = new()
        {
            LibraryId = libraryId,
            Id = showId,
            HighPriority = true
        };
        await job.Handle();
    }

    private static FileItem BuildFileItem(FileInfo file, MovieFile parsed, MovieOrEpisode match, Ffprobe ffprobeData)
    {
        string? parentPath = string.IsNullOrEmpty(file.DirectoryName)
            ? "/"
            : Path.GetDirectoryName(Path.Combine(file.DirectoryName, ".."));

        return new()
        {
            Size = file.Length,
            Mode = (int)file.Attributes,
            Name = Path.GetFileNameWithoutExtension(file.Name),
            Parent = parentPath,
            Parsed = parsed,
            Match = match,
            Path = file.FullName,
            Streams = new()
            {
                Video = ffprobeData.VideoStreams
                    .Select(video => new Video
                    {
                        Index = video.Index,
                        Width = video.Width,
                        Height = video.Height
                    }),
                Audio = ffprobeData.AudioStreams
                    .Select(stream => new Audio
                    {
                        Index = stream.Index,
                        Language = stream.Language
                    }),
                Subtitle = ffprobeData.SubtitleStreams
                    .Select(stream => new Subtitle
                    {
                        Index = stream.Index,
                        Language = stream.Language ?? "und"
                    })
            }
        };
    }

    private static async Task<Episode?> ResolveAbsoluteEpisodeAsync(MediaContext ctx, int showId, int absoluteEpisodeNumber)
    {
        TmdbTvClient tvClient = new(showId);
        TmdbTvEpisodeGroups? episodeGroups = await tvClient.EpisodeGroups(true);
        if (episodeGroups == null)
        {
            Logger.App($"No episode groups found for show {showId}", LogEventLevel.Debug);
            return null;
        }

        // Try all "Absolute" type groups (type 2) — some shows have multiple
        TmdbEpisodeGroupsResult[] absoluteGroups = episodeGroups.Results
            .Where(g => g.Type == 2)
            .ToArray();

        if (absoluteGroups.Length == 0)
        {
            Logger.App($"No absolute episode group (type 2) for show {showId}, available types: {string.Join(", ", episodeGroups.Results.Select(g => $"{g.Name}={g.Type}"))}", LogEventLevel.Debug);
            return null;
        }

        foreach (TmdbEpisodeGroupsResult absoluteGroup in absoluteGroups)
        {
            TmdbEpisodeGroupClient groupClient = new(absoluteGroup.Id);
            TmdbEpisodeGroupDetails? groupDetails = await groupClient.Details(true);
            if (groupDetails == null)
            {
                Logger.App($"Failed to fetch episode group details for {absoluteGroup.Id} ({absoluteGroup.Name})", LogEventLevel.Debug);
                continue;
            }

            // Flatten all episodes across all groups, ordered by group order
            List<TmdbEpisodeGroupEpisode> allEpisodes = groupDetails.Groups
                .OrderBy(g => g.Order)
                .SelectMany(g => g.Episodes)
                .ToList();

            if (absoluteEpisodeNumber < 1 || absoluteEpisodeNumber > allEpisodes.Count)
            {
                Logger.App($"Absolute episode {absoluteEpisodeNumber} out of range in '{absoluteGroup.Name}' (has {allEpisodes.Count} episodes)", LogEventLevel.Debug);
                continue;
            }

            TmdbEpisodeGroupEpisode target = allEpisodes[absoluteEpisodeNumber - 1];
            Logger.App($"Resolved absolute episode {absoluteEpisodeNumber} → S{target.SeasonNumber:D2}E{target.EpisodeNumber:D2} ({target.Name}) via '{absoluteGroup.Name}'", LogEventLevel.Information);

            // Look up the resolved episode in the DB
            Episode? episode = await ctx.Episodes
                .FirstOrDefaultAsync(e => e.TvId == showId
                    && e.SeasonNumber == target.SeasonNumber
                    && e.EpisodeNumber == target.EpisodeNumber);

            if (episode != null) return episode;

            // Fetch from TMDB and add to DB
            TmdbEpisodeClient episodeClient = new(showId, target.SeasonNumber, target.EpisodeNumber);
            TmdbEpisodeDetails? details = await episodeClient.Details(true);
            if (details == null) continue;

            Season? season = await ctx.Seasons
                .FirstOrDefaultAsync(s => s.TvId == showId && s.SeasonNumber == details.SeasonNumber);

            episode = new()
            {
                Id = details.Id,
                TvId = showId,
                SeasonNumber = details.SeasonNumber,
                EpisodeNumber = details.EpisodeNumber,
                Title = details.Name,
                Overview = details.Overview,
                Still = details.StillPath,
                VoteAverage = details.VoteAverage,
                VoteCount = details.VoteCount,
                AirDate = details.AirDate,
                SeasonId = season?.Id ?? 0,
            };

            ctx.Episodes.Add(episode);
            await ctx.SaveChangesAsync();

            return episode;
        }

        return null;
    }

    private static readonly List<string> PrevSearchQueries = [];

    public static async Task<List<FileItem>> GetMusicBrainzReleasesInDirectory(string folder)
    {
        PrevSearchQueries.Clear();

        MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> mediaFolders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType("music")
            .Process(folder, 2);

        if (mediaFolders.Count == 0)
            return [];

        ConcurrentBag<MediaFile> mediaFiles = mediaFolders
            .SelectMany(m => m.Files ?? [])
            .ToConcurrentBag();

        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        List<Guid> lookupReleaseIds = [];

        (List<MusicBrainzReleaseAppends> releases, string year) = await SearchForReleasesFromMediaFiles(
            mediaFiles,
            musicBrainzReleaseClient,
            lookupReleaseIds,
            musicBrainzRecordingClient
        );

        releases = await FetchReleaseAppends(
            lookupReleaseIds,
            musicBrainzReleaseClient,
            releases
        );
        List<FileItem> files = await GenerateResponse(folder, releases, mediaFiles, year);
        return files;
    }

    private static async Task<(List<MusicBrainzReleaseAppends> releases, string year)> SearchForReleasesFromMediaFiles(
        ConcurrentBag<MediaFile> mediaFiles,
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        List<Guid> lookupReleaseIds,
        MusicBrainzRecordingClient musicBrainzRecordingClient
    )
    {
        string prevMusicBrainzReleaseId = string.Empty;
        string year = "0";
        List<MusicBrainzReleaseAppends> releases = [];
        object lockObject = new();
        
        await Parallel.ForEachAsync(mediaFiles, Config.ParallelOptions, async (mediaFile, _) =>
        {
            AudioTagModel audioTagModel = await AudioTagModel.Create(mediaFile);
            
            if (audioTagModel.Tags == null) return;
            if (!string.IsNullOrEmpty(audioTagModel.Tags.MusicBrainzReleaseId))
            {
                (prevMusicBrainzReleaseId, year) = await FromMusicBrainzRelease(musicBrainzReleaseClient, audioTagModel, lockObject, releases, prevMusicBrainzReleaseId, year);
            }
            else
            {
                prevMusicBrainzReleaseId = await FromFingerprint(musicBrainzReleaseClient, mediaFile, lockObject, releases) ?? prevMusicBrainzReleaseId;
            }
        });
        releases = releases
            .Where(x => x.Id != Guid.Empty)
            .DistinctBy(x => x.Id)
            .ToList();
        return (releases, year);
    }

    private static async Task<string?> FromFingerprint(MusicBrainzReleaseClient musicBrainzReleaseClient, MediaFile mediaFile,
        object lockObject, List<MusicBrainzReleaseAppends> releases)
    {
        string prevMusicBrainzReleaseId;
        AcoustIdFingerprintClient acoustIdFingerprintClient = new();
        AcoustIdFingerprint? acoustIds = await acoustIdFingerprintClient.Lookup(mediaFile.Path);
        if (acoustIds == null) return null;
        foreach (AcoustIdFingerprintResult fingerPrint in acoustIds?.Results ?? [])
        {
            foreach (AcoustIdFingerprintRecording? recording in fingerPrint.Recordings ?? [])
            {
                if (recording?.Releases is null) continue;
                foreach (AcoustIdFingerprintReleaseGroups acoustIdFingerprintReleaseGroups in recording.Releases)
                {
                    MusicBrainzReleaseAppends? release =
                        await musicBrainzReleaseClient.WithAllAppends(acoustIdFingerprintReleaseGroups.Id);

                    if (release == null || release.Id == Guid.Empty) return null;
                    prevMusicBrainzReleaseId = release.Id.ToString();
                    lock (lockObject)
                    {
                        releases.Add(release);
                    }
                }
            }
        }

        return null;
    }

    private static async Task<(string prevMusicBrainzReleaseId, string year)> FromMusicBrainzRelease(MusicBrainzReleaseClient musicBrainzReleaseClient,
        AudioTagModel audioTagModel, object lockObject, List<MusicBrainzReleaseAppends> releases, string prevMusicBrainzReleaseId, string year)
    {
        if (prevMusicBrainzReleaseId == audioTagModel.Tags?.MusicBrainzReleaseId)
        {
            if (year == "0")
                year = audioTagModel.Tags?.Year.ToString() ?? "0";
            return (prevMusicBrainzReleaseId, year);
        }

        Guid musicBrainzReleaseId = Guid.Parse(audioTagModel.Tags?.MusicBrainzReleaseId ?? "");
        if (musicBrainzReleaseId == Guid.Empty) return (prevMusicBrainzReleaseId, year);
        MusicBrainzReleaseAppends? release =
            await musicBrainzReleaseClient.WithAllAppends(musicBrainzReleaseId);

        if (release == null || release.Id == Guid.Empty) return (prevMusicBrainzReleaseId, year);
        prevMusicBrainzReleaseId = release.Id.ToString();
        lock (lockObject)
        {
            releases.Add(release);
        }

        return (prevMusicBrainzReleaseId, year);
    }

    private static async Task<List<FileItem>> GenerateResponse(
        string folder,
        List<MusicBrainzReleaseAppends> releases,
        ConcurrentBag<MediaFile> mediaFiles,
        string year
    )
    {
        if (releases.Count == 0)
            return [];

        List<FileItem> files = [];

        MusicBrainzReleaseAppends? bestResult = await GetBestMatchedRelease(mediaFiles, releases);
        if (bestResult != null)
        {
            Logger.MusicBrainz($"Best match: {bestResult.Title} - {bestResult.Id}", LogEventLevel.Verbose);

            Uri? coverPaletteUrl = await CoverArtImageManagerManager.GetCoverUrl(bestResult.Id, true);

            files.Add(new()
            {
                Size = mediaFiles.Sum(x => x.Size),
                Mode = 0,
                Name = bestResult.Title,
                Parent = folder,
                Parsed = new(folder)
                {
                    Title = bestResult.Title,
                    Year = bestResult.DateTime?.Year.ToString() ?? year,
                    IsSeries = false,
                    IsSuccess = true
                },
                Match = new()
                {
                    Id = bestResult.Id,
                    Title = bestResult.Title,
                    Still = coverPaletteUrl?.ToString()
                },
                Path = folder,
                Tracks = bestResult.Media.Sum(m => m.TrackCount),
                Streams = new()
                {
                    Audio =
                    [
                        new()
                        {
                            Index = 0,
                            Language =
                                $"Best Match {string.Join(", ", Enumerable.Select<MusicBrainzMedia, string>(bestResult.Media, m => m.Format))}"
                        }
                    ]
                }
            });
        }

        await Parallel.ForEachAsync(releases, Config.ParallelOptions, async (release, _) =>
        {
            if (files.Any(x => x.Match.Id == release.Id)) return;

            Uri? coverPaletteUrl = await CoverArtImageManagerManager.GetCoverUrl(release.Id, true);

            files.Add(new()
            {
                Size = mediaFiles.Sum(x => x.Size),
                Mode = 0,
                Name = release.Title,
                Parent = folder,
                Parsed = new(folder)
                {
                    Title = release.Title,
                    Year = release.DateTime?.Year.ToString() ?? year,
                    IsSeries = false,
                    IsSuccess = true
                },
                Match = new()
                {
                    Id = release.Id,
                    Title = release.Title,
                    Still = coverPaletteUrl?.ToString()
                },
                Path = folder,
                Tracks = release.Media.Sum(m => m.TrackCount),
                Streams = new()
                {
                    Audio =
                    [
                        new()
                        {
                            Index = 0,
                            Language = $"Formats: {string.Join(", ", release.Media.Select(m => m.Format))}"
                        }
                    ]
                }
            });
        });

        return files;
    }

    private static async Task<List<MusicBrainzReleaseAppends>> FetchReleaseAppends(
        List<Guid> lookupReleaseIds,
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        List<MusicBrainzReleaseAppends> releases
    )
    {
        object lockObject = new();
        lookupReleaseIds = lookupReleaseIds.DistinctBy(x => x).ToList();
        await Parallel.ForEachAsync(lookupReleaseIds, Config.ParallelOptions, async (releaseId, _) =>
        {
            MusicBrainzReleaseAppends? musicBrainzRelease =
                await musicBrainzReleaseClient.WithAllAppends(releaseId, true);
            if (musicBrainzRelease == null || releases.Any(r => r.Id == musicBrainzRelease.Id)) return;
            lock (lockObject)
            {
                releases.Add(musicBrainzRelease);
            }
        });

        return releases
            .Where(x => x.Id != Guid.Empty)
            .DistinctBy(x => x.Id)
            .ToList();
    }

    private static async Task<MusicBrainzReleaseAppends?> GetBestMatchedRelease(
        ConcurrentBag<MediaFile> mediaFiles,
        List<MusicBrainzReleaseAppends> matchedReleases
    )
    {
        MusicBrainzReleaseAppends? bestRelease = null;
        int highestScore = 0;
        object lockObject = new();

        await Parallel.ForEachAsync(matchedReleases, Config.ParallelOptions, (release, _) =>
        {
            int score = CalculateMatchScore(release, mediaFiles);
            lock (lockObject)
            {
                if (score < highestScore) return ValueTask.CompletedTask;
                highestScore = score;
                if (highestScore == mediaFiles.Count)
                    bestRelease = release;
            }

            return ValueTask.CompletedTask;
        });

        return bestRelease;
    }

    private static int CalculateMatchScore(
        MusicBrainzReleaseAppends release,
        ConcurrentBag<MediaFile> localFiles
    )
    {
        int score = 0;

        if (release.Media.Length == 0) return 0;

        Parallel.ForEach(release.Media, Config.ParallelOptions, media =>
        {
            if (media.Tracks.Length == 0 || media.TrackCount == 0)
                return;

            Parallel.ForEach(localFiles, Config.ParallelOptions, file =>
            {
                try
                {
                    file.TagFile ??= TagFile.Create(file.Path);
                    file.FFprobe ??= FfProbe.Create(file.Path);

                    int trackIndex = localFiles.ToList().IndexOf(file);
                    bool isMatch = media.Tracks.Any(track =>
                    {
                        bool nameMatch = CompareTrackName(file, track);
                        bool numberMatch = CompareTrackNumber(file, track, trackIndex);
                        bool durationMatch = CompareTrackDuration(file, track);
                        return nameMatch && numberMatch && durationMatch;
                    });

                    if (!isMatch) return;
                    score = Interlocked.Increment(ref score);
                }
                catch (Exception ex)
                {
                    Logger.MusicBrainz($"Error processing file {file.Path}: {ex.Message}", LogEventLevel.Verbose);
                }
            });
        });

        return score;
    }

    private static bool CompareTrackDuration(MediaFile mediaFile, MusicBrainzTrack track)
    {
        double duration = track.Duration;
        double tagDuration = mediaFile.TagFile?.Properties?.Duration.TotalSeconds ?? 0;
        double fileDuration = mediaFile.FFprobe?.Duration.TotalSeconds ?? 0;

        if (duration == 0 && fileDuration == 0 && tagDuration == 0) return false;

        return Math.Abs(duration - fileDuration).ToInt() < 3 ||
               Math.Abs(duration - tagDuration).ToInt() < 3;
    }

    private static bool CompareTrackNumber(MediaFile mediaFile, MusicBrainzTrack track, int trackIndex)
    {
        int trackNumber = track.Position;
        long tagTrackNumber = mediaFile.TagFile?.Tag?.Track ?? 0;
        int fileTrackNumber = mediaFile.Parsed?.TrackNumber ?? 0;

        if (trackNumber == 0 && fileTrackNumber == 0 && tagTrackNumber == 0) return false;

        return Math.Abs(trackNumber - fileTrackNumber) == 0 ||
               Math.Abs(trackNumber - trackIndex) == 0 ||
               (int)Math.Abs(trackNumber - tagTrackNumber) == 0;
    }

    private static bool CompareTrackName(MediaFile mediaFile, MusicBrainzTrack track)
    {
        string trackTitle = track.Title;
        string tagTitle = mediaFile.TagFile?.Tag?.Title ?? Path.GetFileNameWithoutExtension(mediaFile.Name);
        string fileTitle = mediaFile.Parsed?.Title ?? Path.GetFileNameWithoutExtension(mediaFile.Name);

        if (string.IsNullOrEmpty(trackTitle) && string.IsNullOrEmpty(fileTitle) &&
            string.IsNullOrEmpty(tagTitle)) return false;

        return fileTitle.ContainsSanitized(trackTitle) ||
               tagTitle.ContainsSanitized(trackTitle);
    }

    public async Task<int> DeleteVideoFilesByHostFolderAsync(string hostFolder)
    {
        return await _context.VideoFiles
            .Where(vf => vf.HostFolder == hostFolder)
            .ExecuteDeleteAsync();
    }

    public async Task<int> DeleteMetadataByHostFolderAsync(string hostFolder)
    {
        return await _context.Metadata
            .Where(m => m.HostFolder == hostFolder)
            .ExecuteDeleteAsync();
    }

    public async Task<int> UpdateVideoFilePathsAsync(string oldHostFolder, string oldFilename, string newHostFolder, string newFilename)
    {
        string newFolder = "/" + Path.GetFileName(Path.GetDirectoryName(newHostFolder + "/placeholder"));

        return await _context.VideoFiles
            .Where(vf => vf.HostFolder == oldHostFolder && vf.Filename == oldFilename)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(vf => vf.HostFolder, newHostFolder)
                .SetProperty(vf => vf.Filename, newFilename)
                .SetProperty(vf => vf.Folder, newFolder));
    }

    public List<DirectoryTree> GetDirectoryTree(string folder = "")
    {
        List<DirectoryTree> array = [];

        if (string.IsNullOrEmpty(folder) || folder == "/")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DriveInfo[] driveInfo = DriveInfo.GetDrives();
                return driveInfo.Select(d => new DirectoryTree(d.RootDirectory.ToString(), ""))
                    .OrderBy(file => file.Path)
                    .ToList();
            }

            folder = "/";
        }

        if (!Directory.Exists(folder)) return array;

        string[] directories = Directory.GetDirectories(folder);
        array = directories.Select(d => new DirectoryTree(folder, d))
            .OrderBy(file => file.Path)
            .ToList();

        return array;
    }
}