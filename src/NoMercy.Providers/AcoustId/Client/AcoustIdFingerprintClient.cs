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
    private readonly IAudioFingerprinter? _fingerprinter;

    public AcoustIdFingerprintClient(IAudioFingerprinter? fingerprinter = null)
    {
        _fingerprinter = fingerprinter;
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

        return GetFingerprint<AcoustIdFingerprint>(
            "lookup?meta=" + string.Join("+", appendices),
            queryParams,
            priority
        );
    }

    // Fingerprint the file (chromaprint via IAudioFingerprinter), then look the
    // fingerprint up against AcoustId. Returns null when the file cannot be
    // fingerprinted or AcoustId has no matching recordings.
    public async ValueTask<AcoustIdFingerprint?> Lookup(string? file, bool? priority = false)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        if (_fingerprinter is null)
        {
            throw new InvalidOperationException(
                "AcoustIdFingerprintClient was constructed without an IAudioFingerprinter; "
                         + "fingerprint lookup by file requires one."
            );
        }

        AudioFingerprint? fingerprint = await _fingerprinter.FingerprintAsync(
            file,
            CancellationToken.None
        );
        if (fingerprint is null || string.IsNullOrWhiteSpace(fingerprint.Fingerprint))
        {
            return null;
        }

        FingerPrintData fingerprintData = new()
        {
            Fingerprint = fingerprint.Fingerprint,
            Duration = fingerprint.DurationSeconds,
        };

        return await WithFingerprint(
            ["recordings", "releases", "releasegroups"],
            fingerprintData,
            priority
        );
    }
}
