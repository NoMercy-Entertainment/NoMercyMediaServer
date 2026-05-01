using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDInfo;
using MediaInfo;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.MediaSources.OpticalMedia.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;
using DirectoryInfo = BDInfo.IO.DirectoryInfo;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;
using Shell = NoMercy.NmSystem.SystemCalls.Shell;
using Stream = System.IO.Stream;

namespace NoMercy.MediaSources.OpticalMedia;

public partial class DriveMonitor
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private static readonly HashSet<string> KnownDrives = [];
    public static List<MetaData> Contents { get; private set; } = [];

    public event Action<string, string?>? OnMediaInserted;
    public event Action<string>? OnMediaEjected;

    private static CancellationTokenSource? CancellationToken { get; set; }

    public static Task Start()
    {
        DriveMonitor driveMonitor = new();

        driveMonitor.OnMediaInserted += async (drive, label) =>
        {
            Logger.Ripper($"Media inserted: {drive} ({label})");

            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new DriveStateChangedEvent
                    {
                        DriveStateData = new DriveState
                        {
                            Open = false,
                            Path = drive.TrimEnd(Path.DirectorySeparatorChar),
                            Label = label,
                            MetaData = null,
                        },
                    }
                );

            MetaData? metaData = label is not null ? await GetDriveMetadata(drive) : null;

            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new DriveStateChangedEvent
                    {
                        DriveStateData = new DriveState
                        {
                            Open = false,
                            Path = drive.TrimEnd(Path.DirectorySeparatorChar),
                            Label = label,
                            MetaData = metaData,
                        },
                    }
                );
        };

        driveMonitor.OnMediaEjected += drive =>
        {
            Logger.Ripper($"Media ejected: {drive}");

            Contents = Contents.Where(c => c.Path != drive).ToList();

            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new DriveStateChangedEvent
                    {
                        DriveStateData = new DriveState
                        {
                            Open = true,
                            Path = drive.TrimEnd(Path.DirectorySeparatorChar),
                        },
                    }
                );
        };

        Task.Run(() => driveMonitor.StartPollingAsync())
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted && t.Exception is not null)
                        Logger.Ripper(
                            $"[FATAL] DriveMonitor polling loop crashed: {t.Exception.GetBaseException().Message}"
                        );
                },
                TaskContinuationOptions.OnlyOnFaulted
            );

        return Task.CompletedTask;
    }

    private async Task StartPollingAsync()
    {
        while (true)
        {
            try
            {
                Dictionary<string, string?> detectedDrives = Optical
                    .GetOpticalDrives()
                    .Where(d => d.Value != null)
                    .ToDictionary(d => d.Key, d => d.Value);

                HashSet<string> currentDrives = new(detectedDrives.Keys);

                foreach ((string drive, string? label) in detectedDrives)
                    if (KnownDrives.Add(drive))
                        OnMediaInserted?.Invoke(drive, label);

                foreach (string knownDrive in KnownDrives.Except(currentDrives).ToList())
                {
                    OnMediaEjected?.Invoke(knownDrive);
                    KnownDrives.Remove(knownDrive);
                    Contents = Contents.Where(c => c.Path != knownDrive).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Ripper($"[ERROR] OpticalDriveMonitor: {ex.Message}");
            }

            await Task.Delay(_pollingInterval);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public static async Task<MetaData?> GetDriveMetadata(string drivePath)
    {
        DirectoryInfo directoryInfo = new(drivePath);
        string path = directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);

        if (Contents.Any(c => c.Path == path))
            return Contents.FirstOrDefault(c => c.Path == path);

        try
        {
            BDROM bDRom = ScanBdRom(directoryInfo);
            string title = TryGetTitle(bDRom);
            string[] titleSegments = Regex.Split(
                title,
                @"[:_]|disc\s*\d+",
                RegexOptions.IgnoreCase
            );

            TmdbSearchClient tmdbSearchClient = new();
            TmdbPaginatedResponse<TmdbMovie>? movieResponse = await tmdbSearchClient.Movie(
                titleSegments[0]
            );
            TmdbPaginatedResponse<TmdbTvShow>? tvShowResponse = await tmdbSearchClient.TvShow(
                titleSegments[0]
            );

            TmdbMovie? movie = movieResponse?.Results.FirstOrDefault();
            TmdbTvShow? tvShow = tvShowResponse?.Results.FirstOrDefault();
            List<TmdbEpisode> episodes = await GetTmdbEpisodes(tvShow, titleSegments);

            string playlistString = Shell.ExecStdErrSync(
                AppFiles.FfProbePath,
                $" -hide_banner -v info -i \"bluray:{path}\""
            );

            List<BluRayPlaylist> bluRayPlaylist = ExtractBluRayPlaylists(
                directoryInfo,
                playlistString
            );

            MetaData cache = new()
            {
                Title = title,
                Path = path,
                BluRayPlaylists = bluRayPlaylist,
                Data = new
                {
                    Movie = movie,
                    TvShow = tvShow,
                    Episodes = episodes,
                },
            };

            Contents.Add(cache);

            return cache;
        }
        catch (Exception ex)
        {
            Logger.Ripper(ex, LogEventLevel.Error);
        }

        return null;
    }

    private static async Task<List<TmdbEpisode>> GetTmdbEpisodes(
        TmdbTvShow? tvShow,
        string[] titleSegments
    )
    {
        List<TmdbEpisode> episodes = [];
        if (tvShow == null)
            return episodes;

        TmdbTvClient tmdbTvClient = new(tvShow.Id);
        TmdbTvShowDetails? tvDetails = await tmdbTvClient.Details();

        foreach (TmdbSeason season in tvDetails?.Seasons ?? [])
        foreach (string segment in titleSegments)
        {
            string seasonName = season.Name!.ToLower();
            string segmentTitle = segment.ToName().Trim().ToLower();
            if (!segmentTitle.Contains(seasonName) && !seasonName.Contains(segmentTitle))
                continue;

            TmdbSeasonClient seasonClient = new(tvShow.Id, season.SeasonNumber);
            episodes.AddRange((await seasonClient.Details())?.Episodes ?? []);
            break;
        }

        if (episodes.Count == 0)
        {
            TmdbSeasonClient seasonClient = new(tvShow.Id, 1);
            episodes.AddRange((await seasonClient.Details())?.Episodes ?? []);
        }
        else if (tvDetails?.Seasons.Any(season => season.SeasonNumber == 0) == true)
        {
            try
            {
                TmdbSeasonClient seasonClient = new(tvShow.Id, 0);
                episodes.AddRange((await seasonClient.Details())?.Episodes ?? []);
            }
            catch (Exception)
            {
                //
            }
        }

        return episodes;
    }

    private static List<BluRayPlaylist> ExtractBluRayPlaylists(
        DirectoryInfo directoryInfo,
        string playlistString
    )
    {
        List<BluRayPlaylist> bluRayPlaylist = [];
        foreach (Match match in PlaylistRegex().Matches(playlistString))
        {
            string mplsPath = Path.Combine(
                directoryInfo.FullName,
                "BDMV",
                "PLAYLIST",
                $"{match.Groups["playlist"].Value}.mpls"
            );
            MediaInfoList mediaList = new(false);
            mediaList.Open(mplsPath, InfoFileOptions.Max);
            bluRayPlaylist.Add(BluRayPlaylist.Parse(mediaList.Inform(0)));
        }

        return bluRayPlaylist;
    }

    // public static async Task<MetaData?> ProcessMedia(string drivePath, MediaProcessingRequest request)
    // {
    //     try
    //     {
    //         DirectoryInfo directoryInfo = new(drivePath);
    //         BDROM bDRom = ScanBdRom(directoryInfo);
    //         string title = TryGetTitle(bDRom);
    //         string[] titleSegments = Regex.Split(title, @"[:_]|disc\s*\d+", RegexOptions.IgnoreCase);
    //
    //         TmdbSearchClient tmdbSearchClient = new();
    //         TmdbPaginatedResponse<TmdbMovie>? movieResponse = await tmdbSearchClient.Movie(titleSegments[0]);
    //         TmdbPaginatedResponse<TmdbTvShow>? tvShowResponse = await tmdbSearchClient.TvShow(titleSegments[0]);
    //
    //         TmdbMovie? movie = movieResponse?.Results.FirstOrDefault();
    //         TmdbTvShow? tvShow = tvShowResponse?.Results.FirstOrDefault();
    //         List<TmdbEpisode> episodes = await GetTmdbEpisodes(tvShow, titleSegments);
    //
    //         string path = directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);
    //         string playlistString = Shell.ExecStdErrSync(AppFiles.FfProbePath, $" -hide_banner -v info -i \"bluray:{path}\"");
    //         await File.WriteAllTextAsync(Path.Combine(AppFiles.TempPath, "bdrom.json"), bDRom.ToJson());
    //
    //         List<BluRayPlaylist> bluRayPlaylist = ExtractBluRayPlaylists(directoryInfo, playlistString);
    //         await ConvertMedia(bluRayPlaylist, title, path);
    //
    //         return new()
    //         {
    //             Title = title,
    //             BluRayPlaylists = bluRayPlaylist,
    //             Data = new { Movie = movie, TvShow = tvShow, Episodes = episodes }
    //         };
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.Ripper(ex, LogEventLevel.Error);
    //     }
    //     return null;
    // }

    public static async Task<MetaData?> ProcessMedia(
        string drivePath,
        MediaProcessingRequest request
    )
    {
        try
        {
            OpticalDiscType discType = Optical.GetDiscType(drivePath);
            string path = drivePath.TrimEnd(Path.DirectorySeparatorChar);

            return discType switch
            {
                OpticalDiscType.BluRay => await ProcessBluRay(drivePath),
                OpticalDiscType.Dvd => await ProcessDvd(drivePath),
                OpticalDiscType.Cd => await ProcessCd(drivePath),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            Logger.Ripper(ex, LogEventLevel.Error);
            return null;
        }
    }

    private static async Task<MetaData?> ProcessBluRay(string drivePath)
    {
        DirectoryInfo directoryInfo = new(drivePath);
        BDROM bDRom = ScanBdRom(directoryInfo);
        string title = TryGetTitle(bDRom);
        string[] titleSegments = Regex.Split(title, @"[:_]|disc\s*\d+", RegexOptions.IgnoreCase);

        TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? movieResponse = await tmdbSearchClient.Movie(
            titleSegments[0]
        );
        TmdbPaginatedResponse<TmdbTvShow>? tvShowResponse = await tmdbSearchClient.TvShow(
            titleSegments[0]
        );

        TmdbMovie? movie = movieResponse?.Results.FirstOrDefault();
        TmdbTvShow? tvShow = tvShowResponse?.Results.FirstOrDefault();
        List<TmdbEpisode> episodes = await GetTmdbEpisodes(tvShow, titleSegments);

        string path = directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);
        string playlistString = Shell.ExecStdErrSync(
            AppFiles.FfProbePath,
            $" -hide_banner -v info -i \"bluray:{path}\""
        );
        await File.WriteAllTextAsync(Path.Combine(AppFiles.TempPath, "bdrom.json"), bDRom.ToJson());

        List<BluRayPlaylist> bluRayPlaylist = ExtractBluRayPlaylists(directoryInfo, playlistString);
        await ConvertMedia(bluRayPlaylist, title, path);

        return new()
        {
            Title = title,
            Path = path,
            BluRayPlaylists = bluRayPlaylist,
            Data = new
            {
                Movie = movie,
                TvShow = tvShow,
                Episodes = episodes,
            },
        };
    }

    private static async Task<MetaData?> ProcessDvd(string drivePath)
    {
        DirectoryInfo directoryInfo = new(drivePath);
        string title = directoryInfo.Name;
        string path = directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);

        string encodePath = Path.Combine(AppFiles.TranscodePath, "ripper");
        Folders.EmptyFolder(encodePath);
        Directory.CreateDirectory(encodePath);

        StringBuilder sb = new();
        sb.Append(" -hide_banner -progress - ");
        sb.Append($" -y -i \"dvd:{drivePath}\" ");
        // Add DVD-specific encoding parameters here

        string command = sb.ToString();
        Logger.Encoder(command);

        // Disc ripping commands are incomplete stubs — needs codec/output selection
        Logger.Encoder($"DVD ripping not yet implemented: {command}");
        await Task.CompletedTask;

        return new() { Title = title, Path = path };
    }

    private static async Task<MetaData?> ProcessCd(string drivePath)
    {
        DirectoryInfo directoryInfo = new(drivePath);
        string title = directoryInfo.Name;
        string path = directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);

        string encodePath = Path.Combine(AppFiles.TranscodePath, "ripper");
        Folders.EmptyFolder(encodePath);
        Directory.CreateDirectory(encodePath);

        StringBuilder sb = new();
        sb.Append(" -hide_banner -progress - ");
        sb.Append($" -y -i \"cd:{drivePath}\" ");
        // Add CD-specific encoding parameters here

        string command = sb.ToString();
        Logger.Encoder(command);

        // Disc ripping commands are incomplete stubs — needs codec/output selection
        Logger.Encoder($"CD ripping not yet implemented: {command}");
        await Task.CompletedTask;

        return new() { Title = title, Path = path };
    }

    private static async Task ConvertMedia(
        List<BluRayPlaylist> bluRayPlaylists,
        string title,
        string path
    )
    {
        // BluRay conversion not yet implemented — needs disc ripping pipeline
        foreach ((BluRayPlaylist playlist, int index) in bluRayPlaylists.Select((p, i) => (p, i)))
        {
            string matchTitle = $"{title} {index + 1}".Replace(":", "");
            Logger.Encoder(
                $"BluRay conversion not yet implemented: playlist {index} → {matchTitle}"
            );
        }
        await Task.CompletedTask;
    }

    private static string GenerateMetadata(BluRayPlaylist playlist, string title)
    {
        StringBuilder sb = new();
        sb.AppendLine(";FFMETADATA1");
        sb.AppendLine($"title={title}");
        sb.AppendLine("");

        foreach ((Chapter chapter, int idx) in playlist.Chapters.Select((c, i) => (c, i)))
        {
            int start = (int)chapter.Timestamp.TotalSeconds;
            int end =
                idx < playlist.Chapters.Count - 1
                    ? (int)playlist.Chapters[idx + 1].Timestamp.TotalSeconds
                    : (int)playlist.Duration.TotalSeconds;
            if (end - start < 5)
                continue;
            sb.AppendLine("[CHAPTER]");
            sb.AppendLine("TIMEBASE=1");
            sb.AppendLine($"START={start}");
            sb.AppendLine($"END={end}");
            sb.AppendLine($"title=Chapter {idx + 1}");
            sb.AppendLine("");
        }

        return sb.ToString();
    }

    private static BDROM ScanBdRom(DirectoryInfo directoryInfo)
    {
        BDROM bDRom = new(directoryInfo);

        bDRom.PlaylistFileScanError += (_, args) =>
        {
            Logger.Ripper(args.Message, LogEventLevel.Error);
            return false;
        };
        bDRom.StreamClipFileScanError += (_, args) =>
        {
            Logger.Ripper(args.Message, LogEventLevel.Error);
            return false;
        };
        bDRom.StreamFileScanError += (_, args) =>
        {
            Logger.Ripper(args.Message, LogEventLevel.Error);
            return false;
        };

        bDRom.Scan();
        return bDRom;
    }

    private static string TryGetTitle(BDROM bDRom)
    {
        string metadataFile = Path.Combine(bDRom.DirectoryMETA.FullName, "DL", "bdmt_eng.xml");
        if (!File.Exists(metadataFile))
            return bDRom.VolumeLabel;

        using Stream stream = File.OpenRead(metadataFile);
        using StreamReader reader = new(stream);
        string xmlContent = reader.ReadToEnd();
        XDocument doc = XDocument.Parse(xmlContent);
        XNamespace di = "urn:BDA:bdmv;discinfo";
        return doc.Descendants(di + "name").FirstOrDefault()?.Value ?? bDRom.VolumeLabel;
    }

    [GeneratedRegex(
        @"\[bluray.*?playlist\s(?<playlist>\d+).mpls\s\((?<duration>\d{1,}:\d{1,}:\d{1,})\)"
    )]
    private static partial Regex PlaylistRegex();

    public static async Task<bool> PlayMedia(
        string drivePath,
        string playlistId,
        CancellationTokenSource token
    )
    {
        if (CancellationToken is not null && CancellationToken.Token.CanBeCanceled)
            await CancellationToken.CancelAsync();

        CancellationToken = token;
        OpticalDiscType discType = Optical.GetDiscType(drivePath);

        return discType switch
        {
            OpticalDiscType.BluRay => await PlayBluRay(drivePath, playlistId, token),
            OpticalDiscType.Dvd => await PlayDvd(drivePath, token),
            OpticalDiscType.Cd => await PlayCd(drivePath, token),
            _ => false,
        };
    }

    private static async Task<bool> PlayBluRay(
        string drivePath,
        string playlistId,
        CancellationTokenSource token
    )
    {
        // Disc playback not yet implemented — needs live transcoding pipeline
        Logger.Encoder(
            $"BluRay playback not yet implemented: playlist {playlistId} from {drivePath}"
        );
        await Task.CompletedTask;
        return true;
    }

    private static async Task<bool> PlayDvd(string drivePath, CancellationTokenSource token)
    {
        string encodePath = Path.Combine(AppFiles.TranscodePath, "ripper");
        Folders.EmptyFolder(encodePath);
        Directory.CreateDirectory(encodePath);

        StringBuilder sb = new();
        sb.Append(" -hide_banner -progress - ");
        sb.Append($" -y -i \"dvd:{drivePath}\" ");
        // Add DVD-specific encoding parameters here

        string command = sb.ToString();
        Logger.Encoder(command);

        // Disc playback not yet implemented — needs live transcoding pipeline
        Logger.Encoder($"DVD playback not yet implemented: {command}");
        await Task.CompletedTask;

        return true;
    }

    private static async Task<bool> PlayCd(string drivePath, CancellationTokenSource token)
    {
        string encodePath = Path.Combine(AppFiles.TranscodePath, "ripper");
        Folders.EmptyFolder(encodePath);
        Directory.CreateDirectory(encodePath);

        StringBuilder sb = new();
        sb.Append(" -hide_banner -progress - ");
        sb.Append($" -y -i \"cd:{drivePath}\" ");
        // Add CD-specific encoding parameters here

        string command = sb.ToString();
        Logger.Encoder(command);

        // Disc playback not yet implemented — needs live transcoding pipeline
        Logger.Encoder($"CD playback not yet implemented: {command}");
        await Task.CompletedTask;

        return true;
    }

    public static async Task<bool> StopMedia()
    {
        if (CancellationToken is null || !CancellationToken.Token.CanBeCanceled)
            return false;

        await CancellationToken.CancelAsync();

        string encodePath = Path.Combine(AppFiles.TranscodePath, "ripper");
        Folders.EmptyFolder(encodePath);

        return true;
    }
}
