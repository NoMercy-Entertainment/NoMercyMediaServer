using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MovieFileLibrary;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class FileRepository() : IFileRepository
{
    public Task StoreVideoFile(VideoFile videoFile)
    {
        MediaContext context = new();
        return context.VideoFiles.Upsert(videoFile)
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
                UpdatedAt = vi.UpdatedAt,
                MetadataId = vi.MetadataId,
            })
            .RunAsync();
    }

    public async Task<Ulid> StoreMetadata(Metadata metadata)
    {
        MediaContext context = new();
        await context.Metadata.Upsert(metadata)
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
                _chapters_file = mi._chapters_file,
                _fonts = mi._fonts,
                _fonts_file = mi._fonts_file,
                _previews = mi._previews,
                _subtitles = mi._subtitles,
                _video = mi._video,
            })
            .RunAsync();
        
        return context.Metadata
            .Where(m => m.Filename == metadata.Filename)
            .Where(m => m.HostFolder == metadata.HostFolder)
            .Select(m => m.Id)
            .FirstOrDefault();
    }

    public async Task<Episode?> GetEpisode(int? showId, MediaFile item)
    {
        if (item.Parsed == null) return null;

        MediaContext context = new();
        return await context.Episodes
            .Where(e => e.TvId == showId)
            .Where(e => e.SeasonNumber == item.Parsed!.Season)
            .Where(e => e.EpisodeNumber == item.Parsed!.Episode)
            .FirstOrDefaultAsync();
    }

    public async Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library)
    {
        MediaContext context = new();
        Movie? movie = null;
        Tv? show = null;
        string type = "";

        switch (library.Type)
        {
            case "movie":
                movie = await context.Movies
                    .Where(m => m.Id == id)
                    .FirstOrDefaultAsync();
                type = "movie";
                break;
            case "tv":
                show = await context.Tvs
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();
                type = "tv";
                break;
            case "anime":
                show = await context.Tvs
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();
                type = "anime";
                break;
        }

        return (movie, show, type);
    }

    public async Task SetCreatedAt(VideoFile videoFile)
    {
        string path = videoFile.HostFolder.Substring(0, videoFile.HostFolder.LastIndexOf('/'));
        DateTime createdDateTime = Directory.GetCreationTime(path);

        if (videoFile.CreatedAt == createdDateTime) return;
        
        MediaContext context = new();
        if (videoFile.EpisodeId is not null)
        {
            Tv? tv = await context.Tvs.FindAsync(videoFile.EpisodeId);
            if (tv is null) return;

            tv.CreatedAt = createdDateTime;
            await context.SaveChangesAsync();
        }
        else if (videoFile.MovieId is not null)
        {
            Movie? movie = await context.Movies.FindAsync(videoFile.MovieId);
            if (movie is null) return;

            movie.CreatedAt = createdDateTime;
            await context.SaveChangesAsync();
        }

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
        FFMpegCore.GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        DirectoryInfo directoryInfo = new(directoryPath);
        
        FileInfo[] videoFiles = GetVideoFilesInDirectory(directoryPath);

        FileInfo[] audioFiles = GetAudioFilesInDirectory(directoryPath);

        List<FileItem> fileList = [];
        if (videoFiles.Length == 0 && audioFiles.Length == 0)
            return fileList;

        if (audioFiles.Length > 0 && videoFiles.Length == 0)
        {
            const string pattern = @"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]|\[(?<releaseType>Singles)\])\s?(?<album>.*)?";
            Match match = Regex.Match(directoryPath, pattern);

            int year = match.Groups["year"].Success ? Convert.ToInt32(match.Groups["year"].Value) : 0;
            string albumName = match.Groups["album"].Success ? match.Groups["album"].Value : Regex.Replace(directoryInfo.Name, @"\[\d{4}\]\s?", "");
            
            await Parallel.ForEachAsync(audioFiles, (file, _) =>
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
                        Title = albumName,
                    },
                    Path = Path.Combine(directoryPath, file.FullName)
                });
                return ValueTask.CompletedTask;
            });
        }
        else if (videoFiles.Length > 0)
        {
            await Parallel.ForEachAsync(videoFiles, async (file, token) =>
            {
                try
                {
                    MovieOrEpisode match = new();
                    TmdbSearchClient searchClient = new();
                    MovieDetector movieDetector = new();

                    string title = file.FullName.Replace("v2", "");
                    // remove any text in square brackets that may cause year to match incorrectly
                    title = Str.RemoveBracketedString().Replace(title, string.Empty);

                    FFMpegCore.IMediaAnalysis mediaAnalysis = await FFMpegCore.FFProbe.AnalyseAsync(file.FullName, cancellationToken: token);

                    MovieFile parsed = movieDetector.GetInfo(title);

                    parsed.Year ??= title.TryGetYear();


                    if (parsed.Title == null) return;

                    Regex regex = Str.MatchNumbers();
                    Match match2 = regex.Match(parsed.Title);

                    if (match2.Success)
                    {
                        parsed.Season = 1;
                        parsed.Episode = int.Parse(match2.Value);

                        parsed.Title = regex.Split(parsed.Title).FirstOrDefault();
                    }

                    switch (libraryType)
                    {
                        case "anime" or "tv":
                        {
                            TmdbPaginatedResponse<TmdbTvShow>? shows =
                                await searchClient.TvShow(parsed.Title ?? "", parsed.Year ?? "");
                            TmdbTvShow? show = shows?.Results.FirstOrDefault();
                            if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) return;

                            MediaContext context = new();
                            bool hasShow = context.Tvs
                                .Any(item => item.Id == show.Id);

                            Ulid libraryId = await context.Libraries
                                .Where(item => item.Type == libraryType)
                                .Select(item => item.Id)
                                .FirstOrDefaultAsync();

                            if (!hasShow)
                            {
                                Networking.Networking.SendToAll("Notify", "socket", new NotifyDto
                                {
                                    Title = "Show not found",
                                    Message = $"Show {show.Name} not found in library, adding now",
                                    Type = "info"
                                });
                                AddShowJob job = new()
                                {
                                    LibraryId = libraryId,
                                    Id = show.Id
                                };
                                await job.Handle();
                            }

                            Episode? episode = context.Episodes
                                .Where(item => item.TvId == show.Id)
                                .Where(item => item.SeasonNumber == parsed.Season)
                                .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

                            if (episode == null)
                            {
                                List<Episode> episodes = context.Episodes
                                    .Where(item => item.TvId == show.Id)
                                    .Where(item => item.SeasonNumber > 0)
                                    .OrderBy(item => item.SeasonNumber)
                                    .ThenBy(item => item.EpisodeNumber)
                                    .ToList();

                                episode = episodes.ElementAtOrDefault(parsed.Episode.Value - 1);
                            }

                            if (episode == null)
                            {
                                TmdbEpisodeClient episodeClient =
                                    new(show.Id, parsed.Season.Value, parsed.Episode.Value);
                                TmdbEpisodeDetails? details = await episodeClient.Details();
                                if (details == null) return;

                                Season? season = await context.Seasons
                                    .FirstOrDefaultAsync(season =>
                                        season.TvId == show.Id && season.SeasonNumber == details.SeasonNumber);

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
                                    _colorPalette = await MovieDbImageManager.ColorPalette("still", details.StillPath)
                                };

                                context.Episodes.Add(episode);
                                await context.SaveChangesAsync();
                            }

                            match = new()
                            {
                                Id = episode.Id,
                                Title = episode.Title ?? "",
                                EpisodeNumber = episode.EpisodeNumber,
                                SeasonNumber = episode.SeasonNumber,
                                Still = episode.Still,
                                Duration = mediaAnalysis.Duration,
                                Overview = episode.Overview
                            };

                            parsed.ImdbId = episode.ImdbId;
                            break;
                        }
                        case "movie":
                        {
                            TmdbPaginatedResponse<TmdbMovie>? movies =
                                await searchClient.Movie(parsed.Title ?? "", parsed.Year ?? "");
                            TmdbMovie? movie = movies?.Results.FirstOrDefault();
                            if (movie == null) return;

                            MediaContext context = new();
                            Movie? movieItem = context.Movies
                                .FirstOrDefault(item => item.Id == movie.Id);

                            if (movieItem == null)
                            {
                                TmdbMovieClient movieClient = new(movie.Id);
                                TmdbMovieDetails? details = await movieClient.Details();
                                if (details == null) return;

                                bool hasMovie = context.Movies
                                    .Any(item => item.Id == movie.Id);

                                Ulid libraryId = await context.Libraries
                                    .Where(item => item.Type == libraryType)
                                    .Select(item => item.Id)
                                    .FirstOrDefaultAsync();

                                if (!hasMovie)
                                {
                                    Networking.Networking.SendToAll("Notify", "socket", new NotifyDto
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

                            match = new()
                            {
                                Id = movieItem.Id,
                                Title = movieItem.Title,
                                Still = movieItem.Poster,
                                Duration = mediaAnalysis.Duration,
                                Overview = movieItem.Overview
                            };

                            parsed.ImdbId = movieItem.ImdbId;
                            break;
                        }
                    }

                    string? parentPath = string.IsNullOrEmpty(file.DirectoryName)
                        ? "/"
                        : Path.GetDirectoryName(Path.Combine(file.DirectoryName, ".."));

                    fileList.Add(new()
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
                            Video = mediaAnalysis.VideoStreams
                                .Select(video => new Video
                                {
                                    Index = video.Index,
                                    Width = video.Height,
                                    Height = video.Width
                                }),
                            Audio = mediaAnalysis.AudioStreams
                                .Select(stream => new Audio
                                {
                                    Index = stream.Index,
                                    Language = stream.Language
                                }),
                            Subtitle = mediaAnalysis.SubtitleStreams
                                .Select(stream => new Subtitle
                                {
                                    Index = stream.Index,
                                    Language = stream.Language ?? "und"
                                })
                        },
                    });
                }
                catch (Exception e)
                {
                    Logger.App(e.Message, LogEventLevel.Error);
                }
            });
        }

        return fileList.OrderBy(file => file.Name).ToList();
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
        List<MusicBrainzReleaseAppends> releases = new();
        object lockObject = new();
        await Parallel.ForEachAsync(mediaFiles, async (mediaFile, _) =>
        {
            mediaFile.TagFile ??= TagFile.Create(mediaFile.Path);
            mediaFile.FFprobe ??= FfProbe.Create(mediaFile.Path);
            
            if (mediaFile.TagFile.Tag == null) return;

            if (!string.IsNullOrEmpty(mediaFile.TagFile.Tag.MusicBrainzReleaseId))
            {
                if (prevMusicBrainzReleaseId == mediaFile.TagFile.Tag.MusicBrainzReleaseId)
                {
                    if (year == "0")
                        year = mediaFile.TagFile.Tag.Year.ToString();
                    return;
                }
                Guid musicBrainzReleaseId = Guid.Parse(mediaFile.TagFile.Tag.MusicBrainzReleaseId ?? "");
                if (musicBrainzReleaseId == Guid.Empty) return;
                MusicBrainzReleaseAppends? release =
                    await musicBrainzReleaseClient.WithAllAppends(musicBrainzReleaseId);

                if (release == null || release.Id == Guid.Empty) return;
                prevMusicBrainzReleaseId = release.Id.ToString();
                lock(lockObject)
                {
                    releases.Add(release);
                }
            }
            
            string albumName = mediaFile.TagFile.Tag.Album.Trim();
            string trackName = mediaFile.TagFile.Tag.Title.Trim();
            
            List<Guid> resultIds = await FingerPrint.GetReleaseIds(mediaFile.Path, albumName);
            lock (lockObject)
            {
                lookupReleaseIds.AddRange(resultIds);
                lookupReleaseIds = lookupReleaseIds.Distinct().ToList();
            }

            if (!string.IsNullOrEmpty(trackName))
            {
                await SearchOnRecording(trackName, albumName, mediaFile.TagFile, musicBrainzRecordingClient, lookupReleaseIds);
            }

            if (string.IsNullOrEmpty(albumName) || PrevSearchQueries.Any(x => x == albumName)) return;
            PrevSearchQueries.Add(albumName);
            await SearchOnRelease(albumName, mediaFile.TagFile, musicBrainzReleaseClient, lookupReleaseIds);
        });
        releases = releases
            .Where(x => x.Id != Guid.Empty)
            .DistinctBy(x => x.Id)
            .ToList();
        return (releases, year);
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
                    IsSuccess = true,
                },
                Match = new()
                {
                    Id = bestResult.Id,
                    Title = bestResult.Title,
                    Still = coverPaletteUrl?.ToString(),
                },
                Path = folder,
                Tracks = bestResult.Media.Sum(m => m.TrackCount),
                Streams = new ()
                {
                    Audio =
                    [
                        new Audio
                        {
                            Index = 0,
                            Language = $"Best Match {string.Join(", ", Enumerable.Select<MusicBrainzMedia, string>(bestResult.Media, m => m.Format))}"
                        }
                    ]
                }
            });
        }

        await Parallel.ForEachAsync(releases, async (release, _) =>
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
                    IsSuccess = true,
                },
                Match = new()
                {
                    Id = release.Id,
                    Title = release.Title,
                    Still = coverPaletteUrl?.ToString(),
                },
                Path = folder,
                Tracks = release.Media.Sum(m => m.TrackCount),
                Streams = new ()
                {
                    Audio =
                    [
                        new Audio
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
        await Parallel.ForEachAsync(lookupReleaseIds, async (releaseId, _) =>
        {
            MusicBrainzReleaseAppends? musicBrainzRelease = await musicBrainzReleaseClient.WithAllAppends(releaseId, true);
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

    private static async Task SearchOnRelease(
        string albumName,
        TagFile tagFile,
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        List<Guid> releaseIds
    )
    {
        object lockObject = new();
        string query = $"release:{albumName}";
        if (tagFile.Tag == null) return;
        if (tagFile.Tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tagFile.Tag.FirstAlbumArtist))
        {
            string artistName = !string.IsNullOrEmpty(tagFile.Tag.AlbumArtists[0])
                ? tagFile.Tag.AlbumArtists[0]
                : tagFile.Tag.FirstAlbumArtist;
            query += $" artist:{artistName}";
        }

        if (tagFile.Tag.Year > 0)
        {
            query += $" date:{tagFile.Tag.Year}";
        }
        
        if (PrevSearchQueries.Contains(query))
            return;
        
        PrevSearchQueries.Add(query);
        
        MusicBrainzReleaseSearchResponse? releaseSearchResponse =
            await musicBrainzReleaseClient.SearchReleases(query, true);
                        
        if (releaseSearchResponse == null) return;
        if (releaseSearchResponse.Releases.Length == 0)
        {
            query = $"release:{albumName}";
            if (tagFile.Tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tagFile.Tag.FirstAlbumArtist))
            {
                string artistName = !string.IsNullOrEmpty(tagFile.Tag.AlbumArtists[0])
                    ? tagFile.Tag.AlbumArtists[0]
                    : tagFile.Tag.FirstAlbumArtist;
                query += $" artist:{artistName}";
            }

            releaseSearchResponse = await musicBrainzReleaseClient.SearchReleases(query, true);
            if (releaseSearchResponse == null) return;
            if (releaseSearchResponse.Releases.Length == 0)
                return;
        }
        
        IEnumerable<MusicBrainzRelease> foundReleases = releaseSearchResponse.Releases
            .Where(x => x.Score >= 95)
            .DistinctBy(x => x.Id);
        
        await Parallel.ForEachAsync(foundReleases, (release, _) =>
        {
            lock (lockObject)
            {
                if (release.Id == Guid.Empty || releaseIds.Any(x => x == release.Id)) return ValueTask.CompletedTask;
                releaseIds.Add(release.Id);
            }
            return ValueTask.CompletedTask;
        });
    }

    private static async Task SearchOnRecording(
        string trackName,
        string albumName,
        TagFile tagFile,
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        List<Guid> releaseIds
    )
    {
        object lockObject = new();
        string query = $"recording:{trackName}";
        if (tagFile.Tag == null) return;
        if (tagFile.Tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tagFile.Tag.FirstAlbumArtist))
        {
            string artistName = !string.IsNullOrEmpty(tagFile.Tag.AlbumArtists[0])
                ? tagFile.Tag.AlbumArtists[0]
                : tagFile.Tag.FirstAlbumArtist;
            query += $" artist:{artistName}";
        }

        if (tagFile.Tag.Year > 0)
        {
            query += $" date:{tagFile.Tag.Year}";
        }
        
        if (PrevSearchQueries.Contains(query))
            return;
        
        PrevSearchQueries.Add(query);
        
        MusicBrainzSearchResponse? searchResponse = 
            await musicBrainzRecordingClient.SearchRecordingsDynamic(query, true);

        if (searchResponse == null) return;
        if (searchResponse.Recordings.Count == 0)
        {
            query = $"recording:{trackName}";
            if (tagFile.Tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tagFile.Tag.FirstAlbumArtist))
            {
                string artistName = !string.IsNullOrEmpty(tagFile.Tag.AlbumArtists[0])
                    ? tagFile.Tag.AlbumArtists[0]
                    : tagFile.Tag.FirstAlbumArtist;
                query += $" artist:{artistName}";
            }

            searchResponse = 
                await musicBrainzRecordingClient.SearchRecordingsDynamic(query, true);
            if (searchResponse == null) return;
            if (searchResponse.Recordings.Count == 0)
                return;
        }
        
        IEnumerable<MusicBrainzRelease> foundReleases = searchResponse.Recordings
            .Where(x => x.Score >= 95)
            .SelectMany(x => x.Releases)
            .DistinctBy(x => x.Id);
        
        if (!string.IsNullOrEmpty(albumName))
        {
            foundReleases = foundReleases.Where(r => r.Title.ContainsSanitized(albumName));
        }
        await Parallel.ForEachAsync(foundReleases, (release, _) =>
        {
            lock (lockObject)
            {
                if (release.Id == Guid.Empty || releaseIds.Any(x => x == release.Id)) return ValueTask.CompletedTask;
                releaseIds.Add(release.Id);
            }
            return ValueTask.CompletedTask;
        });
    }

    private static async Task<MusicBrainzReleaseAppends?> GetBestMatchedRelease(
        ConcurrentBag<MediaFile> mediaFiles,
        List<MusicBrainzReleaseAppends> matchedReleases
    )
    {
        MusicBrainzReleaseAppends? bestRelease = null;
        int highestScore = 0;
        object lockObject = new();
        
        await Parallel.ForEachAsync(matchedReleases, (release, _) =>
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

        Parallel.ForEach(release.Media, media =>
        {
            if (media.Tracks.Length == 0 || media.TrackCount == 0)
                return;

            Parallel.ForEach(localFiles, file =>
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
        
        if (string.IsNullOrEmpty(trackTitle) && string.IsNullOrEmpty(fileTitle) && string.IsNullOrEmpty(tagTitle)) return false;
        
        return fileTitle.ContainsSanitized(trackTitle) || 
               tagTitle.ContainsSanitized(trackTitle);
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