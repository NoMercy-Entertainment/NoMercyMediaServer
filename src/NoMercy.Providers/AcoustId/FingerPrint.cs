using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;

namespace NoMercy.Providers.AcoustId;

public static class FingerPrint
{
    private static AcoustIdFingerprintClient AcoustIdFingerprintClient { get; }

    static FingerPrint()
    {
        AcoustIdFingerprintClient = new();
    }

    private static async Task<AcoustIdFingerprint?> GetFingerprint(string file, bool priority = false)
    {
        return await AcoustIdFingerprintClient.Lookup(file, priority);
    }

    public static async Task<List<Guid>> GetReleaseIds(string file, string albumName = "")
    {
        List<Guid> releaseIds = [];
        AcoustIdFingerprint? fingerprint = await GetFingerprint(file, true);
        if (fingerprint is null) return releaseIds;
        object lockObject = new();
        await Parallel.ForEachAsync(fingerprint.Results, async (acoustIdFingerprint, t) =>
        {
            if (acoustIdFingerprint.Id == Guid.Empty) return;
            await Parallel.ForEachAsync(acoustIdFingerprint.Recordings ?? [], t,
                async (acoustIdFingerprintRecording, y) =>
                {
                    if (acoustIdFingerprintRecording is null) return;
                    if (acoustIdFingerprintRecording.Id == Guid.Empty) return;
                    if (acoustIdFingerprintRecording.Releases is null) return;
                    await Parallel.ForEachAsync(acoustIdFingerprintRecording.Releases ?? [], y,
                        (fingerprintRelease, _) =>
                        {
                            if (
                                fingerprintRelease.Id == Guid.Empty ||
                                releaseIds.Any(r => r == fingerprintRelease.Id) ||
                                !(fingerprintRelease.Title ?? "").ContainsSanitized(albumName)
                            )
                                return ValueTask.CompletedTask;

                            lock (lockObject)
                            {
                                releaseIds.Add(fingerprintRelease.Id);
                            }

                            return ValueTask.CompletedTask;
                        });
                });
        });
        return releaseIds;
    }
}