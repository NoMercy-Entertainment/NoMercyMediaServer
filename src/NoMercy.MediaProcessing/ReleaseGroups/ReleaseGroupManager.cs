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

using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public class ReleaseGroupManager(
    IReleaseGroupRepository releaseGroupRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IReleaseGroupManager
{
    public async Task Store(
        MusicBrainzReleaseGroup releaseGroup,
        Ulid id,
        CoverArtImageManagerManager.CoverPalette? coverPalette
    )
    {
        Logger.MusicBrainz($"Storing Release Group: {releaseGroup.Title}", LogEventLevel.Verbose);

        ReleaseGroup insert = new()
        {
            Id = releaseGroup.Id,
            Title = releaseGroup.Title,
            Description = string.IsNullOrEmpty(releaseGroup.Disambiguation)
                ? null
                : releaseGroup.Disambiguation,
            Year = releaseGroup.FirstReleaseDate.ParseYear(),
            LibraryId = id,
            Disambiguation = string.IsNullOrEmpty(releaseGroup.Disambiguation)
                ? null
                : releaseGroup.Disambiguation,

            Cover = coverPalette?.Url is not null ? $"/{coverPalette.Url.FileName()}" : null,
        };

        await releaseGroupRepository.Store(insert);
        jobDispatcher.DispatchColorPaletteJob("releasegroup", insert.Id.ToString());

        Logger.MusicBrainz($"Release Group {releaseGroup.Title} stored", LogEventLevel.Verbose);
    }
}
