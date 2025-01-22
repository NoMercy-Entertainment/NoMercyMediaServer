using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;
using Serilog.Events;

namespace NoMercy.Data.Jobs;

[Serializable]
public class CoverArtImageJob : IShouldQueue
{
    public MusicBrainzReleaseAppends? MusicBrainzRelease { get; set; }

    public CoverArtImageJob()
    {
        //
    }

    public CoverArtImageJob(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        MusicBrainzRelease = musicBrainzRelease;
    }

    public async Task Handle()
    {
        try
        {
            if (MusicBrainzRelease is null) return;

            CoverPalette? coverPalette = await FetchCover(MusicBrainzRelease);
            if (coverPalette is null) return;

            await using MediaContext mediaContext = new();
            Album? album = await mediaContext.Albums
                .Include(a => a.AlbumTrack)
                .ThenInclude(a => a.Track)
                .FirstOrDefaultAsync(a => a.Id == MusicBrainzRelease.Id);
            if (album is null) return;

            album._colorPalette = coverPalette.Palette ?? album._colorPalette;
            album.Cover = coverPalette.Url is not null
                ? "/" + coverPalette.Url.FileName()
                : album.Cover;
            album.UpdatedAt = DateTime.Now;

            await mediaContext.SaveChangesAsync();

            foreach (AlbumTrack albumTrack in album.AlbumTrack)
            {
                albumTrack.Track._colorPalette = coverPalette.Palette ?? albumTrack.Track._colorPalette;
                albumTrack.Track.Cover = coverPalette.Url is not null
                    ? "/" + coverPalette.Url.FileName()
                    : albumTrack.Track.Cover;
                albumTrack.Track.UpdatedAt = DateTime.Now;

                await mediaContext.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.CoverArt(e.Message, LogEventLevel.Verbose);
        }
    }

    private class CoverPalette : Image
    {
        public string? Palette { get; set; }
        public Uri? Url { get; set; }
    }

    private static async Task<CoverPalette?> FetchCover(MusicBrainzReleaseAppends musicBrainzReleaseAppends)
    {
        bool hasCover = musicBrainzReleaseAppends.CoverArtArchive.Front;
        if (!hasCover) return null;

        CoverArtCoverArtClient coverArtCoverArtClient = new(musicBrainzReleaseAppends.Id);
        CoverArtCovers? covers = await coverArtCoverArtClient.Cover();
        if (covers is null) return null;

        List<CoverArtImage> coverList = covers.Images
            .Where(image => image.Types.Contains("Front"))
            .ToList();

        foreach (CoverArtImage coverItem in coverList)
        {
            if (!coverItem.CoverArtThumbnails.Large.HasSuccessStatus("image/*")) continue;

            return new()
            {
                // Palette =
                //     await ImageLogic.CoverArtImage.ColorPalette("cover", coverItem.CoverArtThumbnails.Large),
                Url = coverItem.CoverArtThumbnails.Large
            };
        }

        return null;
    }
}