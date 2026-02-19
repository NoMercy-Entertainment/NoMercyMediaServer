using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName()
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
                    MusicGenreId = genre.Id
                }).ToList();

            await musicGenreRepository.LinkToArtist(genres);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["music", "artist", artistCredit.MusicBrainzArtist.Id.ToString()]
            });
    }

    /** this is the store for a Release artist */
    public async Task Store(MusicBrainzArtistAppends artistCredit, MusicBrainzReleaseAppends releaseAppends, Library library, Folder libraryFolder)
    {
        Logger.MusicBrainz($"Storing Artist: {artistCredit.Name}", LogEventLevel.Verbose);
        string artistFolder = MakeArtistFolder(artistCredit.Name);
        string folder = artistFolder.Replace("/", Str.DirectorySeparator);
        
        CoverArtImageManagerManager.CoverPalette? coverPalette = await GetCoverArtForArtist(artistCredit);
        
        Artist artist = new()
        {
            Id = artistCredit.Id,
            Name = artistCredit.Name,
            Disambiguation = string.IsNullOrEmpty(artistCredit.Disambiguation)
                ? null
                : artistCredit.Disambiguation,
            Cover = coverPalette?.Url is not null
                ? $"/{coverPalette.Url.FileName()}"
                : null,
            Country = artistCredit.Country,
            TitleSort = artistCredit.SortName,

            LibraryId = library.Id,
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName()
        };

        await artistRepository.StoreAsync(artist);
        jobDispatcher.DispatchJob<MusicMetadataJob>(artistCredit);
        
        await LinkToLibrary(artistCredit, library);
        await LinkToRelease(artistCredit, releaseAppends);
        
        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in artistCredit.Genres)
        {
            MusicGenre musicGenre = new()
            {
                Id = musicBrainzGenreDetails.Id,
                Name = musicBrainzGenreDetails.Name,
            };
            await musicGenreRepository.Store(musicGenre);
        }
        try
        {
            List<ArtistMusicGenre> genres = artistCredit.Genres
                .Select(genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.Id,
                    MusicGenreId = genre.Id
                }).ToList();

            await musicGenreRepository.LinkToArtist(genres);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["music", "artist", artistCredit.Id.ToString()]
            });
    }

    /** this is the store for a Recording artist */
    public async Task Store(MusicBrainzArtistDetails artistCredit, Library library, Folder libraryFolder,
        MediaFolder mediaFolder, MusicBrainzTrack track)
    {
        Logger.MusicBrainz($"Storing Artist: {artistCredit.Name}", LogEventLevel.Verbose);
        string artistFolder = MakeArtistFolder(artistCredit.Name);
        string folder = mediaFolder.Path.Replace(libraryFolder.Path, "");
        
        CoverArtImageManagerManager.CoverPalette? coverPalette = await GetCoverArtForArtist(artistCredit);

        Artist artist = new()
        {
            Id = artistCredit.Id,
            Name = artistCredit.Name,
            Disambiguation = string.IsNullOrEmpty(artistCredit.Disambiguation)
                ? null
                : artistCredit.Disambiguation,
            Cover = coverPalette?.Url is not null
                ? $"/{coverPalette.Url.FileName()}"
                : null,
            Country = artistCredit.Country,
            TitleSort = artistCredit.SortName,

            LibraryId = library.Id,
            FolderId = libraryFolder.Id,

            Folder = artistFolder,
            HostFolder = folder.PathName()
        };

        await artistRepository.StoreAsync(artist);
        jobDispatcher.DispatchJob<MusicMetadataJob>(artistCredit);

        await LinkToLibrary(artistCredit, library);
        await LinkToTrack(artistCredit, track);
        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in artistCredit.Genres)
        {
            MusicGenre musicGenre = new()
            {
                Id = musicBrainzGenreDetails.Id,
                Name = musicBrainzGenreDetails.Name,
            };
            await musicGenreRepository.Store(musicGenre);
        }
        try
        {
            List<ArtistMusicGenre> genres = artistCredit.Genres
                .Select(genre => new ArtistMusicGenre
                {
                    ArtistId = artistCredit.Id,
                    MusicGenreId = genre.Id
                }).ToList();

            await musicGenreRepository.LinkToArtist(genres);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["music", "artist", artistCredit.Id.ToString()]
            });

    }

    private static async Task<CoverArtImageManagerManager.CoverPalette?> GetCoverArtForArtist(MusicBrainzArtistDetails artistCredit)
    {
        CoverArtImageManagerManager.CoverPalette? coverPalette = await FanArtImageManager.Add(artistCredit.Id, true);
        
        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await FanArtImageClient.Download(coverPalette.Url!);
        }
        
        return coverPalette;
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

    private async Task LinkToRelease(MusicBrainzArtistDetails artistMusicBrainzArtist,
        MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.App($"Linking Artist to Release: {artistMusicBrainzArtist.Name}", LogEventLevel.Verbose);

        AlbumArtist insert = new()
        {
            ArtistId = artistMusicBrainzArtist.Id,
            AlbumId = releaseAppends.Id
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