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

namespace NoMercy.Setup.Server;

public interface IApiKeyStore
{
    string AcousticIdKey { get; }
    string FanArtApiKey { get; }
    string FanArtClientKey { get; }
    string JwplayerKey { get; }
    string MakeMkvKey { get; }
    string MusixmatchKey { get; }
    string OmdbKey { get; }
    string RottenTomatoes { get; }
    string TadbKey { get; }
    string TmdbKey { get; }
    string TmdbToken { get; }
    string TvdbKey { get; }
    bool KeysLoaded { get; }
    string[] Colors { get; }
    string Quote { get; }
}
