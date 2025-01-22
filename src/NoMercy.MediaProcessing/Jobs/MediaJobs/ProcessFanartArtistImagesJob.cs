// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ProcessFanartArtistImagesJob : AbstractFanArtDataJob
{
    public override string QueueName => "image";
    public override int Priority => 7;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        ImageRepository imageRepository = new(context);
        FanArtImageManager imageManager = new(imageRepository, jobDispatcher);
        
        try
        {
            using FanArtMusicClient fanArtMusicClient = new();
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(Id1);
            if (fanArt is null) return;
            
            Artist dbArtist = await context.Artists
                .FirstAsync(a => a.Id == Id1);
            
            await imageManager.StoreReleaseImages(fanArt.ArtistAlbum, Id1, dbArtist);

            Database.Models.Image? artistCover = await imageManager.StoreArtistImages(fanArt, Id1, dbArtist);
            try
            {
                if (artistCover is not null && (dbArtist.Cover is null || dbArtist._colorPalette is ""))
                {
                    dbArtist.Cover = artistCover.FilePath ?? dbArtist.Cover;
                    dbArtist._colorPalette = artistCover._colorPalette.Replace("\"image\"", "\"cover\"");

                    await context.Artists.Upsert(dbArtist)
                        .On(v => v.Id)
                        .WhenMatched((s, i) => new()
                        {
                            Id = i.Id,
                            Cover = i.Cover,
                            _colorPalette = i._colorPalette,
                            UpdatedAt = i.UpdatedAt
                        })
                        .RunAsync();
                }
            }
            catch (Exception e)
            {
                Logger.FanArt(e.Message, LogEventLevel.Warning);
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
    }
}