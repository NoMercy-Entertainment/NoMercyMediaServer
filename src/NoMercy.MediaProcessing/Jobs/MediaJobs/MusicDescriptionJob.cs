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
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.Tadb.Client;
using NoMercy.Providers.Tadb.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

[Serializable]
public class MusicDescriptionJob : AbstractMusicDescriptionJob
{
    public override string QueueName => "queue";
    public override int Priority => 6;

    public MusicBrainzArtist? MusicBrainzArtist { get; set; }
    public MusicBrainzReleaseGroup? MusicBrainzReleaseGroup { get; set; }

    public MusicDescriptionJob()
    {
        //
    }

    public MusicDescriptionJob(MusicBrainzArtist musicBrainzArtist)
    {
        MusicBrainzArtist = musicBrainzArtist;
    }

    public MusicDescriptionJob(MusicBrainzReleaseGroup? musicBrainzReleaseGroup)
    {
        MusicBrainzReleaseGroup = musicBrainzReleaseGroup;
    }

    public override async Task Handle()
    {
        if (MusicBrainzArtist != null)
            await HandleArtist();
        else if (MusicBrainzReleaseGroup != null) await HandleReleaseGroup();
    }

    private async Task HandleArtist()
    {
        if (MusicBrainzArtist == null) return;

        try
        {
            TadbArtistClient artistClient = new();
            TadbArtist? result = artistClient.ByMusicBrainzId(MusicBrainzArtist.Id);
            if (result?.Descriptions is null) return;

            await using MediaContext context = new();
            Artist? artist = await context.Artists.FindAsync(MusicBrainzArtist.Id);
            if (artist == null) return;

            artist.Description = result.Descriptions
                .Where(x => x.Iso31661 == "EN")
                .Select(x => x.Description)
                .FirstOrDefault();
            artist.Year = result.IntFormedYear?.ToInt();
            await context.SaveChangesAsync();

            List<Translation> translations = result.Descriptions.ConvertAll(x => new Translation
            {
                ArtistId = MusicBrainzArtist.Id,
                Iso31661 = x.Iso31661,
                Description = x.Description
            });

            await context.Translations.UpsertRange(translations)
                .On(x => new { x.ArtistId, x.Iso31661 })
                .WhenMatched((s, i) => new()
                {
                    ArtistId = s.ArtistId,
                    Iso31661 = s.Iso31661,
                    Description = s.Description
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.AudioDb(e.Message, LogEventLevel.Verbose);
        }
    }

    private async Task HandleReleaseGroup()
    {
        if (MusicBrainzReleaseGroup == null) return;

        try
        {
            TadbReleaseGroupClient releaseClient = new();
            TadbAlbum? result = releaseClient.ByMusicBrainzId(MusicBrainzReleaseGroup.Id);
            if (result?.Descriptions is null) return;

            await using MediaContext context = new();
            ReleaseGroup? releaseGroup = await context.ReleaseGroups.FindAsync(MusicBrainzReleaseGroup.Id);
            if (releaseGroup == null) return;

            string? description = result.Descriptions
                .Where(x => x.Iso31661 == "EN")
                .Select(x => x.Description)
                .FirstOrDefault();

            bool hasUpdatedDescription = !string.IsNullOrEmpty(description) && releaseGroup.Description != description;

            releaseGroup.Description = description;
            if (hasUpdatedDescription)
                await context.SaveChangesAsync();

            List<Translation> translations = result.Descriptions.ConvertAll(x => new Translation
            {
                ReleaseGroupId = MusicBrainzReleaseGroup.Id,
                Iso31661 = x.Iso31661,
                Description = x.Description
            });

            await context.Translations.UpsertRange(translations)
                .On(x => new { x.ReleaseGroupId, x.Iso31661 })
                .WhenMatched((s, i) => new()
                {
                    ReleaseGroupId = s.ReleaseGroupId,
                    Iso31661 = s.Iso31661,
                    Description = s.Description
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.AudioDb(e.Message, LogEventLevel.Verbose);
        }
    }
}