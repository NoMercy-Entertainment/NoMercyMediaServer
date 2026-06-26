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

using AcoustID;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Setup.Server;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtMovieClient : FanArtBaseClient
{
    public FanArtMovieClient()
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    public Task<FanArtMovie?> Movie(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtMovie>("movies/" + id, queryParams, priority);
    }

    public Task<FanArtLatest[]?> Latest(Guid id, bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<FanArtLatest[]>("movies/latest" + id, queryParams, priority);
    }
}
