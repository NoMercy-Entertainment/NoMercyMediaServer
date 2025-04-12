using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Recordings;

public partial class RecordingManager(
    IRecordingRepository recordingRepository,
    IMusicGenreRepository musicGenreRepository
) : BaseManager, IRecordingManager
{
    public async Task<bool> Store(MusicBrainzReleaseAppends releaseAppends,
        MusicBrainzTrack musicBrainzTrack, MusicBrainzMedia musicBrainzMedia, Folder libraryFolder, MediaFolder mediaFolder, CoverArtImageManagerManager.CoverPalette? coverPalette)
    {
        Logger.MusicBrainz($"Storing Recording: {releaseAppends.Title} - {musicBrainzMedia.Position}-{musicBrainzTrack.Position} {musicBrainzTrack.Title}", LogEventLevel.Verbose);

        MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .FilterByMediaType("music")
            .Process(mediaFolder.Path, 1);

        foreach (MediaFolderExtend folder in folders)
        {
            if (folder.Files is null) continue;
            foreach (MediaFile file in folder.Files)
            {
                MediaFile? mediaFile = FileMatch(file, releaseAppends, musicBrainzMedia, musicBrainzTrack.Position);
                if (mediaFile is null) continue;

                Logger.MusicBrainz($"Recording {musicBrainzTrack.Title} found", LogEventLevel.Verbose);

                string path = mediaFile.Parsed?.FilePath.Replace(Path.DirectorySeparatorChar + mediaFile.Name, "") ?? string.Empty;
        
                Track insert = new()
                {
                    Id = musicBrainzTrack.Id,
                    Name = musicBrainzTrack.Title,
                    Date = releaseAppends.DateTime ?? releaseAppends.ReleaseEvents?.FirstOrDefault()?.DateTime,
                    DiscNumber = musicBrainzMedia.Position,
                    TrackNumber = musicBrainzTrack.Position,

                    Filename = "/" + Path.GetFileName(mediaFile.Path),
                    Quality = (int)Math.Floor(mediaFile.FFprobe!.Format.BitRate / 1000.0),
                    Duration = HmsRegex().Replace(mediaFile.FFprobe.Duration.ToString("hh\\:mm\\:ss"), ""),

                    FolderId = libraryFolder.Id,
                    Folder = path.Replace(libraryFolder.Path, "").Replace("\\", "/"),
                    HostFolder = path.PathName(),
                    
                    Cover = coverPalette?.Url is not null
                        ? $"/{coverPalette.Url.FileName()}" : null,
                    _colorPalette = coverPalette?.Palette ?? string.Empty,
                };
        
                await recordingRepository.Store(insert);
                
                await LinkToRelease(musicBrainzTrack, releaseAppends);
                
                List<MusicGenreTrack> genres = musicBrainzTrack.Genres
                    ?.Select(genre => new MusicGenreTrack
                    {
                        TrackId = musicBrainzTrack.Id,
                        GenreId = genre.Id,
                    }).ToList() ?? [];

                await musicGenreRepository.LinkToRecording(genres);
                
                Logger.MusicBrainz($"Recording {musicBrainzTrack.Title} stored", LogEventLevel.Verbose);
                
                return true;
            }
        }
        
        return false;
    }

    private async Task LinkToArtist(MusicBrainzTrack musicBrainzTrack, MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.MusicBrainz($"Linking Recording to Artist: {musicBrainzTrack.Title} - {releaseAppends.MusicBrainzReleaseGroup.Title}", LogEventLevel.Verbose);

        foreach (ReleaseArtistCredit credit in releaseAppends.ArtistCredit)
        {
            ArtistTrack insert = new()
            {
                ArtistId = credit.MusicBrainzArtist.Id,
                TrackId = musicBrainzTrack.Id
            };
            
            await recordingRepository.LinkToArtist(insert);
        }
    }

    private async Task LinkToRelease(MusicBrainzTrack track, MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.MusicBrainz($"Linking Recording to Release Group: {track.Title} - {releaseAppends.MusicBrainzReleaseGroup.Title}", LogEventLevel.Verbose);
        
        AlbumTrack insert = new()
        {
            AlbumId = releaseAppends.Id,
            TrackId = track.Id
        };
        
        await recordingRepository.LinkToRelease(insert);
    }
    
    private MediaFile? FileMatch(MediaFile inputFile, MusicBrainzReleaseAppends musicBrainzRelease,
        MusicBrainzMedia musicBrainzMedia, int trackNumber)
    {
        bool hasMatch = FindTrackWithAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, false,
            musicBrainzRelease.Media.Length,
            trackNumber, 4);
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber, 3);
        hasMatch = FindTrackWithAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber);

        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber, 4);
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber, 3);
        hasMatch = FindTrackWithoutAlbumNumberByNumberPadded(inputFile, musicBrainzMedia, hasMatch,
            musicBrainzRelease.Media.Length,
            trackNumber);
        
        if(!hasMatch) return null;
        return inputFile;
    }

    private bool FindTrackWithoutAlbumNumberByNumberPadded(MediaFile inputFile, MusicBrainzMedia musicBrainzMedia,
        bool hasMatch,
        int numberOfAlbums, int trackNumber, int padding = 2)
    {
        if (hasMatch) return true;
        if (numberOfAlbums > 1) return false;
        if (inputFile.Parsed is null) return false;

        string fileName = Path.GetFileName(inputFile.Parsed.FilePath).RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber = $"{trackNumber.ToString().PadLeft(padding, '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber) return false;
        string matchString = musicBrainzMedia.Tracks[trackNumber - 1].Title
            .RemoveDiacritics().RemoveNonAlphaNumericCharacters().ToLower().Replace(".mp3", "");

        return fileName.StartsWith(matchNumber)
               && fileName.Contains(matchString);
    }

    private bool FindTrackWithAlbumNumberByNumberPadded(MediaFile inputFile, MusicBrainzMedia musicBrainzMedia,
        bool hasMatch,
        int numberOfAlbums, int trackNumber, int padding = 2)
    {
        if (hasMatch) return true;
        if (numberOfAlbums == 1) return false;
        if (inputFile.Parsed is null) return false;

        string fileName = Path.GetFileName(inputFile.Parsed.FilePath).RemoveDiacritics()
            .RemoveNonAlphaNumericCharacters()
            .ToLower();

        string matchNumber = $"{musicBrainzMedia.Position}-{trackNumber.ToString().PadLeft(padding, '0')} ";
        if (musicBrainzMedia.Tracks.Length < trackNumber) return false;
        string matchString = musicBrainzMedia.Tracks[trackNumber - 1].Title
            .RemoveDiacritics().RemoveNonAlphaNumericCharacters().ToLower().Replace(".mp3", "");

        return fileName.StartsWith(matchNumber)
               && fileName.Contains(matchString);
    }
    
    [GeneratedRegex("^00:")]
    private static partial Regex HmsRegex();
}