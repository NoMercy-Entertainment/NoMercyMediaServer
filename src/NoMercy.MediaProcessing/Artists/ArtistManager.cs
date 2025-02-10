using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Artists;

public class ArtistManager(
    IArtistRepository artistRepository,
    IMusicGenreRepository musicGenreRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IArtistManager
{
    /** this is the store for a Release artist */
    public async Task Store(ReleaseArtistCredit artistCredit, Library library, Folder libraryFolder,
        MediaFolder mediaFolder, MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.MusicBrainz($"Storing Artist: {artistCredit.MusicBrainzArtist.Name}", LogEventLevel.Verbose);
        string artistFolder = MakeArtistFolder(artistCredit.MusicBrainzArtist.Name);
        string folder = mediaFolder.Path.Replace(libraryFolder.Path, "");
            
        Artist artist = new()
        {
            Id = artistCredit.MusicBrainzArtist.Id,
            Name = artistCredit.MusicBrainzArtist.Name,
            Disambiguation = string.IsNullOrEmpty(artistCredit.MusicBrainzArtist.Disambiguation)
                ? null
                : artistCredit.MusicBrainzArtist.Disambiguation,
            Country = artistCredit.MusicBrainzArtist.Country,
            TitleSort = artistCredit.MusicBrainzArtist.SortName,
        
            LibraryId = library.Id,
            FolderId =libraryFolder.Id,
            
            Folder = artistFolder,
            HostFolder = folder.PathName(),
        };
        
        await artistRepository.StoreAsync(artist);
        
        await LinkToLibrary(artistCredit.MusicBrainzArtist, library);
        await LinkToRelease(artistCredit.MusicBrainzArtist, releaseAppends);
        
        try
        {
            List<ArtistMusicGenre> genres = artistCredit.MusicBrainzArtist.Genres
                .Select(genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.MusicBrainzArtist.Id,
                    MusicGenreId = genre.Id,
                }).ToList();

            await musicGenreRepository.LinkToArtist(genres);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }
        
        jobDispatcher.DispatchJob<ProcessFanartArtistImagesJob>(artistCredit.MusicBrainzArtist.Id);
    }
    
    /** this is the store for a Recording artist */
    public async Task Store(MusicBrainzArtistDetails artistCredit, Library library, Folder libraryFolder,  MediaFolder mediaFolder, MusicBrainzTrack release)
    {
        Logger.MusicBrainz($"Storing Artist: {artistCredit.Name}", LogEventLevel.Verbose);
        string artistFolder = MakeArtistFolder(artistCredit.Name);
        string folder = mediaFolder.Path.Replace(libraryFolder.Path, "");
            
        Artist artist = new()
        {
            Id = artistCredit.Id,
            Name = artistCredit.Name,
            Disambiguation = string.IsNullOrEmpty(artistCredit.Disambiguation)
                ? null
                : artistCredit.Disambiguation,
            Country = artistCredit.Country,
            TitleSort = artistCredit.SortName,
        
            LibraryId = library.Id,
            FolderId =libraryFolder.Id,
            
            Folder = artistFolder,
            HostFolder = folder.PathName(),
        };
        
        await artistRepository.StoreAsync(artist);
        
        await LinkToLibrary(artistCredit, library);
        await LinkToTrack(artistCredit, release);
        
        jobDispatcher.DispatchJob<ProcessFanartArtistImagesJob>(artistCredit.Id);
    }

    private async Task LinkToTrack(MusicBrainzArtistDetails artistCredit, MusicBrainzTrack track)
    {
        Logger.App($"Linking Artist to Track: {artistCredit.Name}", LogEventLevel.Verbose);
        
        ArtistTrack insert = new()
        {
            ArtistId = artistCredit.Id,
            TrackId = track.Id
        };
        
        await artistRepository.LinkToRecording(insert);
    }

    private async Task LinkToRelease(MusicBrainzArtistDetails artistMusicBrainzArtist, MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.App($"Linking Artist to Release: {artistMusicBrainzArtist.Name}", LogEventLevel.Verbose);
        
        AlbumArtist insert = new()
        {
            ArtistId = artistMusicBrainzArtist.Id,
            AlbumId = releaseAppends.Id,
        };
        
        await artistRepository.LinkToRelease(insert);
    }

    private async Task LinkToLibrary(MusicBrainzArtistDetails artistMusicBrainzArtist, Library library)
    {
        Logger.App($"Linking Artist to Library: {artistMusicBrainzArtist.Name}", LogEventLevel.Verbose);
        
        ArtistLibrary insert = new()
        {
            ArtistId = artistMusicBrainzArtist.Id,
            LibraryId = library.Id
        };
        
        await artistRepository.LinkToLibrary(insert);
    }
    
    private static string MakeArtistFolder(string artist)
    {
        string artistName = artist.RemoveDiacritics();

        string artistFolder = char.IsNumber(artistName[0])
            ? "#"
            : artistName[0].ToString().ToUpper();

        return $"/{artistFolder}/{artistName}";
    }
}