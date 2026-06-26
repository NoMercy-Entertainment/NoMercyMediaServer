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

using AniDB;
using AniDB.RequestEnums;
using AniDB.ResponseItems;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AniDb.Models;

namespace NoMercy.Providers.AniDb.Client;

public static class AniDbRandomAnime
{
    public static Task<AniDBAnimeItem> GetRandomAnime()
    {
        AniDBClient client = AniDbBaseClient.Client();
        TaskCompletionSource<AniDBAnimeItem> tcs = new();

        client.FetchRandomAnime(
            response =>
            {
                Logger.AniDb(response.StatusCode.ToString());
                Logger.AniDb(response.StatusMessage);

                response.GetMessageItem(
                    0,
                    new AniDbCallbackObject<AniDBAnimeItem>(messageItem =>
                    {
                        messageItem.parseContentsDefault();

                        tcs.SetResult(messageItem);
                    })
                );
            },
            RandomAnimeSource.ANY,
            2
        );

        return tcs.Task;
    }
}
