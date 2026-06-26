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
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Setup.Server;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdFingerprintClient : AcoustIdBaseClient
{
    public AcoustIdFingerprintClient()
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    private Task<AcoustIdFingerprint?> WithFingerprint(
        string[] appendices,
        FingerPrintData fingerprintData,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["client"] = ApiKeyStore.Current.AcousticIdKey,
            ["duration"] = fingerprintData.Duration.ToString(),
            ["fingerprint"] = fingerprintData.Fingerprint,
        };

        return Get<AcoustIdFingerprint>(
            "lookup?meta=" + string.Join("+", appendices),
            queryParams,
            priority
        );
    }

    public async ValueTask<AcoustIdFingerprint?> Lookup(string? file, bool? priority = false)
    {
        if (file == null)
            return null;

        // Fingerprinting requires chromaprint/fpcalc to extract audio fingerprint + FFmpeg for duration.
        // The V1 encoder bundled this; V3 encoder doesn't expose fingerprinting directly.
        // Return null until a dedicated fingerprint service is wired up via DI.
        await Task.CompletedTask;
        return null;
    }
}
