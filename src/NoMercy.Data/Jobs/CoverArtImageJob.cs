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
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Data.Jobs;

[Serializable]
public class CoverArtImageJob : IShouldQueue, IJobStorageInjector
{
    [JsonIgnore]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [JsonIgnore]
    private ILogger Log => field ??= LoggerFactory.CreateLogger(GetType());

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    public string QueueName => "image";
    public int Priority => 3;

    /// <summary>
    /// The release whose cover this fetches, and whether the archive claims to
    /// have a front image at all.
    /// <para>
    /// Two scalars rather than the release: the whole MusicBrainz release graph
    /// was going into the payload — around a megabyte — and these are the only
    /// two things ever read back out of it.
    /// </para>
    /// </summary>
    public Guid ReleaseId { get; set; }

    public bool HasFrontCover { get; set; }

    // Read from a payload but never written back to one, so a job queued before
    // the ids replaced the graph still knows which release it describes.
    public MusicBrainzReleaseAppends? MusicBrainzRelease { get; set; }

    public bool ShouldSerializeMusicBrainzRelease() => false;

    // Constructor injection: the queue worker builds the job via
    // ActivatorUtilities, so the logger factory arrives without the
    // post-construction InjectStorageServices hook. The parameterless
    // ctor below is kept for deserialization and direct construction.
    [ActivatorUtilitiesConstructor]
    public CoverArtImageJob(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
    }

    public CoverArtImageJob()
    {
        //
    }

    public CoverArtImageJob(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        ReleaseId = musicBrainzRelease.Id;
        HasFrontCover = musicBrainzRelease.CoverArtArchive.Front;
    }

    public async Task Handle()
    {
        if (ReleaseId == Guid.Empty && MusicBrainzRelease is not null)
        {
            ReleaseId = MusicBrainzRelease.Id;
            HasFrontCover = MusicBrainzRelease.CoverArtArchive.Front;
        }

        try
        {
            if (ReleaseId == Guid.Empty)
                return;

            Uri? coverPalette = await FetchCover();
            if (coverPalette is null)
                return;

            await using MediaContext mediaContext = new();
            Album? album = await mediaContext
                .Albums.Include(a => a.AlbumTrack)
                    .ThenInclude(a => a.Track)
                .FirstOrDefaultAsync(a => a.Id == ReleaseId);
            if (album is null)
                return;

            album.Cover = coverPalette is not null ? "/" + coverPalette.FileName() : album.Cover;

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
            if (e.Message.Contains("404"))
                return;
            Log.LogTrace(e.Message);
        }
    }

    private async Task<Uri?> FetchCover()
    {
        if (!HasFrontCover)
            return null;

        CoverArtCoverArtClient coverArtCoverArtClient = new(ReleaseId);
        CoverArtCovers? covers = await coverArtCoverArtClient.Cover();
        if (covers is null)
            return null;

        List<CoverArtImage> coverList = covers
            .Images.Where(image => image.Types.Contains("Front"))
            .ToList();

        foreach (CoverArtImage coverItem in coverList)
        {
            if (!coverItem.CoverArtThumbnails.Large.HasSuccessStatus("image/*"))
                continue;

            return coverItem.CoverArtThumbnails.Large;
        }

        return null;
    }
}
