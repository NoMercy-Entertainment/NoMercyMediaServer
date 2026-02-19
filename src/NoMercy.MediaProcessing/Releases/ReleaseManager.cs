using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.MusicBrainz.Client;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Releases;

public class ReleaseManager(
    IReleaseRepository releaseRepository,
    IMusicGenreRepository musicGenreRepository
) : BaseManager, IReleaseManager
{
    public async
        Task<(MusicBrainzReleaseAppends? releaseAppends, CoverArtImageManagerManager.CoverPalette? coverPalette)> Add(
            Guid id, Library albumLibrary, Folder libraryFolder,
            MediaFolder mediaFolder)
    {
        Logger.MusicBrainz($"Adding Release: {id} to Library: {albumLibrary.Title}", LogEventLevel.Verbose);

        MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        MusicBrainzReleaseAppends? releaseAppends = await musicBrainzReleaseClient.WithAllAppends(id);
        
        if (releaseAppends == null) 
            return (null, null);

        CoverArtImageManagerManager.CoverPalette? coverPalette = await CoverArtImageManagerManager.Add(releaseAppends.MusicBrainzReleaseGroup.Id);
        
        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await CoverArtCoverArtClient.Download(coverPalette.Url);
        }

        await Store(releaseAppends, albumLibrary, libraryFolder, mediaFolder, coverPalette);

        return (releaseAppends, coverPalette);
    }

    private async Task Store(MusicBrainzReleaseAppends releaseAppends, Library library, Folder libraryFolder,
        MediaFolder mediaFolder, CoverArtImageManagerManager.CoverPalette? coverPalette)
    {
        try
        {
            Logger.MusicBrainz($"Storing Release: {releaseAppends.Title}", LogEventLevel.Verbose);

            string folder = mediaFolder.Path.Replace(libraryFolder.Path, "");

            Album release = new()
            {
                Id = releaseAppends.Id,
                Name = releaseAppends.Title,
                Country = releaseAppends.Country,
                Disambiguation = string.IsNullOrEmpty(releaseAppends.Disambiguation)
                    ? null
                    : releaseAppends.Disambiguation,
                Year = releaseAppends.DateTime?.Year ?? 0,
                Tracks = releaseAppends.Media.Sum(m => m.TrackCount),

                LibraryId = library.Id,
                FolderId = libraryFolder.Id,
                HostFolder = folder.PathName(),

                Folder = folder.Replace(libraryFolder.Path, "")
                    .Replace("\\", "/"),

                Cover = coverPalette?.Url is not null
                    ? $"/{coverPalette.Url.FileName()}"
                    : null,
                
                _colorPalette = coverPalette?.Palette ?? string.Empty
            };

            await releaseRepository.Store(release);

            await LinkToLibrary(releaseAppends, library);

            List<AlbumMusicGenre> genres = releaseAppends.Genres.Select(genre => new AlbumMusicGenre
            {
                AlbumId = releaseAppends.Id,
                MusicGenreId = genre.Id
            }).ToList();

            await musicGenreRepository.LinkToRelease(genres);

            Logger.MusicBrainz($"Release {releaseAppends.Title} stored", LogEventLevel.Verbose);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }
    }

    private async Task LinkToLibrary(MusicBrainzReleaseAppends releaseAppends, Library library)
    {
        Logger.MusicBrainz($"Linking Release to Library: {releaseAppends.Title}", LogEventLevel.Verbose);

        AlbumLibrary insert = new()
        {
            AlbumId = releaseAppends.Id,
            LibraryId = library.Id
        };

        await releaseRepository.LinkToLibrary(insert);
    }

    public async Task Store(MusicBrainzReleaseAppends releaseAppends, Library library, Folder libraryFolder,
        MediaFile mediaFile, CoverArtImageManagerManager.CoverPalette? coverPalette)
    {
        try
        {
            Logger.MusicBrainz($"Storing Release: {releaseAppends.Title}", LogEventLevel.Verbose);

            string folder = Path.GetDirectoryName(mediaFile.Path.Replace(libraryFolder.Path, "")) ?? "";

            Album release = new()
            {
                Id = releaseAppends.Id,
                Name = releaseAppends.Title,
                Country = releaseAppends.Country,
                Disambiguation = string.IsNullOrEmpty(releaseAppends.Disambiguation)
                    ? null
                    : releaseAppends.Disambiguation,
                Year = releaseAppends.DateTime?.Year ?? 0,
                Tracks = releaseAppends.Media.Sum(m => m.TrackCount),

                LibraryId = library.Id,
                FolderId = libraryFolder.Id,
                HostFolder = folder.PathName(),

                Folder = folder.Replace(libraryFolder.Path, "")
                    .Replace("\\", "/"),

                Cover = coverPalette?.Url is not null
                    ? $"/{coverPalette.Url.FileName()}"
                    : null,
                
                _colorPalette = coverPalette?.Palette ?? string.Empty
            };

            await releaseRepository.Store(release);

            await LinkToLibrary(releaseAppends, library);
            await LinkToReleaseGroup(releaseAppends);
            await LinkToGenre(releaseAppends);

            Logger.MusicBrainz($"Release {releaseAppends.Title} stored", LogEventLevel.Verbose);
        }
        catch (Exception e)
        {
            Logger.MusicBrainz(e.Message, LogEventLevel.Error);
        }
    }

    private async Task LinkToGenre(MusicBrainzReleaseAppends releaseAppends)
    {
        List<AlbumMusicGenre> genres = releaseAppends.Genres.Select(genre => new AlbumMusicGenre
        {
            AlbumId = releaseAppends.Id,
            MusicGenreId = genre.Id
        }).ToList();

        await musicGenreRepository.LinkToRelease(genres);
    }

    private async Task LinkToReleaseGroup(MusicBrainzReleaseAppends releaseAppends)
    {
        Logger.MusicBrainz($"Linking Release to Release Group: {releaseAppends.Title}", LogEventLevel.Verbose);

        AlbumReleaseGroup insert = new()
        {
            AlbumId = releaseAppends.Id,
            ReleaseGroupId = releaseAppends.MusicBrainzReleaseGroup.Id
        };

        await releaseRepository.LinkToReleaseGroup(insert);
    }
}