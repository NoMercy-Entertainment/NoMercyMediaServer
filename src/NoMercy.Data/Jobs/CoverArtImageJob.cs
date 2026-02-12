using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;
using Serilog.Events;

namespace NoMercy.Data.Jobs;

[Serializable]
public class CoverArtImageJob : IShouldQueue
{
    public string QueueName => "image";
    public int Priority => 3;

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

            Uri? coverPalette = await FetchCover(MusicBrainzRelease);
            if (coverPalette is null) return;

            await using MediaContext mediaContext = new();
            Album? album = await mediaContext.Albums
                .Include(a => a.AlbumTrack)
                .ThenInclude(a => a.Track)
                .FirstOrDefaultAsync(a => a.Id == MusicBrainzRelease.Id);
            if (album is null) return;

            album.Cover = coverPalette is not null
                ? "/" + coverPalette.FileName()
                : album.Cover;

            await mediaContext.SaveChangesAsync();

            foreach (AlbumTrack albumTrack in album.AlbumTrack)
            {
                albumTrack.Track.Cover = coverPalette is not null
                    ? "/" + coverPalette.FileName()
                    : albumTrack.Track.Cover;

                await mediaContext.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.CoverArt(e.Message, LogEventLevel.Verbose);
        }
    }

    private static async Task<Uri?> FetchCover(MusicBrainzReleaseAppends musicBrainzReleaseAppends)
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

            return coverItem.CoverArtThumbnails.Large;
        }

        return null;
    }
}