using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using MovieFileLibrary;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking.Dto;
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
    public Task StoreVideoFile(VideoFile videoFile)
    {
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

        return await context.Episodes
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
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

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
            
            Parallel.ForEach(audioFiles, (file) =>
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
                    File = Path.Combine(directoryPath, file.FullName)
                });
            });
        }
        else if (videoFiles.Length > 0)
        {
            foreach (FileInfo file in videoFiles)
            {
                try
                {
                    MovieOrEpisode match = new();
                    TmdbSearchClient searchClient = new();
                    MovieDetector movieDetector = new();
                    
                    string title = file.FullName.Replace("v2", "");
                    // remove any text in square brackets that may cause year to match incorrectly
                    title = Str.RemoveBracketedString().Replace(title, string.Empty);
                    
                    IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file.FullName);

                    MovieFile parsed = movieDetector.GetInfo(title);
                    
                    parsed.Year ??= title.TryGetYear();


                    if (parsed.Title == null) continue;

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
                            if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) continue;

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
                                if (details == null) continue;

                                Season? season = await context.Seasons
                                    .FirstOrDefaultAsync(season => season.TvId == show.Id && season.SeasonNumber == details.SeasonNumber);

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
                            if (movie == null) continue;

                            Movie? movieItem = context.Movies
                                .FirstOrDefault(item => item.Id == movie.Id);

                            if (movieItem == null)
                            {
                                TmdbMovieClient movieClient = new(movie.Id);
                                TmdbMovieDetails? details = await movieClient.Details();
                                if (details == null) continue;

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
                        File = file.FullName,
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
                } catch (Exception e)
                {
                    Logger.App(e.Message, LogEventLevel.Error);
                }
            }
        }

        return fileList.OrderBy(file => file.Name).ToList();
    }

    public static async Task<List<FileItem>> GetMusicBrainzReleasesInDirectory(string folder)
    {
        DirectoryInfo directoryInfo = new(folder);
        FileInfo[] audioFiles = directoryInfo.GetFiles()
            .Where(file => file.Extension is ".mp3" or ".flac" or ".wav" or ".m4a")
            .ToArray();
        
        Dictionary<Guid,(MusicBrainzReleaseAppends release, int count)> musicBrainzReleasesDic = new();

        AcoustIdFingerprintClient acoustIdFingerprintClient = new();
        
        foreach (FileInfo file in audioFiles)
        {
            AcoustIdFingerprint? fingerprint = await acoustIdFingerprintClient.Lookup(file.FullName, priority: true);
            
            if (fingerprint is null) continue;
            foreach (AcoustIdFingerprintResult acoustIdFingerprint in fingerprint.Results)
            {
                if (acoustIdFingerprint.Id == Guid.Empty) continue;
                foreach (AcoustIdFingerprintRecording? acoustIdFingerprintRecording in acoustIdFingerprint.Recordings ?? [])
                {
                    if (acoustIdFingerprintRecording is null) continue;
                    if (acoustIdFingerprintRecording.Id == Guid.Empty) continue;
                    if (acoustIdFingerprintRecording.Releases is null) continue;

                    foreach (AcoustIdFingerprintReleaseGroups release in acoustIdFingerprintRecording.Releases ?? [])
                    {
                        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
                        MusicBrainzReleaseAppends? item = await musicBrainzReleaseClient.WithAllAppends(release.Id, true);
                        if (item == null) continue;
                        if (musicBrainzReleasesDic.ContainsKey(item.Id))
                        {
                            (MusicBrainzReleaseAppends release, int count) tmp = musicBrainzReleasesDic[item.Id];
                            tmp.count++;
                            musicBrainzReleasesDic[item.Id] = tmp;
                            continue;
                        }
                        musicBrainzReleasesDic.Add(item.Id, (item, 1));
                    }
                }
            }
        }

        List<MusicBrainzReleaseAppends> musicBrainzReleases = musicBrainzReleasesDic.Values
            .OrderByDescending(x => x.count)
            .Select(x => x.release)
            .ToList();
    
        if (musicBrainzReleases.Count == 0)
            throw new("No releases found");

        List<FileItem> files = [];

        foreach (MusicBrainzReleaseAppends release in musicBrainzReleases)
        {
            Uri? coverPaletteUrl = await CoverArtImageManagerManager.GetCoverUrl(release.Id, true);
            
            files.Add(new()
            {
                Size = audioFiles.Sum(x => x.Length),
                Mode = 0,
                Name = release.Title,
                Parent = folder,
                Parsed = new(folder)
                {
                    Title = release.Title,
                    Year = release.DateTime.ParseYear().ToString(),
                    IsSeries = false,
                    IsSuccess = true,
                },
                Match = new()
                {
                    Id = release.Id,
                    Title = release.Title,
                    Still = coverPaletteUrl?.ToString(),
                },
                File = folder,
                Tracks = release.Media.Sum(m => m.TrackCount)
            });
        }

        return files;
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