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
                    Path = Path.Combine(directoryPath, file.FullName)
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
        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        
        List<TagLib.File> fileTagList = new();
        
        await Parallel.ForEachAsync(audioFiles, (file ,_) =>
        {
            TagLib.File tagFile = TagLib.File.Create(file.FullName);
            if (tagFile.Tag == null || string.IsNullOrEmpty(tagFile.Tag.Album)) return ValueTask.CompletedTask;
            fileTagList.Add(tagFile);
            return ValueTask.CompletedTask;
        });

        if (fileTagList.Count > 0)
        {
            List<TagLib.Tag> albumTags = fileTagList.DistinctBy(x => x.Tag.Album).Select(x => x.Tag).ToList();
            await Parallel.ForEachAsync(albumTags, async (tag, t) =>
            {
                List<MusicBrainzRelease> releases = [];
                if (!string.IsNullOrEmpty(tag.MusicBrainzReleaseId))
                {
                    MusicBrainzReleaseAppends? release =
                        await musicBrainzReleaseClient.WithAllAppends(tag.MusicBrainzReleaseId.ToGuid());
                    if (release == null) return;
                    releases.Add(release);
                }
                else
                {
                    string albumName = tag.Album;
                    string query = $"release:{albumName}";
                    if (tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tag.FirstAlbumArtist))
                    {
                        string artistName = !string.IsNullOrEmpty(tag.AlbumArtists[0])
                            ? tag.AlbumArtists[0]
                            : tag.FirstAlbumArtist;
                        query += $" artist:{artistName}";
                    }

                    if (tag.Year > 0)
                    {
                        query += $" date:{tag.Year}";
                    }

                    MusicBrainzReleaseSearchResponse? releaseSearchResponse =
                        await musicBrainzReleaseClient.SearchReleases(query, true);
                    if (releaseSearchResponse == null) return;
                    if (releaseSearchResponse.Releases.Length == 0)
                    {
                        query = $"release:{albumName}";
                        if (tag.AlbumArtists.Length > 0 || !string.IsNullOrEmpty(tag.FirstAlbumArtist))
                        {
                            string artistName = !string.IsNullOrEmpty(tag.AlbumArtists[0])
                                ? tag.AlbumArtists[0]
                                : tag.FirstAlbumArtist;
                            query += $" artist:{artistName}";
                        }

                        releaseSearchResponse = await musicBrainzReleaseClient.SearchReleases(query, true);
                        if (releaseSearchResponse == null) return;
                        if (releaseSearchResponse.Releases.Length == 0)
                            return;
                    }

                    releases.AddRange(releaseSearchResponse.Releases
                        .Where(x => x.Score >= 95));
                }

                await Parallel.ForEachAsync(releases, t, async (release, _) =>
                {
                    MusicBrainzReleaseAppends? releaseAppends =
                        await musicBrainzReleaseClient.WithAllAppends(release.Id, true);
                    if (releaseAppends == null) return;
                    if (musicBrainzReleasesDic.TryGetValue(releaseAppends.Id,
                            out (MusicBrainzReleaseAppends release, int count) tmp))
                    {
                        tmp.count++;
                        musicBrainzReleasesDic[releaseAppends.Id] = tmp;
                        return;
                    }

                    musicBrainzReleasesDic.Add(releaseAppends.Id, (releaseAppends, 1));
                });
            });
        }
        else
        {
            await Parallel.ForEachAsync(audioFiles, async (file, _) =>
            {
                await Fingerprint(acoustIdFingerprintClient, file, musicBrainzReleaseClient, musicBrainzReleasesDic);
            });
        }
        
        if (musicBrainzReleasesDic.Count == 0)
        {
            await Parallel.ForEachAsync(audioFiles, async (file, t) =>
            {
                MusicBrainzSearchResponse? searchResponse;

                TagLib.File tagFile = TagLib.File.Create(file.FullName);

                if (tagFile.Tag == null || string.IsNullOrEmpty(tagFile.Tag.Title))
                    searchResponse =
                        await musicBrainzRecordingClient.SearchRecordingsDynamic(file.Name.Replace(file.Extension, ""));
                else
                    searchResponse = await musicBrainzRecordingClient.SearchRecordingsDynamic(tagFile.Tag.Title);

                if (searchResponse == null) return;
                if (searchResponse.Recordings.Count == 0) return;
                searchResponse.Recordings = searchResponse.Recordings.Where(x => x.Score >= 95).ToList();
                await Parallel.ForEachAsync(searchResponse.Recordings, t,async (recording, y) =>
                {
                    await Parallel.ForEachAsync(recording.Releases, y,async (recordingRelease, _) =>
                    {
                        MusicBrainzReleaseAppends? release =
                            await musicBrainzReleaseClient.WithAllAppends(recordingRelease.Id, true);
                        if (release == null) return;
                        if (musicBrainzReleasesDic.TryGetValue(release.Id,
                                out (MusicBrainzReleaseAppends release, int count) tmp))
                        {
                            tmp.count++;
                            musicBrainzReleasesDic[release.Id] = tmp;
                            return;
                        }

                        musicBrainzReleasesDic.Add(release.Id, (release, 1));
                    });
                });
            });
        }
            
        List<MusicBrainzReleaseAppends> musicBrainzReleases = musicBrainzReleasesDic.Values
            .OrderByDescending(x => x.count)
            .Select(x => x.release)
            .ToList();
    
        if (musicBrainzReleases.Count == 0)
            return [];

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
                Path = folder,
                Tracks = release.Media.Sum(m => m.TrackCount)
            });
        }

        return files;
    }

    private static async Task Fingerprint(AcoustIdFingerprintClient acoustIdFingerprintClient, FileInfo file,
        MusicBrainzReleaseClient musicBrainzReleaseClient, Dictionary<Guid, (MusicBrainzReleaseAppends release, int count)> musicBrainzReleasesDic)
    {
        AcoustIdFingerprint? fingerprint = await acoustIdFingerprintClient.Lookup(file.FullName, priority: true);
        if (fingerprint is null) return;
        await Parallel.ForEachAsync(fingerprint.Results, async (acoustIdFingerprint, t) =>
        {
            if (acoustIdFingerprint.Id == Guid.Empty) return;
            await Parallel.ForEachAsync(acoustIdFingerprint.Recordings ?? [], t,
                async (acoustIdFingerprintRecording, y) =>
                {
                    if (acoustIdFingerprintRecording is null) return;
                    if (acoustIdFingerprintRecording.Id == Guid.Empty) return;
                    if (acoustIdFingerprintRecording.Releases is null) return;
                    await Parallel.ForEachAsync(acoustIdFingerprintRecording.Releases ?? [], y,
                        async (fingerprintRelease, _) =>
                        {
                            MusicBrainzReleaseAppends? release =
                                await musicBrainzReleaseClient.WithAllAppends(fingerprintRelease.Id, true);
                            if (release == null) return;
                            if (musicBrainzReleasesDic.ContainsKey(release.Id))
                            {
                                (MusicBrainzReleaseAppends release, int count) tmp = musicBrainzReleasesDic[release.Id];
                                tmp.count++;
                                musicBrainzReleasesDic[release.Id] = tmp;
                                return;
                            }

                            musicBrainzReleasesDic.Add(release.Id, (release, 1));
                        });
                });
        });
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