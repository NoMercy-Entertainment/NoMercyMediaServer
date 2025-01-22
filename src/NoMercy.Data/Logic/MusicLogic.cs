using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoMercy.Data.Jobs;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;
using Serilog.Events;

namespace NoMercy.Data.Logic;

public partial class MusicLogic : IAsyncDisposable
{
    private AcoustIdFingerprint? FingerPrint { get; set; }
    private ConcurrentBag<MediaFile>? Files { get; set; }
    private MediaFolderExtend ListPath { get; set; }

    private int Year { get; set; }
    private string AlbumName { get; set; }
    private string ArtistName { get; set; }
    private Library Library { get; set; }
    private Folder? Folder { get; set; }

    public MusicLogic(Library library, MediaFolderExtend listPath)
    {
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        Library = library;
        ListPath = listPath;

        Files = listPath.Files;

        Match match = PathRegex().Match(listPath.Path);
        ArtistName = match.Groups["artist"].Success ? match.Groups["artist"].Value : string.Empty;
        AlbumName = match.Groups["album"].Success ? match.Groups["album"].Value : string.Empty;
        Year = match.Groups["year"].Success ? Convert.ToInt32(match.Groups["year"].Value) : 1970;

        string libraryFolder = (match.Groups["library_folder"].Success ? match.Groups["library_folder"].Value : null) ??
                               string.Empty;

        Folder = Library.FolderLibraries
            .Select(folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(folder => folder.Path == libraryFolder);

        Logger.App("Files", LogEventLevel.Verbose);
        Logger.App(Files, LogEventLevel.Verbose);

        Logger.App("ArtistName: " + ArtistName, LogEventLevel.Verbose);
        Logger.App("AlbumName " + AlbumName, LogEventLevel.Verbose);
        Logger.App("Year: " + Year, LogEventLevel.Verbose);

        Logger.App("Folder: " + Folder?.Path, LogEventLevel.Verbose);
    }

    public async Task Process()
    {
        Logger.App($"Processing Folder: {Folder?.Path}", LogEventLevel.Verbose);
        await Parallel.ForEachAsync(Files ?? [], async (file, ct) =>
        {
            try
            {
                Logger.App($"Analyzing File: {file.Name}", LogEventLevel.Debug);
                IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file.Path, cancellationToken: ct);

                AcoustIdFingerprintRecording? fingerPrintRecording = await MatchTrack(file, mediaAnalysis);
                if (fingerPrintRecording is not null)
                {
                    foreach (AcoustIdFingerprintReleaseGroups release in fingerPrintRecording.Releases ?? [])
                    {
                        if (release.TrackCount == null || release.TrackCount != Files?.Count)
                        {
                            Logger.App($"Track Count Mismatch: {release.Title}", LogEventLevel.Verbose);
                            return;
                        }

                        try
                        {
                            await ProcessRelease(release, file);
                        }
                        catch (Exception e)
                        {
                            if (e.Message.Contains("404")) return;
                            Logger.App(e.Message, LogEventLevel.Error);
                        }
                    }

                    return;
                }

                AcoustIdFingerprintReleaseGroups? fallbackParsedResult = FallbackParser(file, mediaAnalysis);
                if (fallbackParsedResult is null) return;

                await ProcessRelease(fallbackParsedResult, file);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("404")) return;
                Logger.App(e.Message, LogEventLevel.Error);
            }
        });
    }

    private async Task ProcessRelease(AcoustIdFingerprintReleaseGroups release, MediaFile mediaFile)
    {
        Logger.App($"Processing release: {release.Title} with id: {release.Id}", LogEventLevel.Verbose);

        using MusicBrainzReleaseClient musicBrainzReleaseClient = new(release.Id);

        MusicBrainzReleaseAppends? releaseAppends = await musicBrainzReleaseClient.WithAllAppends();

        if (releaseAppends is null || releaseAppends.Title.IsNullOrEmpty())
        {
            Logger.App($"Release not found: {release.Title}", LogEventLevel.Warning);
            await Task.CompletedTask;
            return;
        }

        if (await StoreReleaseGroups(releaseAppends) is null)
            Logger.App($"Release Group already exists: {releaseAppends.MusicBrainzReleaseGroup.Title}",
                LogEventLevel.Verbose);
        // await Task.CompletedTask;
        // return;
        else
            Logger.App($"Processing release: {release.Title} with id: {release.Id}", LogEventLevel.Debug);

        if (await StoreRelease(releaseAppends, mediaFile) is null)
            Logger.App($"Release already exists: {releaseAppends.Title}", LogEventLevel.Verbose);
        // await Task.CompletedTask;
        // return;
        await LinkReleaseToReleaseGroup(releaseAppends);
        await LinkReleaseToLibrary(releaseAppends);

        foreach (MusicBrainzMedia media in releaseAppends.Media)
        foreach (MusicBrainzTrack track in media.Tracks)
        {
            if (await StoreTrack(releaseAppends, track, media, mediaFile) is null) continue;

            await LinkTrackToRelease(track, releaseAppends);

            foreach (ReleaseArtistCredit artist in track.ArtistCredit)
            {
                await StoreArtist(artist.MusicBrainzArtist);
                await LinkArtistToTrack(artist.MusicBrainzArtist, track);

                await LinkArtistToAlbum(artist.MusicBrainzArtist, releaseAppends);
                await LinkArtistToLibrary(artist.MusicBrainzArtist);

                await LinkArtistToReleaseGroup(releaseAppends, artist.MusicBrainzArtist.Id);
            }
        }

        await Task.CompletedTask;
    }

    private async Task<AcoustIdFingerprintRecording?> MatchTrack(MediaFile file, IMediaAnalysis mediaAnalysis)
    {
        Logger.App($"Matching Track: {file.Name}", LogEventLevel.Verbose);

        try
        {
            AcoustIdFingerprintClient acoustIdFingerprintClient = new();
            FingerPrint = await acoustIdFingerprintClient.Lookup(file.Path);
            acoustIdFingerprintClient.Dispose();
            if (FingerPrint is null) return null;
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            throw;
        }

        AcoustIdFingerprintRecording? fingerPrintRecording = null;

        foreach (AcoustIdFingerprintResult fingerPrint in FingerPrint?.Results ?? [])
        {
            Logger.App($"Matching Recording: {fingerPrint.Id}", LogEventLevel.Verbose);
            foreach (AcoustIdFingerprintRecording? recording in fingerPrint.Recordings ?? [])
            {
                if (recording?.Releases is null) continue;

                fingerPrintRecording = MatchRelease(file, recording, mediaAnalysis);

                if (fingerPrintRecording is not null) break;

                fingerPrintRecording = MatchRelease(file, recording, mediaAnalysis, false);
            }
        }

        return fingerPrintRecording;
    }

    private AcoustIdFingerprintReleaseGroups? FallbackParser(MediaFile file, IMediaAnalysis mediaAnalysis)
    {
        Logger.App($"Fallback Parser: {file.Name}", LogEventLevel.Verbose);
        string? albumId = mediaAnalysis.Format.Tags?.FirstOrDefault(t => t.Key == "MusicBrainz Album Id").Value;

        Logger.App($"AlbumId: {albumId}", LogEventLevel.Verbose);

        if (albumId is null) return null;

        return new()
        {
            Id = albumId.ToGuid()
        };
    }

    private AcoustIdFingerprintRecording? MatchRelease(MediaFile file, AcoustIdFingerprintRecording? recording,
        IMediaAnalysis mediaAnalysis, bool strictMatch = true)
    {
        Logger.App($"Matching Release: {recording?.Title}", LogEventLevel.Verbose);
        if (recording is null) return null;

        AcoustIdFingerprintRecording? fingerPrintRecording = null;

        foreach (AcoustIdFingerprintReleaseGroups release in recording.Releases ?? [])
        {
            bool matchesTrackCount = release.TrackCount != null && release.TrackCount == Files?.Count;
            if (!matchesTrackCount) continue;

            string fileNameSanitized = file.Parsed?.Title?.RemoveDiacritics().RemoveNonAlphaNumericCharacters() ??
                                       string.Empty;
            string recordNameSanitized =
                recording.Title?.RemoveDiacritics().RemoveNonAlphaNumericCharacters() ?? string.Empty;
            bool matchesName = !fileNameSanitized.Equals(string.Empty)
                               && !recordNameSanitized.Equals(string.Empty)
                               && fileNameSanitized.Contains(recordNameSanitized);

            // var mediaAnalysis = FFProbe.AnalyseAsync(file.Path).Result;
            double fileDuration = mediaAnalysis.Format.Duration.TotalSeconds;
            int recordDuration = recording.Duration;
            bool matchesDuration = fileDuration > 0
                                   && recordDuration > 0
                                   && Math.Abs(recordDuration - fileDuration) < 10;

            if (strictMatch && matchesName && matchesDuration)
            {
                fingerPrintRecording = recording;
                break;
            }

            if (!matchesName && !matchesDuration) continue;
            fingerPrintRecording = recording;
            break;
        }

        return fingerPrintRecording;
    }

    private static string MakeArtistFolder(string artist)
    {
        string artistName = artist.RemoveDiacritics();

        string artistFolder = char.IsNumber(artistName[0])
            ? "#"
            : artistName[0].ToString().ToUpper();

        return $"/{artistFolder}/{artistName}";
    }


    private async Task<MusicBrainzReleaseAppends?> StoreReleaseGroups(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        Logger.App($"Storing Release Group: {musicBrainzRelease.MusicBrainzReleaseGroup.Title}", LogEventLevel.Verbose);
        MediaContext mediaContext = new();
        bool hasReleaseGroup = mediaContext.ReleaseGroups
            .AsNoTracking()
            .Any(r => r.Id == musicBrainzRelease.MusicBrainzReleaseGroup.Id);

        if (hasReleaseGroup) return null;

        ReleaseGroup insert = new()
        {
            Id = musicBrainzRelease.MusicBrainzReleaseGroup.Id,
            Title = musicBrainzRelease.MusicBrainzReleaseGroup.Title,
            Description = musicBrainzRelease.MusicBrainzReleaseGroup.Disambiguation.IsNullOrEmpty()
                ? null
                : musicBrainzRelease.MusicBrainzReleaseGroup.Disambiguation,
            Year = musicBrainzRelease.MusicBrainzReleaseGroup.FirstReleaseDate.ParseYear(),
            LibraryId = Library.Id
        };

        try
        {
            await mediaContext.ReleaseGroups.Upsert(insert)
                .On(e => new { e.Id })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    Year = i.Year,
                    LibraryId = i.LibraryId,
                    UpdatedAt = i.UpdatedAt
                })
                .RunAsync();

            foreach (MusicBrainzGenreDetails genre in musicBrainzRelease.MusicBrainzReleaseGroup.Genres ?? [])
                await LinkGenreToReleaseGroup(musicBrainzRelease.MusicBrainzReleaseGroup, genre);

            MusicDescriptionJob musicDescriptionJob = new(musicBrainzRelease.MusicBrainzReleaseGroup);
            JobDispatcher.Dispatch(musicDescriptionJob, "data", 2);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return null;
        }

        Logger.App($"Release Group stored: {musicBrainzRelease.MusicBrainzReleaseGroup.Title}", LogEventLevel.Verbose);
        return musicBrainzRelease;
    }

    private async Task<MusicBrainzReleaseAppends?> StoreRelease(MusicBrainzReleaseAppends musicBrainzRelease,
        MediaFile mediaFile)
    {
        Logger.App($"Storing Release: {musicBrainzRelease.Title}", LogEventLevel.Verbose);
        MusicBrainzMedia? media = musicBrainzRelease.Media.FirstOrDefault(m => m.Tracks.Length > 0);
        if (media is null) return null;

        MediaContext mediaContext = new();
        bool hasAlbum = mediaContext.Albums
            .AsNoTracking()
            .Any(a => a.Id == musicBrainzRelease.Id && a.Cover != null);

        if (hasAlbum) return musicBrainzRelease;

        string folder = mediaFile.Parsed?.FilePath.Replace(Path.DirectorySeparatorChar + mediaFile.Name, "") ??
                        string.Empty;

        Album insert = new()
        {
            Id = musicBrainzRelease.Id,
            Name = musicBrainzRelease.Title,
            Country = musicBrainzRelease.Country,
            Disambiguation = musicBrainzRelease.Disambiguation.IsNullOrEmpty()
                ? null
                : musicBrainzRelease.Disambiguation,
            Year = musicBrainzRelease.DateTime?.ParseYear() ??
                   musicBrainzRelease.ReleaseEvents?.FirstOrDefault()?.DateTime?.ParseYear() ?? 0,
            Tracks = media.Tracks.Length,

            LibraryId = Library.Id,
            FolderId = Folder!.Id,
            Folder = folder.Replace(Folder.Path, "")
                .Replace("\\", "/"),
            HostFolder = folder.PathName()
        };

        try
        {
            await mediaContext.Albums.Upsert(insert)
                .On(e => new { e.Id })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    Name = i.Name,
                    Disambiguation = i.Disambiguation,
                    Description = i.Description,
                    Year = i.Year,
                    Country = i.Country,
                    Tracks = i.Tracks,

                    LibraryId = i.LibraryId,
                    Folder = i.Folder,
                    FolderId = i.FolderId,
                    HostFolder = i.HostFolder,
                    UpdatedAt = i.UpdatedAt
                })
                .RunAsync();

            foreach (MusicBrainzGenreDetails genre in musicBrainzRelease.Genres)
                await LinkGenreToRelease(musicBrainzRelease, genre);

            CoverArtImageJob coverArtImageJob = new(musicBrainzRelease);
            JobDispatcher.Dispatch(coverArtImageJob, "image", 3);

            FanArtImagesJob fanartImagesJob = new(musicBrainzRelease);
            JobDispatcher.Dispatch(fanartImagesJob, "image", 2);

            Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
            {
                QueryKey = ["music", "albums", musicBrainzRelease.Id.ToString()]
            });
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return null;
        }

        Logger.App($"Release stored: {musicBrainzRelease.Title}", LogEventLevel.Verbose);

        return musicBrainzRelease;
    }

    private async Task StoreArtist(MusicBrainzArtistDetails musicBrainzArtist)
    {
        Logger.App($"Processing Artist: {musicBrainzArtist.Name}", LogEventLevel.Verbose);
        MediaContext mediaContext = new();
        bool hasArtist = mediaContext.Artists
            .AsNoTracking()
            .Any(a => a.Id == musicBrainzArtist.Id);

        if (hasArtist) return;

        string artistFolder = MakeArtistFolder(musicBrainzArtist.Name);
        Artist insert = new()
        {
            Id = musicBrainzArtist.Id,
            Name = musicBrainzArtist.Name,
            Disambiguation = musicBrainzArtist.Disambiguation.IsNullOrEmpty() ? null : musicBrainzArtist.Disambiguation,
            Country = musicBrainzArtist.Country,
            TitleSort = musicBrainzArtist.SortName,

            Folder = artistFolder,
            HostFolder = Path.Join(Library.FolderLibraries.FirstOrDefault()?.Folder.Path, artistFolder).PathName(),
            LibraryId = Library.Id,
            FolderId = Folder!.Id
        };

        try
        {
            await mediaContext.Artists.Upsert(insert)
                .On(e => new { e.Id })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    Name = i.Name,
                    Disambiguation = i.Disambiguation,
                    Description = i.Description,

                    Folder = i.Folder,
                    HostFolder = i.HostFolder,
                    LibraryId = i.LibraryId,
                    FolderId = i.FolderId,
                    UpdatedAt = i.UpdatedAt
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return;
        }

        try
        {
            foreach (MusicBrainzGenreDetails genre in musicBrainzArtist.Genres ?? [])
                await LinkGenreToArtist(musicBrainzArtist, genre);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
        }

        MusicDescriptionJob musicDescriptionJob = new(musicBrainzArtist);
        JobDispatcher.Dispatch(musicDescriptionJob, "data", 2);

        FanArtImagesJob fanartImagesJob = new(musicBrainzArtist);
        JobDispatcher.Dispatch(fanartImagesJob, "image", 2);

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "artists", musicBrainzArtist.Id.ToString()]
        });
    }

    private async Task<MusicBrainzTrack?> StoreTrack(MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzTrack musicBrainzTrack, MusicBrainzMedia musicBrainzMedia, MediaFile mediaFile)
    {
        Logger.App($"Processing Track: {musicBrainzTrack.Title}", LogEventLevel.Verbose);
        MediaContext mediaContext = new();
        bool hasTrack = mediaContext.Tracks
            .AsNoTracking()
            .Any(t => t.Id == musicBrainzTrack.Id && t.Filename != null && t.Duration != null);

        if (hasTrack) return null;

        Track insert = new()
        {
            Id = musicBrainzTrack.Id,
            Name = musicBrainzTrack.Title,
            Date = musicBrainzRelease.DateTime ?? musicBrainzRelease.ReleaseEvents?.FirstOrDefault()?.DateTime,
            DiscNumber = musicBrainzMedia.Position,
            TrackNumber = musicBrainzTrack.Position
        };

        string? file = FileMatch(musicBrainzRelease, musicBrainzMedia, insert);

        if (file is not null)
        {
            Logger.App($"File Match: {file}", LogEventLevel.Verbose);
            IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file);
            string folder = mediaFile.Parsed?.FilePath.Replace(Path.DirectorySeparatorChar + mediaFile.Name, "") ??
                            string.Empty;

            insert.Filename = "/" + Path.GetFileName(file);
            insert.Quality = (int)Math.Floor(mediaAnalysis.Format.BitRate / 1000.0);
            insert.Duration =
                HmsRegex().Replace(mediaAnalysis.Duration.ToString("hh\\:mm\\:ss"), "");

            insert.FolderId = Folder!.Id;
            insert.Folder = folder.Replace(Folder.Path, "").Replace("\\", "/");
            insert.HostFolder = folder.PathName();
        }

        try
        {
            await mediaContext.Tracks.Upsert(insert)
                .On(e => new { e.Id })
                .WhenMatched((ts, ti) => new()
                {
                    Id = ti.Id,
                    Name = ti.Name,
                    DiscNumber = ti.DiscNumber,
                    TrackNumber = ti.TrackNumber,
                    Date = ti.Date,
                    UpdatedAt = ti.UpdatedAt,

                    Folder = file == "" ? ts.Folder : ti.Folder,
                    FolderId = file == "" ? ts.FolderId : ti.FolderId,
                    HostFolder = file == "" ? ts.HostFolder : ti.HostFolder,
                    Duration = file == "" ? ts.Duration : ti.Duration,
                    Filename = file == "" ? ts.Filename : ti.Filename,
                    Quality = file == "" ? ts.Quality : ti.Quality
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return null;
        }

        try
        {
            foreach (MusicBrainzGenreDetails genre in musicBrainzTrack.Genres ?? [])
                await LinkGenreToTrack(musicBrainzTrack, genre);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return null;
        }

        Logger.App($"Track stored: {musicBrainzTrack.Title}", LogEventLevel.Verbose);
        return musicBrainzTrack;
    }

    private string? FileMatch(MusicBrainzReleaseAppends musicBrainzRelease, MusicBrainzMedia musicBrainzMedia,
        Track track)
    {
        string? file = FindTrackWithAlbumNumberByNumberPadded(musicBrainzMedia, null, musicBrainzRelease.Media.Length,
            track.TrackNumber, 4);
        file = FindTrackWithAlbumNumberByNumberPadded(musicBrainzMedia, file, musicBrainzRelease.Media.Length,
            track.TrackNumber, 3);
        file = FindTrackWithAlbumNumberByNumberPadded(musicBrainzMedia, file, musicBrainzRelease.Media.Length,
            track.TrackNumber);

        file = FindTrackWithoutAlbumNumberByNumberPadded(musicBrainzMedia, file, musicBrainzRelease.Media.Length,
            track.TrackNumber, 4);
        file = FindTrackWithoutAlbumNumberByNumberPadded(musicBrainzMedia, file, musicBrainzRelease.Media.Length,
            track.TrackNumber, 3);
        file = FindTrackWithoutAlbumNumberByNumberPadded(musicBrainzMedia, file, musicBrainzRelease.Media.Length,
            track.TrackNumber);

        return file;
    }

    private async Task LinkReleaseToReleaseGroup(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        Logger.App($"Linking Release to Release Group: {musicBrainzRelease.MusicBrainzReleaseGroup.Title}",
            LogEventLevel.Verbose);
        AlbumReleaseGroup insert = new()
        {
            AlbumId = musicBrainzRelease.Id,
            ReleaseGroupId = musicBrainzRelease.MusicBrainzReleaseGroup.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.AlbumReleaseGroup.Upsert(insert)
            .On(e => new { e.AlbumId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    private async Task LinkArtistToReleaseGroup(MusicBrainzReleaseAppends musicBrainzRelease, Guid artistId)
    {
        Logger.App($"Linking Artist to Release Group: {musicBrainzRelease.MusicBrainzReleaseGroup.Title}",
            LogEventLevel.Verbose);
        ArtistReleaseGroup insert = new()
        {
            ArtistId = artistId,
            ReleaseGroupId = musicBrainzRelease.MusicBrainzReleaseGroup.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.ArtistReleaseGroup.Upsert(insert)
            .On(e => new { e.ArtistId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new()
            {
                ArtistId = i.ArtistId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    private async Task LinkReleaseToLibrary(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        Logger.App($"Linking Release to Library: {musicBrainzRelease.Title}", LogEventLevel.Verbose);
        AlbumLibrary insert = new()
        {
            AlbumId = musicBrainzRelease.Id,
            LibraryId = Library.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.AlbumLibrary.Upsert(insert)
            .On(e => new { e.AlbumId, e.LibraryId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                LibraryId = i.LibraryId
            })
            .RunAsync();
    }

    private async Task LinkArtistToLibrary(MusicBrainzArtist musicBrainzArtistMusicBrainzArtist)
    {
        Logger.App($"Linking Artist to Library: {musicBrainzArtistMusicBrainzArtist.Name}", LogEventLevel.Verbose);
        ArtistLibrary insert = new()
        {
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id,
            LibraryId = Library.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.ArtistLibrary.Upsert(insert)
            .On(e => new { e.ArtistId, e.LibraryId })
            .WhenMatched((s, i) => new()
            {
                ArtistId = i.ArtistId,
                LibraryId = i.LibraryId
            })
            .RunAsync();
    }

    private async Task LinkTrackToRelease(MusicBrainzTrack? track, MusicBrainzReleaseAppends? release)
    {
        Logger.App($"Linking Track to Release: {track?.Title}", LogEventLevel.Verbose);
        if (track == null || release == null) return;

        AlbumTrack insert = new()
        {
            AlbumId = release.Id,
            TrackId = track.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.AlbumTrack.Upsert(insert)
            .On(e => new { e.AlbumId, e.TrackId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                TrackId = i.TrackId
            })
            .RunAsync();
    }

    private async Task LinkArtistToAlbum(MusicBrainzArtist musicBrainzArtistMusicBrainzArtist,
        MusicBrainzReleaseAppends musicBrainzRelease)
    {
        Logger.App($"Linking Artist to Album: {musicBrainzRelease.Title}", LogEventLevel.Verbose);
        AlbumArtist insert = new()
        {
            AlbumId = musicBrainzRelease.Id,
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.AlbumArtist.Upsert(insert)
            .On(e => new { e.AlbumId, e.ArtistId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                ArtistId = i.ArtistId
            })
            .RunAsync();
    }

    private async Task LinkArtistToTrack(MusicBrainzArtist musicBrainzArtistMusicBrainzArtist,
        MusicBrainzTrack musicBrainzTrack)
    {
        Logger.App($"Linking Artist to Track: {musicBrainzTrack.Title}", LogEventLevel.Verbose);
        ArtistTrack insert = new()
        {
            ArtistId = musicBrainzArtistMusicBrainzArtist.Id,
            TrackId = musicBrainzTrack.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.ArtistTrack.Upsert(insert)
            .On(e => new { e.ArtistId, e.TrackId })
            .WhenMatched((s, i) => new()
            {
                ArtistId = i.ArtistId,
                TrackId = i.TrackId
            })
            .RunAsync();
    }

    private async Task LinkGenreToReleaseGroup(MusicBrainzReleaseGroup musicBrainzReleaseGroup,
        MusicBrainzGenreDetails musicBrainzGenre)
    {
        Logger.App($"Linking Genre to Release Group: {musicBrainzReleaseGroup.Title}", LogEventLevel.Verbose);
        MusicGenreReleaseGroup insert = new()
        {
            GenreId = musicBrainzGenre.Id,
            ReleaseGroupId = musicBrainzReleaseGroup.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.MusicGenreReleaseGroup.Upsert(insert)
            .On(e => new { e.GenreId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new()
            {
                GenreId = i.GenreId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    private async Task LinkGenreToArtist(MusicBrainzArtistDetails musicBrainzArtist,
        MusicBrainzGenreDetails musicBrainzGenre)
    {
        Logger.App($"Linking Genre to Artist: {musicBrainzArtist.Name}", LogEventLevel.Verbose);

        bool genreExists = new MediaContext().MusicGenres
            .AsNoTracking()
            .Any(g => g.Id == musicBrainzGenre.Id);

        if (!genreExists)
        {
            Logger.App($"Genre does not exist: {musicBrainzGenre.Name}, creating it", LogEventLevel.Verbose);
            MusicGenre genreInsert = new()
            {
                Id = musicBrainzGenre.Id,
                Name = musicBrainzGenre.Name
            };

            await new MediaContext().MusicGenres.Upsert(genreInsert)
                .On(e => new { e.Id })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    Name = i.Name
                })
                .RunAsync();
        }

        ArtistMusicGenre insert = new()
        {
            MusicGenreId = musicBrainzGenre.Id,
            ArtistId = musicBrainzArtist.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.ArtistMusicGenre.Upsert(insert)
            .On(e => new { e.MusicGenreId, e.ArtistId })
            .WhenMatched((s, i) => new()
            {
                MusicGenreId = i.MusicGenreId,
                ArtistId = i.ArtistId
            })
            .RunAsync();
    }

    private async Task LinkGenreToRelease(MusicBrainzReleaseAppends artist, MusicBrainzGenreDetails musicBrainzGenre)
    {
        Logger.App($"Linking Genre to Album: {artist.Title}", LogEventLevel.Verbose);
        AlbumMusicGenre insert = new()
        {
            MusicGenreId = musicBrainzGenre.Id,
            AlbumId = artist.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.AlbumMusicGenre.Upsert(insert)
            .On(e => new { e.MusicGenreId, e.AlbumId })
            .WhenMatched((s, i) => new()
            {
                MusicGenreId = i.MusicGenreId,
                AlbumId = i.AlbumId
            })
            .RunAsync();
    }

    private async Task LinkGenreToTrack(MusicBrainzTrack musicBrainzTrack, MusicBrainzGenreDetails musicBrainzGenre)
    {
        Logger.App($"Linking Genre to Track: {musicBrainzTrack.Title}", LogEventLevel.Verbose);
        MusicGenreTrack insert = new()
        {
            GenreId = musicBrainzGenre.Id,
            TrackId = musicBrainzTrack.Id
        };

        MediaContext mediaContext = new();
        await mediaContext.MusicGenreTrack.Upsert(insert)
            .On(e => new { e.GenreId, e.TrackId })
            .WhenMatched((s, i) => new()
            {
                GenreId = i.GenreId,
                TrackId = i.TrackId
            })
            .RunAsync();
    }

    private string? FindTrackWithoutAlbumNumberByNumberPadded(MusicBrainzMedia musicBrainzMedia, string? file,
        int numberOfAlbums, int trackNumber, int padding = 2)
    {
        if (file is not null) return file;
        if (numberOfAlbums > 1) return file;

        return Files?.FirstOrDefault(f =>
        {
            string fileName = Path.GetFileName(f.Parsed!.FilePath).RemoveDiacritics().RemoveNonAlphaNumericCharacters()
                .ToLower();

            string matchNumber = $"{trackNumber.ToString().PadLeft(padding, '0')} ";
            string matchString = musicBrainzMedia.Tracks[trackNumber - 1].Title
                .RemoveDiacritics().RemoveNonAlphaNumericCharacters().ToLower().Replace(".mp3", "");

            return fileName.StartsWith(matchNumber)
                   && fileName.Contains(matchString);
        })?.Parsed!.FilePath;
    }

    private string? FindTrackWithAlbumNumberByNumberPadded(MusicBrainzMedia musicBrainzMedia, string? file,
        int numberOfAlbums, int trackNumber, int padding = 2)
    {
        if (file is not null) return file;
        if (numberOfAlbums == 1) return file;

        return Files?.FirstOrDefault(f =>
        {
            string fileName = Path.GetFileName(f.Parsed!.FilePath).RemoveDiacritics().RemoveNonAlphaNumericCharacters()
                .ToLower();

            string matchNumber = $"{musicBrainzMedia.Position}-{trackNumber.ToString().PadLeft(padding, '0')} ";
            string matchString = musicBrainzMedia.Tracks[trackNumber - 1].Title
                .RemoveDiacritics().RemoveNonAlphaNumericCharacters().ToLower().Replace(".mp3", "");

            return fileName.StartsWith(matchNumber)
                   && fileName.Contains(matchString);
        })?.Parsed!.FilePath;
    }

    [GeneratedRegex("^00:")]
    private static partial Regex HmsRegex();

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [GeneratedRegex(
        @"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]?\s?(?<album>.*)?)")]
    private static partial Regex PathRegex();
}