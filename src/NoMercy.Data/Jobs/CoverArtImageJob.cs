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
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercyQueue.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyQueue;
namespace NoMercy.Data.Jobs;

[Serializable]
public class CoverArtImageJob : IShouldQueue, IJobStorageInjector
{
    [JsonIgnore]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [JsonIgnore]
    private ILogger Log => field ??= LoggerFactory.CreateLogger(type: GetType());

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    public string QueueName => "image";
    public int Priority => 3;

    public MusicBrainzReleaseAppends? MusicBrainzRelease { get; set; }

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
        MusicBrainzRelease = musicBrainzRelease;
    }

    public async Task Handle()
    {
        try
        {
            if (MusicBrainzRelease is null)
                return;

            Uri? coverPalette = await FetchCover(musicBrainzReleaseAppends: MusicBrainzRelease);
            if (coverPalette is null)
                return;

            await using MediaContext mediaContext = new();
            Album? album = await mediaContext
                .Albums.Include(navigationPropertyPath: a => a.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: a => a.Track)
                .FirstOrDefaultAsync(predicate: a => a.Id == MusicBrainzRelease.Id);
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
            if (e.Message.Contains(value: "404"))
                return;
            Log.LogTrace(message: e.Message);
        }
    }

    private static async Task<Uri?> FetchCover(MusicBrainzReleaseAppends musicBrainzReleaseAppends)
    {
        bool hasCover = musicBrainzReleaseAppends.CoverArtArchive.Front;
        if (!hasCover)
            return null;

        CoverArtCoverArtClient coverArtCoverArtClient = new(id: musicBrainzReleaseAppends.Id);
        CoverArtCovers? covers = await coverArtCoverArtClient.Cover();
        if (covers is null)
            return null;

        List<CoverArtImage> coverList = covers
            .Images.Where(predicate: image => image.Types.Contains(value: "Front"))
            .ToList();

        foreach (CoverArtImage coverItem in coverList)
        {
            if (!coverItem.CoverArtThumbnails.Large.HasSuccessStatus(contentType: "image/*"))
                continue;

            return coverItem.CoverArtThumbnails.Large;
        }

        return null;
    }
}
