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

using NoMercy.Providers.FanArt.Models;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtMusicClient : FanArtBaseClient
{
    public FanArtMusicClient() { }

    public Task<FanArtArtistDetails?> Artist(Guid id, bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        return Get<FanArtArtistDetails>("music/" + id, queryParams, priority);
    }

    public Task<FanArtAlbum?> Album(Guid id, bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        return Get<FanArtAlbum>("music/albums/" + id, queryParams, priority);
    }

    public Task<FanArtLabel?> Label(Guid id, bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        return Get<FanArtLabel>("music/labels/" + id, queryParams, priority);
    }

    public Task<FanArtLatest[]?> Latest(Guid id, bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        return Get<FanArtLatest[]>("music/latest", queryParams, priority);
    }
}
