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

using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId.Models;

namespace NoMercy.Providers.AcoustId.Client;

public static class FingerPrint
{
    private static AcoustIdFingerprintClient AcoustIdFingerprintClient { get; }

    static FingerPrint()
    {
        AcoustIdFingerprintClient = new();
    }

    private static async Task<AcoustIdFingerprint?> GetFingerprint(
        string file,
        bool priority = false
    )
    {
        return await AcoustIdFingerprintClient.Lookup(file: file, priority: priority);
    }

    public static async Task<List<Guid>> GetReleaseIds(string file, string albumName = "")
    {
        List<Guid> releaseIds = [];
        AcoustIdFingerprint? fingerprint = await GetFingerprint(file: file, priority: true);
        if (fingerprint is null)
            return releaseIds;
        object lockObject = new();
        await Parallel.ForEachAsync(
            source: fingerprint.Results,
            body: async (acoustIdFingerprint, t) =>
            {
                if (acoustIdFingerprint.Id == Guid.Empty)
                    return;
                await Parallel.ForEachAsync(
                    source: acoustIdFingerprint.Recordings ?? [],
                    cancellationToken: t,
                    body: async (acoustIdFingerprintRecording, y) =>
                    {
                        if (acoustIdFingerprintRecording is null)
                            return;
                        if (acoustIdFingerprintRecording.Id == Guid.Empty)
                            return;
                        if (acoustIdFingerprintRecording.Releases is null)
                            return;
                        await Parallel.ForEachAsync(
                            source: acoustIdFingerprintRecording.Releases ?? [],
                            cancellationToken: y,
                            body: (fingerprintRelease, _) =>
                            {
                                if (
                                    fingerprintRelease.Id == Guid.Empty
                                    || releaseIds.Any(predicate: r => r == fingerprintRelease.Id)
                                    || !fingerprintRelease
                                        .Title.OrEmpty()
                                        .ContainsSanitized(value: albumName)
                                )
                                    return ValueTask.CompletedTask;

                                lock (lockObject)
                                {
                                    releaseIds.Add(item: fingerprintRelease.Id);
                                }

                                return ValueTask.CompletedTask;
                            }
                        );
                    }
                );
            }
        );
        return releaseIds;
    }
}
