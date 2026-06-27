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

using AniDB.ResponseItems;

namespace NoMercy.Providers.AniDb.Client;

/// <summary>
/// Injectable AniDB (UDP) client. Replaces the former all-static AniDbBaseClient
/// so its connection lifecycle is owned by DI (disposed on shutdown) instead of
/// process-global static state with a never-invoked static Dispose.
/// </summary>
public interface IAniDbService : IDisposable
{
    void SetCredentials(string username, string password, string? apiKey);
    Task Init();
    Task<AniDBAnimeItem> GetRandomAnime(CancellationToken ct = default);
}
