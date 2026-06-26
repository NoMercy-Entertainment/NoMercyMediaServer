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
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public class MusicGenreManager() : BaseManager, IMusicGenreManager
{
    private readonly MusicGenreRepository _musicGenreRepository = null!;

    public MusicGenreManager(MusicGenreRepository musicGenreRepository)
        : this()
    {
        _musicGenreRepository = musicGenreRepository;
    }

    public Task Store(MusicBrainzGenreDetails genre)
    {
        MusicGenre insert = new() { Id = genre.Id, Name = genre.Name };

        return _musicGenreRepository!.Store(insert);
    }
}
