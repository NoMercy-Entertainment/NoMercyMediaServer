// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.Tadb.Client;
using NoMercy.Providers.Tadb.Models;
namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

[Serializable]
public class MusicMetadataJob : AbstractMusicDescriptionJob
{
    public override string QueueName => "music";
    public override int Priority => 6;

    public MusicBrainzArtist? MusicBrainzArtist { get; set; }
    public MusicBrainzReleaseGroup? MusicBrainzReleaseGroup { get; set; }

    public MusicMetadataJob()
    {
        //
    }

    [ActivatorUtilitiesConstructor]
    public MusicMetadataJob(ILoggerFactory loggerFactory)
        : base(loggerFactory) { }

    public MusicMetadataJob(MusicBrainzArtist musicBrainzArtist)
    {
        MusicBrainzArtist = musicBrainzArtist;
    }

    public MusicMetadataJob(MusicBrainzReleaseGroup? musicBrainzReleaseGroup)
    {
        MusicBrainzReleaseGroup = musicBrainzReleaseGroup;
    }

    public override async Task Handle()
    {
        if (MusicBrainzArtist != null)
            await HandleArtist();
        else if (MusicBrainzReleaseGroup != null)
            await HandleReleaseGroup();
    }

    private async Task HandleArtist()
    {
        if (MusicBrainzArtist == null)
            return;

        try
        {
            TadbArtistClient artistClient = new();
            TadbArtist? result = await artistClient.ByMusicBrainzId(MusicBrainzArtist.Id);
            if (result?.Descriptions is null)
                return;

            await using MediaContext context = new();
            Artist? artist = await context.Artists.FindAsync(MusicBrainzArtist.Id);
            if (artist == null)
                return;

            artist.Description = result
                .Descriptions.Where(x => x.Iso31661 == "EN")
                .Select(x => x.Description)
                .FirstOrDefault();
            artist.Year = result.IntFormedYear?.ToInt();
            await context.SaveChangesAsync();

            List<Translation> translations = result.Descriptions.ConvertAll(x => new Translation
            {
                ArtistId = MusicBrainzArtist.Id,
                Iso31661 = x.Iso31661,
                Description = x.Description,
            });

            await context
                .Translations.UpsertRange(translations)
                .On(x => new { x.ArtistId, x.Iso31661 })
                .WhenMatched((s, i) =>
                        new()
                        {
                            ArtistId = s.ArtistId,
                            Iso31661 = s.Iso31661,
                            Description = s.Description,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404"))
                return;
            Log.LogTrace(e.Message);
        }
    }

    private async Task HandleReleaseGroup()
    {
        if (MusicBrainzReleaseGroup == null)
            return;

        try
        {
            TadbReleaseGroupClient releaseClient = new();
            TadbAlbum? result = await releaseClient.ByMusicBrainzId(MusicBrainzReleaseGroup.Id);
            if (result?.Descriptions is null)
                return;

            await using MediaContext context = new();
            ReleaseGroup? releaseGroup = await context.ReleaseGroups.FindAsync(
                MusicBrainzReleaseGroup.Id
            );
            if (releaseGroup == null)
                return;

            string? description = result
                .Descriptions.Where(x => x.Iso31661 == "EN")
                .Select(x => x.Description)
                .FirstOrDefault();

            bool hasUpdatedDescription =
                !string.IsNullOrEmpty(description) && releaseGroup.Description != description;

            releaseGroup.Description = description;
            if (hasUpdatedDescription)
                await context.SaveChangesAsync();

            List<Translation> translations = result.Descriptions.ConvertAll(x => new Translation
            {
                ReleaseGroupId = MusicBrainzReleaseGroup.Id,
                Iso31661 = x.Iso31661,
                Description = x.Description,
            });

            await context
                .Translations.UpsertRange(translations)
                .On(x => new { x.ReleaseGroupId, x.Iso31661 })
                .WhenMatched((s, i) =>
                        new()
                        {
                            ReleaseGroupId = s.ReleaseGroupId,
                            Iso31661 = s.Iso31661,
                            Description = s.Description,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404"))
                return;
            Log.LogTrace(e.Message);
        }
    }
}
