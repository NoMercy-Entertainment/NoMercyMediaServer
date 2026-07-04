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

using NoMercy.Providers.Tadb.Models;

namespace NoMercy.Providers.Tadb.Client;

public class TadbReleaseGroupClient : TadbBaseClient
{
    public async Task<TadbAlbum?> ByMusicBrainzId(Guid id, bool priority = false)
    {
        Dictionary<string, string?> queryParams = new() { { "i", id.ToString() } };

        try
        {
            return (
                await Get<TadbAlbumResponse>("album-mb.php", queryParams, priority)
            )?.Albums.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
