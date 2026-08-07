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

    /// <summary>
    /// The artist this job describes, or <see cref="Guid.Empty"/> when it describes
    /// a release group instead.
    /// <para>
    /// An id rather than the artist: the whole MusicBrainz artist graph — every
    /// recording it appears on — was being serialized into the payload at around
    /// 70KB a job, and the only thing ever read back out of it was this id.
    /// </para>
    /// </summary>
    public Guid ArtistId { get; set; }

    /// <summary>
    /// The release group this job describes, or <see cref="Guid.Empty"/> when it
    /// describes an artist instead.
    /// </summary>
    public Guid ReleaseGroupId { get; set; }

    // Read from a payload but never written back to one, so a job queued before
    // the ids replaced the graphs still knows which artist it describes.
    public MusicBrainzArtist? MusicBrainzArtist { get; set; }

    public bool ShouldSerializeMusicBrainzArtist() => false;

    public MusicBrainzReleaseGroup? MusicBrainzReleaseGroup { get; set; }

    public bool ShouldSerializeMusicBrainzReleaseGroup() => false;

    public MusicMetadataJob()
    {
        //
    }

    [ActivatorUtilitiesConstructor]
    public MusicMetadataJob(ILoggerFactory loggerFactory)
        : base(loggerFactory) { }

    public MusicMetadataJob(MusicBrainzArtist musicBrainzArtist)
    {
        ArtistId = musicBrainzArtist.Id;
    }

    public MusicMetadataJob(MusicBrainzReleaseGroup? musicBrainzReleaseGroup)
    {
        ReleaseGroupId = musicBrainzReleaseGroup?.Id ?? Guid.Empty;
    }

    public override async Task Handle()
    {
        if (ArtistId == Guid.Empty && MusicBrainzArtist is not null)
            ArtistId = MusicBrainzArtist.Id;

        if (ReleaseGroupId == Guid.Empty && MusicBrainzReleaseGroup is not null)
            ReleaseGroupId = MusicBrainzReleaseGroup.Id;

        if (ArtistId != Guid.Empty)
            await HandleArtist();
        else if (ReleaseGroupId != Guid.Empty)
            await HandleReleaseGroup();
    }

    private async Task HandleArtist()
    {
        if (ArtistId == Guid.Empty)
            return;

        try
        {
            TadbArtistClient artistClient = new();
            TadbArtist? result = await artistClient.ByMusicBrainzId(ArtistId);
            if (result?.Descriptions is null)
                return;

            await using MediaContext context = new();
            Artist? artist = await context.Artists.FindAsync(ArtistId);
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
                ArtistId = this.ArtistId,
                Iso31661 = x.Iso31661,
                Description = x.Description,
            });

            await context
                .Translations.UpsertRange(translations)
                .On(x => new { x.ArtistId, x.Iso31661 })
                .WhenMatched(
                    (s, i) =>
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
        if (ReleaseGroupId == Guid.Empty)
            return;

        try
        {
            TadbReleaseGroupClient releaseClient = new();
            TadbAlbum? result = await releaseClient.ByMusicBrainzId(ReleaseGroupId);
            if (result?.Descriptions is null)
                return;

            await using MediaContext context = new();
            ReleaseGroup? releaseGroup = await context.ReleaseGroups.FindAsync(ReleaseGroupId);
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
                ReleaseGroupId = this.ReleaseGroupId,
                Iso31661 = x.Iso31661,
                Description = x.Description,
            });

            await context
                .Translations.UpsertRange(translations)
                .On(x => new { x.ReleaseGroupId, x.Iso31661 })
                .WhenMatched(
                    (s, i) =>
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
